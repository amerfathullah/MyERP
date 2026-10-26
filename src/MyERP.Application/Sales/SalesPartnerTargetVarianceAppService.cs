using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesPartnerTargetVarianceAppService : ApplicationService, ISalesPartnerTargetVarianceAppService
{
    private readonly IRepository<SalesPartner, Guid> _salesPartnerRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SalesPartnerTargetVarianceAppService(
        IRepository<SalesPartner, Guid> salesPartnerRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<ItemGroup, Guid> itemGroupRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _salesPartnerRepo = salesPartnerRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesOrderRepo = salesOrderRepo;
        _itemRepo = itemRepo;
        _itemGroupRepo = itemGroupRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SalesPartnerTargetVarianceReportDto> GetReportAsync(GetSalesPartnerTargetVarianceInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var docType = string.Equals(input.DocType?.Trim(), "Sales Order", StringComparison.OrdinalIgnoreCase)
            ? "Sales Order"
            : "Sales Invoice";

        var targetOn = string.Equals(input.TargetOn?.Trim(), "Quantity", StringComparison.OrdinalIgnoreCase)
            ? "Quantity"
            : "Amount";

        DateTime? fromDate = input.FromDate;
        DateTime? toDate = input.ToDate;
        string? fiscalYearName = null;

        if (input.FiscalYearId.HasValue)
        {
            var fy = await _fiscalYearRepo.FindAsync(input.FiscalYearId.Value);
            if (fy != null)
            {
                fiscalYearName = fy.Name;
                fromDate ??= fy.StartDate;
                toDate ??= fy.EndDate;
            }
        }

        if (!fromDate.HasValue && !toDate.HasValue)
        {
            var now = DateTime.UtcNow;
            fromDate = new DateTime(now.Year, 1, 1);
            toDate = new DateTime(now.Year, 12, 31, 23, 59, 59);
        }

        // Master maps
        var itemGroups = (await _itemGroupRepo.GetQueryableAsync()).ToList();
        var itemGroupMap = itemGroups.ToDictionary(g => g.Id, g => g.Name);

        var items = (await _itemRepo.GetQueryableAsync()).Where(i => i.CompanyId == input.CompanyId).ToList();
        var itemMap = items.ToDictionary(i => i.Id, i => i);

        var spQuery = await _salesPartnerRepo.WithDetailsAsync(sp => sp.Targets);
        var allSalesPartners = spQuery.ToList();
        var salesPartners = input.SalesPartnerId.HasValue
            ? allSalesPartners.Where(sp => sp.Id == input.SalesPartnerId.Value).ToList()
            : allSalesPartners;

        var allPartnerMap = allSalesPartners.ToDictionary(sp => sp.Id, sp => sp.Name);

        // Build tree rollup map for Item Group descendants
        var igChildrenMap = itemGroups
            .Where(g => g.ParentId.HasValue)
            .GroupBy(g => g.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        HashSet<Guid> GetItemGroupDescendantIds(Guid rootId)
        {
            var result = new HashSet<Guid> { rootId };
            var queue = new Queue<Guid>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (igChildrenMap.TryGetValue(cur, out var children))
                {
                    foreach (var ch in children)
                    {
                        if (result.Add(ch))
                            queue.Enqueue(ch);
                    }
                }
            }
            return result;
        }

        var igDescendantsMap = itemGroups.ToDictionary(g => g.Id, g => GetItemGroupDescendantIds(g.Id));

        // Gather targets: (SalesPartnerId, ItemGroupId?) -> Target
        var targets = new Dictionary<(Guid SalesPartnerId, Guid? ItemGroupId), decimal>();
        foreach (var partner in salesPartners)
        {
            var pTargets = partner.Targets.AsEnumerable();
            if (input.FiscalYearId.HasValue)
            {
                pTargets = pTargets.Where(t => !t.FiscalYearId.HasValue || t.FiscalYearId == input.FiscalYearId.Value);
            }
            if (input.ItemGroupId.HasValue)
            {
                pTargets = pTargets.Where(t => !t.ItemGroupId.HasValue || t.ItemGroupId == input.ItemGroupId.Value);
            }

            foreach (var t in pTargets)
            {
                var val = targetOn == "Quantity" ? t.TargetQty : t.TargetAmount;
                var key = (partner.Id, t.ItemGroupId);
                targets[key] = targets.GetValueOrDefault(key, 0m) + val;
            }
        }

        // Query actual sales transactions scoped by Company and date range (PR #62324 / commit 7d493a6678)
        var actualRows = new List<(Guid SalesPartnerId, Guid ItemId, Guid? ItemGroupId, decimal Qty, decimal Amount)>();

        if (docType == "Sales Order")
        {
            var soQuery = await _salesOrderRepo.WithDetailsAsync(so => so.Items);
            var orders = soQuery
                .Where(so => so.CompanyId == input.CompanyId
                          && so.Status == DocumentStatus.Submitted
                          && so.SalesPartnerId.HasValue
                          && so.OrderDate >= fromDate!.Value
                          && so.OrderDate <= toDate!.Value)
                .ToList();

            foreach (var so in orders)
            {
                var partnerId = so.SalesPartnerId!.Value;
                foreach (var line in so.Items)
                {
                    if (itemMap.TryGetValue(line.ItemId, out var item))
                    {
                        actualRows.Add((partnerId, item.Id, item.ItemGroupId, line.Quantity, line.LineTotal));
                    }
                }
            }
        }
        else
        {
            var siQuery = await _salesInvoiceRepo.WithDetailsAsync(si => si.Items);
            var invoices = siQuery
                .Where(si => si.CompanyId == input.CompanyId
                          && (si.Status == DocumentStatus.Submitted || si.Status == DocumentStatus.Posted)
                          && si.SalesPartnerId.HasValue
                          && si.IssueDate >= fromDate!.Value
                          && si.IssueDate <= toDate!.Value)
                .ToList();

            foreach (var si in invoices)
            {
                var partnerId = si.SalesPartnerId!.Value;
                var multiplier = si.IsReturn ? -1m : 1m;
                foreach (var line in si.Items)
                {
                    if (itemMap.TryGetValue(line.ItemId, out var item))
                    {
                        actualRows.Add((partnerId, item.Id, item.ItemGroupId, line.Quantity * multiplier, line.LineTotal * multiplier));
                    }
                }
            }
        }

        // Calculate achieved per (SalesPartnerId, ItemGroupId?) key
        var allKeys = new HashSet<(Guid SalesPartnerId, Guid? ItemGroupId)>(targets.Keys);
        if (allKeys.Count == 0)
        {
            foreach (var p in salesPartners)
            {
                allKeys.Add((p.Id, input.ItemGroupId));
            }
        }

        var reportRows = new List<SalesPartnerTargetVarianceRowDto>();

        foreach (var key in allKeys)
        {
            var partnerId = key.SalesPartnerId;
            var itemGroupId = key.ItemGroupId;
            var targetVal = targets.GetValueOrDefault(key, 0m);

            HashSet<Guid>? allowedItemGroups = null;
            if (itemGroupId.HasValue)
            {
                if (!igDescendantsMap.TryGetValue(itemGroupId.Value, out allowedItemGroups))
                {
                    allowedItemGroups = new HashSet<Guid> { itemGroupId.Value };
                }
            }

            var matchingActuals = actualRows
                .Where(r => r.SalesPartnerId == partnerId
                         && (allowedItemGroups == null || (r.ItemGroupId.HasValue && allowedItemGroups.Contains(r.ItemGroupId.Value))))
                .ToList();

            var achievedVal = targetOn == "Quantity"
                ? matchingActuals.Sum(r => r.Qty)
                : matchingActuals.Sum(r => r.Amount);

            var variance = achievedVal - targetVal;
            var percentage = targetVal > 0 ? Math.Round((achievedVal / targetVal) * 100m, 2) : 0m;

            var partnerName = allPartnerMap.GetValueOrDefault(partnerId, "Unknown");
            var itemGroupName = itemGroupId.HasValue && itemGroupMap.TryGetValue(itemGroupId.Value, out var ign)
                ? ign
                : "All Item Groups";

            reportRows.Add(new SalesPartnerTargetVarianceRowDto
            {
                SalesPartnerId = partnerId,
                SalesPartnerName = partnerName,
                ItemGroupId = itemGroupId,
                ItemGroupName = itemGroupName,
                Target = Math.Round(targetVal, 2),
                Achieved = Math.Round(achievedVal, 2),
                Variance = Math.Round(variance, 2),
                AchievementPercentage = percentage
            });
        }

        reportRows = reportRows
            .OrderBy(r => r.SalesPartnerName)
            .ThenBy(r => r.ItemGroupName)
            .ToList();

        var totalTarget = reportRows.Sum(r => r.Target);
        var totalAchieved = reportRows.Sum(r => r.Achieved);
        var totalVariance = totalAchieved - totalTarget;
        var overallPercentage = totalTarget > 0 ? Math.Round((totalAchieved / totalTarget) * 100m, 2) : 0m;

        return new SalesPartnerTargetVarianceReportDto
        {
            Currency = currency,
            DocType = docType,
            TargetOn = targetOn,
            FiscalYearName = fiscalYearName,
            Rows = reportRows,
            TotalTarget = Math.Round(totalTarget, 2),
            TotalAchieved = Math.Round(totalAchieved, 2),
            TotalVariance = Math.Round(totalVariance, 2),
            OverallAchievementPercentage = overallPercentage
        };
    }
}
