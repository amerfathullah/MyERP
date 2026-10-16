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
public class SalesPersonTargetVarianceAppService : ApplicationService, ISalesPersonTargetVarianceAppService
{
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo;
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public SalesPersonTargetVarianceAppService(
        IRepository<SalesPerson, Guid> salesPersonRepo,
        IRepository<SalesTeamEntry, Guid> teamEntryRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<ItemGroup, Guid> itemGroupRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _salesPersonRepo = salesPersonRepo;
        _teamEntryRepo = teamEntryRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesOrderRepo = salesOrderRepo;
        _itemRepo = itemRepo;
        _itemGroupRepo = itemGroupRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<SalesPersonTargetVarianceReportDto> GetReportAsync(GetSalesPersonTargetVarianceInput input)
    {
        var company = await _companyRepo.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var docType = string.Equals(input.DocType?.Trim(), "Sales Order", StringComparison.OrdinalIgnoreCase)
            ? "Sales Order"
            : "Sales Invoice";
        var parentTypeName = docType.Replace(" ", "");

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

        var spQuery = await _salesPersonRepo.WithDetailsAsync(sp => sp.Targets);
        var salesPersons = spQuery.ToList();
        if (input.SalesPersonId.HasValue)
            salesPersons = salesPersons.Where(sp => sp.Id == input.SalesPersonId.Value).ToList();

        var spMap = salesPersons.ToDictionary(sp => sp.Id, sp => sp.Name);

        // 1. Gather Targets
        var targetDict = new Dictionary<(Guid SalesPersonId, Guid? ItemGroupId), decimal>();

        foreach (var sp in salesPersons)
        {
            foreach (var t in sp.Targets)
            {
                if (input.FiscalYearId.HasValue && t.FiscalYearId.HasValue && t.FiscalYearId != input.FiscalYearId.Value)
                    continue;

                if (input.ItemGroupId.HasValue && t.ItemGroupId.HasValue && t.ItemGroupId != input.ItemGroupId.Value)
                    continue;

                var key = (sp.Id, t.ItemGroupId);
                var targetVal = targetOn == "Quantity" ? t.TargetQty : t.TargetAmount;

                if (!targetDict.ContainsKey(key))
                    targetDict[key] = 0;
                targetDict[key] += targetVal;
            }
        }

        // 2. Gather Achieved
        var achievedDict = new Dictionary<(Guid SalesPersonId, Guid? ItemGroupId), decimal>();
        var spIds = salesPersons.Select(sp => sp.Id).ToHashSet();

        if (docType == "Sales Order")
        {
            var soQuery = await _salesOrderRepo.WithDetailsAsync(so => so.Items);
            var orders = soQuery.Where(so =>
                so.CompanyId == input.CompanyId &&
                so.Status != DocumentStatus.Cancelled &&
                so.Status != DocumentStatus.Draft).ToList();

            if (fromDate.HasValue)
                orders = orders.Where(so => so.OrderDate >= fromDate.Value).ToList();
            if (toDate.HasValue)
                orders = orders.Where(so => so.OrderDate <= toDate.Value).ToList();

            if (orders.Count > 0)
            {
                var orderIds = orders.Select(so => so.Id).ToHashSet();
                var entries = (await _teamEntryRepo.GetQueryableAsync())
                    .Where(e => e.ParentType == parentTypeName && orderIds.Contains(e.ParentId) && spIds.Contains(e.SalesPersonId))
                    .ToList();

                var teamByOrder = entries.GroupBy(e => e.ParentId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var order in orders)
                {
                    if (!teamByOrder.TryGetValue(order.Id, out var orderEntries) || orderEntries.Count == 0)
                        continue;

                    foreach (var item in order.Items)
                    {
                        itemMap.TryGetValue(item.ItemId, out var itm);
                        var itemGroupId = itm?.ItemGroupId;

                        if (input.ItemGroupId.HasValue && itemGroupId != input.ItemGroupId.Value)
                            continue;

                        var netAmount = item.Quantity * item.UnitPrice;
                        var valToAttribute = targetOn == "Quantity" ? item.Quantity : netAmount;

                        foreach (var entry in orderEntries)
                        {
                            var contribution = Math.Round(valToAttribute * entry.AllocatedPercentage / 100m, 2);
                            var key = (entry.SalesPersonId, itemGroupId);

                            if (!achievedDict.ContainsKey(key))
                                achievedDict[key] = 0;
                            achievedDict[key] += contribution;
                        }
                    }
                }
            }
        }
        else // Sales Invoice
        {
            var siQuery = await _salesInvoiceRepo.WithDetailsAsync(si => si.Items);
            var invoices = siQuery.Where(si =>
                si.CompanyId == input.CompanyId &&
                si.Status != DocumentStatus.Cancelled &&
                si.Status != DocumentStatus.Draft).ToList();

            if (fromDate.HasValue)
                invoices = invoices.Where(si => si.IssueDate >= fromDate.Value).ToList();
            if (toDate.HasValue)
                invoices = invoices.Where(si => si.IssueDate <= toDate.Value).ToList();

            if (invoices.Count > 0)
            {
                var invoiceIds = invoices.Select(si => si.Id).ToHashSet();
                var entries = (await _teamEntryRepo.GetQueryableAsync())
                    .Where(e => e.ParentType == parentTypeName && invoiceIds.Contains(e.ParentId) && spIds.Contains(e.SalesPersonId))
                    .ToList();

                var teamByInvoice = entries.GroupBy(e => e.ParentId).ToDictionary(g => g.Key, g => g.ToList());

                foreach (var invoice in invoices)
                {
                    if (!teamByInvoice.TryGetValue(invoice.Id, out var invEntries) || invEntries.Count == 0)
                        continue;

                    foreach (var item in invoice.Items)
                    {
                        itemMap.TryGetValue(item.ItemId, out var itm);
                        var itemGroupId = itm?.ItemGroupId;

                        if (input.ItemGroupId.HasValue && itemGroupId != input.ItemGroupId.Value)
                            continue;

                        var netAmount = item.Quantity * item.UnitPrice;
                        var valToAttribute = targetOn == "Quantity" ? item.Quantity : netAmount;

                        foreach (var entry in invEntries)
                        {
                            var contribution = Math.Round(valToAttribute * entry.AllocatedPercentage / 100m, 2);
                            var key = (entry.SalesPersonId, itemGroupId);

                            if (!achievedDict.ContainsKey(key))
                                achievedDict[key] = 0;
                            achievedDict[key] += contribution;
                        }
                    }
                }
            }
        }

        // 3. Merge keys
        var allKeys = targetDict.Keys.Union(achievedDict.Keys).ToList();

        if (input.ItemGroupId.HasValue)
            allKeys = allKeys.Where(k => k.ItemGroupId == input.ItemGroupId.Value).ToList();
        if (input.SalesPersonId.HasValue)
            allKeys = allKeys.Where(k => k.SalesPersonId == input.SalesPersonId.Value).ToList();

        var rows = new List<SalesPersonTargetVarianceRowDto>();

        foreach (var key in allKeys)
        {
            spMap.TryGetValue(key.SalesPersonId, out var spName);
            string itemGroupName = "All Item Groups";
            if (key.ItemGroupId.HasValue && itemGroupMap.TryGetValue(key.ItemGroupId.Value, out var ign))
                itemGroupName = ign;

            targetDict.TryGetValue(key, out var targetVal);
            achievedDict.TryGetValue(key, out var achievedVal);

            var variance = Math.Round(achievedVal - targetVal, 2);
            var achievementPct = targetVal > 0 ? Math.Round(achievedVal / targetVal * 100m, 2) : (achievedVal > 0 ? 100m : 0m);

            rows.Add(new SalesPersonTargetVarianceRowDto
            {
                SalesPersonId = key.SalesPersonId,
                SalesPersonName = spName ?? key.SalesPersonId.ToString()[..8],
                ItemGroupId = key.ItemGroupId,
                ItemGroupName = itemGroupName,
                Target = Math.Round(targetVal, 2),
                Achieved = Math.Round(achievedVal, 2),
                Variance = variance,
                AchievementPercentage = achievementPct,
            });
        }

        var sortedRows = rows
            .OrderBy(r => r.SalesPersonName)
            .ThenBy(r => r.ItemGroupName)
            .ToList();

        var totalTarget = sortedRows.Sum(r => r.Target);
        var totalAchieved = sortedRows.Sum(r => r.Achieved);
        var totalVariance = Math.Round(totalAchieved - totalTarget, 2);
        var overallPct = totalTarget > 0 ? Math.Round(totalAchieved / totalTarget * 100m, 2) : (totalAchieved > 0 ? 100m : 0m);

        return new SalesPersonTargetVarianceReportDto
        {
            Currency = currency,
            DocType = docType,
            TargetOn = targetOn,
            FiscalYearName = fiscalYearName,
            Rows = sortedRows,
            TotalTarget = totalTarget,
            TotalAchieved = totalAchieved,
            TotalVariance = totalVariance,
            OverallAchievementPercentage = overallPct,
        };
    }
}
