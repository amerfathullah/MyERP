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
public class TerritoryTargetVarianceAppService : ApplicationService, ITerritoryTargetVarianceAppService
{
    private readonly IRepository<Territory, Guid> _territoryRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo;
    private readonly IRepository<Company, Guid> _companyRepo;

    public TerritoryTargetVarianceAppService(
        IRepository<Territory, Guid> territoryRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<ItemGroup, Guid> itemGroupRepo,
        IRepository<FiscalYear, Guid> fiscalYearRepo,
        IRepository<Company, Guid> companyRepo)
    {
        _territoryRepo = territoryRepo;
        _customerRepo = customerRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesOrderRepo = salesOrderRepo;
        _itemRepo = itemRepo;
        _itemGroupRepo = itemGroupRepo;
        _fiscalYearRepo = fiscalYearRepo;
        _companyRepo = companyRepo;
    }

    public async Task<TerritoryTargetVarianceReportDto> GetReportAsync(GetTerritoryTargetVarianceInput input)
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

        var territoryQuery = await _territoryRepo.WithDetailsAsync(t => t.Targets);
        var allTerritories = territoryQuery.ToList();
        var territories = input.TerritoryId.HasValue
            ? allTerritories.Where(t => t.Id == input.TerritoryId.Value).ToList()
            : allTerritories;

        var allTerritoryMap = allTerritories.ToDictionary(t => t.Id, t => t.Name);

        // Build tree rollup map for Territory descendants
        var territoryChildrenMap = allTerritories
            .Where(t => t.ParentId.HasValue)
            .GroupBy(t => t.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        HashSet<Guid> GetTerritoryDescendantIds(Guid rootId)
        {
            var result = new HashSet<Guid> { rootId };
            var queue = new Queue<Guid>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (territoryChildrenMap.TryGetValue(cur, out var children))
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

        var territoryDescendantsMap = allTerritories.ToDictionary(t => t.Id, t => GetTerritoryDescendantIds(t.Id));

        // Build tree rollup map for Item Group descendants
        var igChildrenMap = itemGroups
            .Where(g => g.ParentId.HasValue)
            .GroupBy(g => g.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        HashSet<Guid> GetIgDescendantIds(Guid rootId)
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

        var igDescendantsMap = itemGroups.ToDictionary(g => g.Id, g => GetIgDescendantIds(g.Id));

        // Customer territory mapping
        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customerTerritoryMap = customerQuery
            .Where(c => c.TerritoryId.HasValue)
            .Select(c => new { c.Id, TerritoryId = c.TerritoryId!.Value })
            .ToDictionary(c => c.Id, c => c.TerritoryId);

        // 1. Gather Targets
        var targetDict = new Dictionary<(Guid TerritoryId, Guid? ItemGroupId), decimal>();

        foreach (var terr in territories)
        {
            foreach (var t in terr.Targets)
            {
                if (input.FiscalYearId.HasValue && t.FiscalYearId.HasValue && t.FiscalYearId != input.FiscalYearId.Value)
                    continue;

                if (input.ItemGroupId.HasValue && t.ItemGroupId.HasValue && t.ItemGroupId != input.ItemGroupId.Value)
                    continue;

                var key = (terr.Id, t.ItemGroupId);
                var targetVal = targetOn == "Quantity" ? t.TargetQty : t.TargetAmount;

                if (!targetDict.ContainsKey(key))
                    targetDict[key] = 0;
                targetDict[key] += targetVal;
            }
        }

        // 2. Gather Actual Contributions from orders/invoices linked to customers in relevant territories
        var actualContributions = new List<(Guid TerritoryId, Guid? ItemGroupId, decimal Amount)>();

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

            foreach (var order in orders)
            {
                if (!customerTerritoryMap.TryGetValue(order.CustomerId, out var custTerritoryId))
                    continue;

                foreach (var item in order.Items)
                {
                    itemMap.TryGetValue(item.ItemId, out var itm);
                    var itemGroupId = itm?.ItemGroupId;

                    if (input.ItemGroupId.HasValue && itemGroupId != input.ItemGroupId.Value)
                        continue;

                    var netAmount = item.Quantity * item.UnitPrice;
                    var valToAttribute = targetOn == "Quantity" ? item.Quantity : netAmount;

                    actualContributions.Add((custTerritoryId, itemGroupId, valToAttribute));
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

            foreach (var inv in invoices)
            {
                if (!customerTerritoryMap.TryGetValue(inv.CustomerId, out var custTerritoryId))
                    continue;

                foreach (var item in inv.Items)
                {
                    itemMap.TryGetValue(item.ItemId, out var itm);
                    var itemGroupId = itm?.ItemGroupId;

                    if (input.ItemGroupId.HasValue && itemGroupId != input.ItemGroupId.Value)
                        continue;

                    var netAmount = item.Quantity * item.UnitPrice;
                    var valToAttribute = targetOn == "Quantity" ? item.Quantity : netAmount;

                    actualContributions.Add((custTerritoryId, itemGroupId, valToAttribute));
                }
            }
        }

        // 3. Compute Achieved per Territory (with tree rollups)
        var achievedDict = new Dictionary<(Guid TerritoryId, Guid? ItemGroupId), decimal>();

        foreach (var terr in territories)
        {
            var terrDescendants = territoryDescendantsMap[terr.Id];

            var terrTargets = targetDict.Keys.Where(k => k.TerritoryId == terr.Id).ToList();

            // If no target defined on this territory but actuals exist for descendants
            if (terrTargets.Count == 0 && actualContributions.Any(c => terrDescendants.Contains(c.TerritoryId)))
            {
                terrTargets.Add((terr.Id, null));
            }

            foreach (var (_, targetItemGroupId) in terrTargets)
            {
                var key = (terr.Id, targetItemGroupId);
                decimal achievedSum = 0;

                HashSet<Guid>? validItemGroups = null;
                if (targetItemGroupId.HasValue && igDescendantsMap.TryGetValue(targetItemGroupId.Value, out var descIg))
                {
                    validItemGroups = descIg;
                }

                foreach (var c in actualContributions)
                {
                    if (!terrDescendants.Contains(c.TerritoryId))
                        continue;

                    if (targetItemGroupId.HasValue)
                    {
                        if (!c.ItemGroupId.HasValue || (validItemGroups != null && !validItemGroups.Contains(c.ItemGroupId.Value)))
                            continue;
                    }

                    achievedSum += c.Amount;
                }

                achievedDict[key] = achievedSum;
            }
        }

        // 4. Combine into rows
        var allKeys = targetDict.Keys.Union(achievedDict.Keys).Distinct().ToList();
        var rows = new List<TerritoryTargetVarianceRowDto>();

        foreach (var (territoryId, itemGroupId) in allKeys)
        {
            targetDict.TryGetValue((territoryId, itemGroupId), out var target);
            achievedDict.TryGetValue((territoryId, itemGroupId), out var achieved);

            var variance = achieved - target;
            var pct = target > 0 ? Math.Round((achieved / target) * 100m, 2) : 0m;

            string terrName = allTerritoryMap.TryGetValue(territoryId, out var tn) ? tn : string.Empty;
            string igName = itemGroupId.HasValue && itemGroupMap.TryGetValue(itemGroupId.Value, out var ign)
                ? ign
                : (itemGroupId.HasValue ? string.Empty : "All Item Groups");

            rows.Add(new TerritoryTargetVarianceRowDto
            {
                TerritoryId = territoryId,
                TerritoryName = terrName,
                ItemGroupId = itemGroupId,
                ItemGroupName = igName,
                Target = Math.Round(target, 2),
                Achieved = Math.Round(achieved, 2),
                Variance = Math.Round(variance, 2),
                AchievementPercentage = pct
            });
        }

        rows = rows
            .OrderBy(r => r.TerritoryName)
            .ThenBy(r => r.ItemGroupName)
            .ToList();

        var totalTarget = rows.Sum(r => r.Target);
        var totalAchieved = rows.Sum(r => r.Achieved);
        var totalVariance = totalAchieved - totalTarget;
        var overallPct = totalTarget > 0 ? Math.Round((totalAchieved / totalTarget) * 100m, 2) : 0m;

        return new TerritoryTargetVarianceReportDto
        {
            Currency = currency,
            DocType = docType,
            TargetOn = targetOn,
            FiscalYearName = fiscalYearName,
            Rows = rows,
            TotalTarget = totalTarget,
            TotalAchieved = totalAchieved,
            TotalVariance = totalVariance,
            OverallAchievementPercentage = overallPct
        };
    }
}
