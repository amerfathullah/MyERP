using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Core;
using Microsoft.AspNetCore.Authorization;
using MyERP.Sales.DomainServices;
using MyERP.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesInvoices.Default)]
public class SalesAnalyticsAppService : ApplicationService, ISalesAnalyticsAppService
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;

    public SalesAnalyticsAppService(
        IRepository<SalesInvoice, Guid> siRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Item, Guid> itemRepo)
    {
        _siRepo = siRepo;
        _customerRepo = customerRepo;
        _itemRepo = itemRepo;
    }

    public async Task<SalesAnalyticsReportDto> GetReportAsync(SalesAnalyticsRequestDto input)
    {
        var periods = GeneratePeriods(input.FromDate, input.ToDate, input.PeriodType);
        var useQty = string.Equals(input.ValueField, "Quantity", StringComparison.OrdinalIgnoreCase);

        var siQuery = await _siRepo.GetQueryableAsync();
        var invoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId
                && si.Status == DocumentStatus.Posted
                && !si.IsReturn
                && !si.IsOpening
                && si.IssueDate >= input.FromDate
                && si.IssueDate <= input.ToDate)
            .ToList();

        // Resolve entity names
        var customerNames = new Dictionary<Guid, string>();
        var itemNames = new Dictionary<Guid, string>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
            if (customerIds.Count > 0)
            {
                var customers = (await _customerRepo.GetQueryableAsync())
                    .Where(c => customerIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name })
                    .ToList();
                customerNames = customers.ToDictionary(c => c.Id, c => c.Name);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var allItemIds = invoices.SelectMany(si => si.Items).Select(i => i.ItemId).Distinct().ToList();
            if (allItemIds.Count > 0)
            {
                var items = (await _itemRepo.GetQueryableAsync())
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, i.ItemName })
                    .ToList();
                itemNames = items.ToDictionary(i => i.Id, i => i.ItemName);
            }
        }

        // Group data by entity + period
        var rows = new List<SalesAnalyticsRowDto>();

        if (input.GroupBy == AnalyticsGroupBy.Customer)
        {
            var grouped = invoices.GroupBy(si => si.CustomerId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), customerNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var itemGroups = invoices
                .SelectMany(si => si.Items.Select(item => new { si.IssueDate, item.ItemId, item.Quantity, Amount = item.Quantity * item.UnitPrice }))
                .GroupBy(x => x.ItemId);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.IssueDate >= period.From && x.IssueDate <= period.To);
                    periodValues.Add(useQty ? periodItems.Sum(x => x.Quantity) : periodItems.Sum(x => x.Amount));
                }
                var total = periodValues.Sum();
                var growth = CalculateGrowth(periodValues);
                rows.Add(new SalesAnalyticsRowDto
                {
                    EntityId = g.Key.ToString(),
                    EntityName = itemNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    PeriodValues = periodValues,
                    Total = total,
                    Growth = growth,
                });
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.ItemGroup)
        {
            var allItemIds = invoices.SelectMany(si => si.Items).Select(i => i.ItemId).Distinct().ToList();
            var itemGroupMap = new Dictionary<Guid, string>();
            if (allItemIds.Count > 0)
            {
                var items = (await _itemRepo.GetQueryableAsync())
                    .Where(i => allItemIds.Contains(i.Id))
                    .Select(i => new { i.Id, ItemGroup = !string.IsNullOrEmpty(i.ItemGroup) ? i.ItemGroup : "All Item Groups" })
                    .ToList();
                itemGroupMap = items.ToDictionary(i => i.Id, i => i.ItemGroup);
            }

            var itemGroups = invoices
                .SelectMany(si => si.Items.Select(item => new
                {
                    si.IssueDate,
                    ItemGroup = itemGroupMap.GetValueOrDefault(item.ItemId, "All Item Groups"),
                    item.Quantity,
                    Amount = item.Quantity * item.UnitPrice,
                }))
                .GroupBy(x => x.ItemGroup);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.IssueDate >= period.From && x.IssueDate <= period.To);
                    periodValues.Add(useQty ? periodItems.Sum(x => x.Quantity) : periodItems.Sum(x => x.Amount));
                }
                var total = periodValues.Sum();
                var growth = CalculateGrowth(periodValues);
                rows.Add(new SalesAnalyticsRowDto
                {
                    EntityId = g.Key,
                    EntityName = g.Key,
                    PeriodValues = periodValues,
                    Total = total,
                    Growth = growth,
                });
            }
        }
        else
        {
            // Territory/SalesPerson: group by customer as fallback
            var grouped = invoices.GroupBy(si => si.CustomerId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), customerNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    g.ToList(), periods, useQty);
                rows.Add(row);
            }
        }

        // Filter by entity IDs if specified (ERPNext commit 3f29cdf8d2)
        if (input.EntityIds != null && input.EntityIds.Count > 0)
        {
            var entitySet = input.EntityIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows.Where(r => entitySet.Contains(r.EntityId) || entitySet.Contains(r.EntityName)).ToList();
        }

        // Sort by total descending (top revenue first)
        rows = rows.OrderByDescending(r => r.Total).ToList();

        // Calculate period totals
        var periodTotals = new List<decimal>();
        for (int i = 0; i < periods.Count; i++)
        {
            periodTotals.Add(rows.Sum(r => r.PeriodValues.Count > i ? r.PeriodValues[i] : 0m));
        }

        return new SalesAnalyticsReportDto
        {
            PeriodLabels = periods.Select(p => p.Label).ToList(),
            Rows = rows,
            GrandTotal = rows.Sum(r => r.Total),
            PeriodTotals = periodTotals,
        };
    }

    private SalesAnalyticsRowDto BuildRow(string entityId, string entityName, List<SalesInvoice> invoices,
        List<PeriodRange> periods, bool useQty)
    {
        var periodValues = new List<decimal>();
        foreach (var period in periods)
        {
            var periodInvoices = invoices.Where(si => si.IssueDate >= period.From && si.IssueDate <= period.To);
            if (useQty)
                periodValues.Add(periodInvoices.SelectMany(si => si.Items).Sum(i => i.Quantity));
            else
                periodValues.Add(periodInvoices.Sum(si => si.GrandTotal));
        }

        return new SalesAnalyticsRowDto
        {
            EntityId = entityId,
            EntityName = entityName,
            PeriodValues = periodValues,
            Total = periodValues.Sum(),
            Growth = CalculateGrowth(periodValues),
        };
    }

    public async Task<List<CustomerCreditBalanceDto>> GetCustomerCreditBalanceReportAsync(CustomerCreditBalanceInputDto input)
    {
        var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
        await companyRepo.GetAsync(input.CompanyId);
        var creditLimitService = LazyServiceProvider.LazyGetRequiredService<CreditLimitService>();

        var customerQuery = await _customerRepo.GetQueryableAsync();
        if (input.CustomerId.HasValue)
        {
            customerQuery = customerQuery.Where(c => c.Id == input.CustomerId.Value);
        }

        var customers = customerQuery.ToList();
        var result = new List<CustomerCreditBalanceDto>();

        foreach (var customer in customers)
        {
            var (creditLimit, _) = await creditLimitService.GetResolvedCreditLimitAsync(customer.Id, input.CompanyId);

            // Per ERPNext PR #60286 / commit 56fdfcbbcd:
            // List customers with group or company credit limits in Customer Credit Balance report
            // Only list customers that have an active credit limit (> 0)
            if (creditLimit > 0)
            {
                var outstanding = await creditLimitService.GetCustomerOutstandingAsync(customer.Id, input.CompanyId);
                result.Add(new CustomerCreditBalanceDto
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    CreditLimit = creditLimit,
                    OutstandingAmount = outstanding,
                    CreditBalance = creditLimit - outstanding,
                    BypassCreditLimitCheckAtSalesOrder = customer.BypassCreditLimitCheckAtSalesOrder,
                    IsFrozen = customer.IsFrozen,
                    Disabled = !customer.IsActive
                });
            }
        }

        return result;
    }

    public async Task<List<CustomerWithoutSalesDto>> GetCustomersWithoutSalesTransactionsAsync(CustomersWithoutSalesInputDto input)
    {
        if (input.CompanyId.HasValue)
        {
            var companyRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Company, Guid>>();
            await companyRepo.GetAsync(input.CompanyId.Value);
        }

        var soRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<SalesOrder, Guid>>();
        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var customerGroupRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<CustomerGroup, Guid>>();
        var territoryRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Territory, Guid>>();

        // Query customers
        var customerQuery = await _customerRepo.GetQueryableAsync();
        var customers = customerQuery.ToList();

        // 1. Sales Invoice (includes POS Invoice where IsPos == true per PR #60308 / commit 21e96defdb)
        var siQuery = await _siRepo.GetQueryableAsync();
        siQuery = siQuery.Where(si => si.Status != DocumentStatus.Draft && si.Status != DocumentStatus.Cancelled);
        if (input.CompanyId.HasValue)
        {
            siQuery = siQuery.Where(si => si.CompanyId == input.CompanyId.Value);
        }
        var siCustomerIds = siQuery.Select(si => si.CustomerId).Distinct().ToHashSet();

        // 2. Sales Order
        var soQuery = await soRepo.GetQueryableAsync();
        soQuery = soQuery.Where(so => so.Status != DocumentStatus.Draft && so.Status != DocumentStatus.Cancelled);
        if (input.CompanyId.HasValue)
        {
            soQuery = soQuery.Where(so => so.CompanyId == input.CompanyId.Value);
        }
        var soCustomerIds = soQuery.Select(so => so.CustomerId).Distinct().ToHashSet();

        // 3. Delivery Note (per PR #60308 / commit 21e96defdb)
        var dnQuery = await dnRepo.GetQueryableAsync();
        dnQuery = dnQuery.Where(dn => dn.Status != DocumentStatus.Draft && dn.Status != DocumentStatus.Cancelled);
        if (input.CompanyId.HasValue)
        {
            dnQuery = dnQuery.Where(dn => dn.CompanyId == input.CompanyId.Value);
        }
        var dnCustomerIds = dnQuery.Select(dn => dn.CustomerId).Distinct().ToHashSet();

        // Filter customers who have NO transactions in any of the three doctypes
        var filteredCustomers = customers
            .Where(c => !siCustomerIds.Contains(c.Id)
                     && !soCustomerIds.Contains(c.Id)
                     && !dnCustomerIds.Contains(c.Id))
            .ToList();

        // Resolve CustomerGroup and Territory names
        var groupIds = filteredCustomers.Where(c => c.CustomerGroupId.HasValue).Select(c => c.CustomerGroupId!.Value).Distinct().ToList();
        var territoryIds = filteredCustomers.Where(c => c.TerritoryId.HasValue).Select(c => c.TerritoryId!.Value).Distinct().ToList();

        var groupNames = new Dictionary<Guid, string>();
        if (groupIds.Count > 0)
        {
            groupNames = (await customerGroupRepo.GetQueryableAsync())
                .Where(g => groupIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Name })
                .ToDictionary(g => g.Id, g => g.Name);
        }

        var territoryNames = new Dictionary<Guid, string>();
        if (territoryIds.Count > 0)
        {
            territoryNames = (await territoryRepo.GetQueryableAsync())
                .Where(t => territoryIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name })
                .ToDictionary(t => t.Id, t => t.Name);
        }

        return filteredCustomers.Select(c => new CustomerWithoutSalesDto
        {
            CustomerId = c.Id,
            CustomerName = c.Name,
            CustomerGroup = c.CustomerGroupId.HasValue && groupNames.TryGetValue(c.CustomerGroupId.Value, out var gName) ? gName : null,
            Territory = c.TerritoryId.HasValue && territoryNames.TryGetValue(c.TerritoryId.Value, out var tName) ? tName : null
        }).ToList();
    }

    /// <summary>
    /// Resolves customer-wise item prices identical to how a selling transaction does.
    /// Per ERPNext PR #60300 / commit a1899ecf69:
    /// - Customer filter is mandatory.
    /// - Candidate price lists in priority order: customer default -> fallback to Selling Settings default.
    /// - Rate lookup strictly excludes other customers' special prices to prevent rate leaks.
    /// - Rate lookup filters by active date range (valid_from <= today <= valid_upto) and matching UOM.
    /// - Includes available stock from Bin.
    /// </summary>
    public async Task<List<CustomerWiseItemPriceDto>> GetCustomerWiseItemPriceReportAsync(CustomerWiseItemPriceInputDto input)
    {
        if (input.CustomerId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Customer filter is mandatory for Customer-wise Item Price report.");
        }

        var customer = await _customerRepo.GetAsync(input.CustomerId);

        var priceListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<PriceList, Guid>>();
        var itemPriceRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemPrice, Guid>>();
        var binRepo = LazyServiceProvider.LazyGetService<IRepository<Bin, Guid>>();
        var settingProvider = LazyServiceProvider.LazyGetService<Volo.Abp.Settings.ISettingProvider>();

        var candidatePriceListIds = new List<Guid>();
        var primaryPriceListId = input.PriceListId ?? customer.DefaultPriceListId;
        if (primaryPriceListId.HasValue && primaryPriceListId.Value != Guid.Empty)
        {
            candidatePriceListIds.Add(primaryPriceListId.Value);
        }

        // Check fallback setting
        var fallbackEnabled = true;
        if (settingProvider != null)
        {
            var fallbackVal = await settingProvider.GetOrNullAsync(MyERPSettings.Selling.FallbackToDefaultPriceList);
            if (!string.IsNullOrWhiteSpace(fallbackVal) && bool.TryParse(fallbackVal, out var parsedFallback))
            {
                fallbackEnabled = parsedFallback;
            }
        }

        if (fallbackEnabled)
        {
            Guid? defaultPlId = null;
            if (settingProvider != null)
            {
                var defaultPlVal = await settingProvider.GetOrNullAsync(MyERPSettings.Selling.DefaultPriceList);
                if (!string.IsNullOrWhiteSpace(defaultPlVal) && Guid.TryParse(defaultPlVal, out var parsedPlId))
                {
                    defaultPlId = parsedPlId;
                }
            }

            if (defaultPlId.HasValue && defaultPlId.Value != Guid.Empty && !candidatePriceListIds.Contains(defaultPlId.Value))
            {
                candidatePriceListIds.Add(defaultPlId.Value);
            }
            else if (candidatePriceListIds.Count == 0)
            {
                var plQuery = await priceListRepo.GetQueryableAsync();
                var firstSellingPl = plQuery.FirstOrDefault(p => p.IsSelling && p.IsActive);
                if (firstSellingPl != null)
                {
                    candidatePriceListIds.Add(firstSellingPl.Id);
                }
            }
        }

        var priceListNames = new Dictionary<Guid, string>();
        if (candidatePriceListIds.Count > 0)
        {
            var plQuery = await priceListRepo.GetQueryableAsync();
            priceListNames = plQuery
                .Where(pl => candidatePriceListIds.Contains(pl.Id))
                .Select(pl => new { pl.Id, pl.Name })
                .ToDictionary(pl => pl.Id, pl => pl.Name);
        }

        var itemQuery = await _itemRepo.GetQueryableAsync();
        itemQuery = itemQuery.Where(i => i.IsActive && !i.HasVariants);
        if (input.ItemId.HasValue)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        var items = itemQuery.OrderBy(i => i.ItemName).ToList();
        if (items.Count == 0) return new List<CustomerWiseItemPriceDto>();

        var itemIds = items.Select(i => i.Id).ToList();

        var stockMap = new Dictionary<Guid, decimal>();
        if (binRepo != null)
        {
            var binQuery = await binRepo.GetQueryableAsync();
            stockMap = binQuery
                .Where(b => itemIds.Contains(b.ItemId))
                .GroupBy(b => b.ItemId)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.ActualQty));
        }

        var today = DateTime.UtcNow.Date;
        var ipQuery = await itemPriceRepo.GetQueryableAsync();
        var itemPrices = ipQuery
            .Where(ip => itemIds.Contains(ip.ItemId)
                && candidatePriceListIds.Contains(ip.PriceListId)
                && (ip.CustomerId == null || ip.CustomerId == input.CustomerId)
                && (ip.ValidFrom == null || ip.ValidFrom <= today)
                && (ip.ValidUpto == null || ip.ValidUpto >= today))
            .ToList();

        var result = new List<CustomerWiseItemPriceDto>();

        foreach (var item in items)
        {
            decimal resolvedRate = 0m;
            Guid? resolvedPlId = null;

            foreach (var plId in candidatePriceListIds)
            {
                var matchingPrices = itemPrices
                    .Where(ip => ip.ItemId == item.Id && ip.PriceListId == plId && (string.IsNullOrEmpty(ip.Uom) || ip.Uom == item.Uom))
                    .ToList();

                var bestPrice = matchingPrices.FirstOrDefault(ip => ip.CustomerId == input.CustomerId)
                             ?? matchingPrices.FirstOrDefault(ip => ip.CustomerId == null);

                if (bestPrice != null && bestPrice.PriceListRate > 0)
                {
                    resolvedRate = bestPrice.PriceListRate;
                    resolvedPlId = plId;
                    break;
                }
            }

            if (resolvedPlId == null && candidatePriceListIds.Count > 0)
            {
                resolvedPlId = candidatePriceListIds.First();
            }

            result.Add(new CustomerWiseItemPriceDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                StockUom = item.Uom,
                SellingRate = resolvedRate,
                PriceListId = resolvedPlId,
                PriceListName = resolvedPlId.HasValue && priceListNames.TryGetValue(resolvedPlId.Value, out var plName) ? plName : null,
                AvailableStock = stockMap.GetValueOrDefault(item.Id, 0m)
            });
        }

        return result;
    }

    /// <summary>
    /// Computes Customer Acquisition and Loyalty metrics across territories or periods.
    /// Per ERPNext PR #60697 / commit cdcb0de6b8:
    /// - Resolve customer acquisition (first non-return invoice) BEFORE assigning revenue.
    /// - Returns never acquire a customer; they only net revenue.
    /// - Without an acquiring sale (e.g. standalone credit note), the customer is never classified as "new".
    /// </summary>
    public async Task<CustomerAcquisitionAndLoyaltyReportDto> GetCustomerAcquisitionAndLoyaltyReportAsync(CustomerAcquisitionAndLoyaltyInputDto input)
    {
        var siQuery = await _siRepo.GetQueryableAsync();
        siQuery = siQuery.Where(si => si.CompanyId == input.CompanyId
            && (si.Status == DocumentStatus.Submitted || si.Status == DocumentStatus.Posted));

        if (input.CustomerId.HasValue)
        {
            siQuery = siQuery.Where(si => si.CustomerId == input.CustomerId.Value);
        }

        var invoices = siQuery.OrderBy(si => si.IssueDate).ThenBy(si => si.CreationTime).ToList();

        var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerTerritoryMap = new Dictionary<Guid, string>();
        if (customerIds.Count > 0)
        {
            var territoryRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Territory, Guid>>();
            var customers = (await _customerRepo.GetQueryableAsync())
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.TerritoryId })
                .ToList();

            var territoryIds = customers.Where(c => c.TerritoryId.HasValue).Select(c => c.TerritoryId!.Value).Distinct().ToList();
            var territoryNames = new Dictionary<Guid, string>();
            if (territoryIds.Count > 0)
            {
                territoryNames = (await territoryRepo.GetQueryableAsync())
                    .Where(t => territoryIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.Name })
                    .ToDictionary(t => t.Id, t => t.Name);
            }

            customerTerritoryMap = customers.ToDictionary(
                c => c.Id,
                c => c.TerritoryId.HasValue && territoryNames.TryGetValue(c.TerritoryId.Value, out var tName) ? tName : "Unassigned");
        }

        // 1. Resolve acquisition per customer
        var acquisition = new Dictionary<Guid, (string Key, DateTime Date)>();
        foreach (var si in invoices)
        {
            if (!si.IsReturn && !acquisition.ContainsKey(si.CustomerId))
            {
                var territory = customerTerritoryMap.GetValueOrDefault(si.CustomerId, "Unassigned");
                var key = input.TreeView ? territory : si.IssueDate.ToString("yyyy-MM");
                acquisition[si.CustomerId] = (key, si.IssueDate);
            }
        }

        // 2. Classify and aggregate within date range
        var newCustomerSets = new Dictionary<string, HashSet<Guid>>();
        var repeatCustomerSets = new Dictionary<string, HashSet<Guid>>();
        var newRevenueMap = new Dictionary<string, decimal>();
        var repeatRevenueMap = new Dictionary<string, decimal>();

        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;

        foreach (var si in invoices)
        {
            var issueDate = si.IssueDate.Date;
            if (issueDate < fromDate || issueDate > toDate) continue;

            var territory = customerTerritoryMap.GetValueOrDefault(si.CustomerId, "Unassigned");
            var key = input.TreeView ? territory : issueDate.ToString("yyyy-MM");

            var acq = acquisition.GetValueOrDefault(si.CustomerId);
            var isNew = acq != default && key == acq.Key && acq.Date.Date >= fromDate;

            var netAmount = si.IsReturn ? -Math.Abs(si.GrandTotal) : Math.Abs(si.GrandTotal);

            if (isNew)
            {
                if (!newCustomerSets.ContainsKey(key)) newCustomerSets[key] = new HashSet<Guid>();
                newCustomerSets[key].Add(si.CustomerId);
                newRevenueMap[key] = newRevenueMap.GetValueOrDefault(key, 0m) + netAmount;
            }
            else
            {
                if (!repeatCustomerSets.ContainsKey(key)) repeatCustomerSets[key] = new HashSet<Guid>();
                repeatCustomerSets[key].Add(si.CustomerId);
                repeatRevenueMap[key] = repeatRevenueMap.GetValueOrDefault(key, 0m) + netAmount;
            }
        }

        var allKeys = newCustomerSets.Keys.Union(repeatCustomerSets.Keys).OrderBy(k => k).ToList();
        var rows = new List<CustomerAcquisitionAndLoyaltyRowDto>();

        foreach (var key in allKeys)
        {
            var newCount = newCustomerSets.GetValueOrDefault(key)?.Count ?? 0;
            var newRev = Math.Round(newRevenueMap.GetValueOrDefault(key, 0m), 2);
            var repeatCount = repeatCustomerSets.GetValueOrDefault(key)?.Count ?? 0;
            var repeatRev = Math.Round(repeatRevenueMap.GetValueOrDefault(key, 0m), 2);

            rows.Add(new CustomerAcquisitionAndLoyaltyRowDto
            {
                Key = key,
                NewCustomerCount = newCount,
                NewCustomerRevenue = newRev,
                RepeatCustomerCount = repeatCount,
                RepeatCustomerRevenue = repeatRev
            });
        }

        var totalNewCustomers = newCustomerSets.Values.SelectMany(s => s).Distinct().Count();
        var totalRepeatCustomers = repeatCustomerSets.Values.SelectMany(s => s).Distinct().Count();
        var totalNewRev = Math.Round(rows.Sum(r => r.NewCustomerRevenue), 2);
        var totalRepeatRev = Math.Round(rows.Sum(r => r.RepeatCustomerRevenue), 2);

        return new CustomerAcquisitionAndLoyaltyReportDto
        {
            Rows = rows,
            TotalNewCustomers = totalNewCustomers,
            TotalRepeatCustomers = totalRepeatCustomers,
            TotalNewRevenue = totalNewRev,
            TotalRepeatRevenue = totalRepeatRev,
            TotalRevenue = totalNewRev + totalRepeatRev
        };
    }

    private static decimal CalculateGrowth(List<decimal> values)
    {
        if (values.Count < 2) return 0;
        var first = values.First();
        var last = values.Last();
        if (first == 0 && last > 0) return 100;
        if (first == 0 && last < 0) return -100;
        if (first == 0) return 0;
        return Math.Round((last - first) / first * 100, 1);
    }

    private static List<PeriodRange> GeneratePeriods(DateTime from, DateTime to, AnalyticsPeriodType periodType)
    {
        var periods = new List<PeriodRange>();
        var current = from;

        while (current <= to)
        {
            DateTime periodStart = current;
            DateTime periodEnd;
            string label;

            switch (periodType)
            {
                case AnalyticsPeriodType.Monthly:
                    periodEnd = new DateTime(current.Year, current.Month, DateTime.DaysInMonth(current.Year, current.Month));
                    label = current.ToString("MMM yyyy");
                    current = periodEnd.AddDays(1);
                    break;
                case AnalyticsPeriodType.Quarterly:
                    int quarter = ((current.Month - 1) / 3) + 1;
                    int endMonth = quarter * 3;
                    periodEnd = new DateTime(current.Year, endMonth, DateTime.DaysInMonth(current.Year, endMonth));
                    label = $"Q{quarter} {current.Year}";
                    current = periodEnd.AddDays(1);
                    break;
                default:
                    periodEnd = new DateTime(current.Year, 12, 31);
                    label = current.Year.ToString();
                    current = periodEnd.AddDays(1);
                    break;
            }

            if (periodEnd > to) periodEnd = to;
            periods.Add(new PeriodRange(Start: periodStart, End: periodEnd, Label: label));
        }

        return periods;
    }

    private record PeriodRange(DateTime Start, DateTime End, string Label)
    {
        public DateTime From => Start;
        public DateTime To => End;
    }
}
