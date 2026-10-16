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

        var isBillingDate = string.Equals(input.PeriodBasedOn, "BillingDate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.PeriodBasedOn, "bill_date", StringComparison.OrdinalIgnoreCase);

        var siQuery = await _siRepo.GetQueryableAsync();
        var allInvoices = siQuery
            .Where(si => si.CompanyId == input.CompanyId
                && si.Status == DocumentStatus.Posted
                && !si.IsReturn
                && !si.IsOpening)
            .ToList();

        // Effective date falls back to IssueDate (posting date) when bill date / FromDate is empty (ERPNext PR #60045 / commit 027e969735)
        var invoices = allInvoices
            .Where(si =>
            {
                var dt = isBillingDate ? (si.FromDate ?? si.IssueDate) : si.IssueDate;
                return dt >= input.FromDate && dt <= input.ToDate;
            })
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
                    g.ToList(), periods, useQty, isBillingDate);
                rows.Add(row);
            }
        }
        else if (input.GroupBy == AnalyticsGroupBy.Item)
        {
            var itemGroups = invoices
                .SelectMany(si =>
                {
                    var invoiceDate = isBillingDate ? (si.FromDate ?? si.IssueDate) : si.IssueDate;
                    return si.Items.Select(item => new
                    {
                        InvoiceDate = invoiceDate,
                        item.ItemId,
                        item.Quantity,
                        Amount = item.Quantity * item.UnitPrice
                    });
                })
                .GroupBy(x => x.ItemId);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.InvoiceDate >= period.From && x.InvoiceDate <= period.To);
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
                .SelectMany(si =>
                {
                    var invoiceDate = isBillingDate ? (si.FromDate ?? si.IssueDate) : si.IssueDate;
                    return si.Items.Select(item => new
                    {
                        InvoiceDate = invoiceDate,
                        ItemGroup = itemGroupMap.GetValueOrDefault(item.ItemId, "All Item Groups"),
                        item.Quantity,
                        Amount = item.Quantity * item.UnitPrice,
                    });
                })
                .GroupBy(x => x.ItemGroup);

            foreach (var g in itemGroups)
            {
                var periodValues = new List<decimal>();
                foreach (var period in periods)
                {
                    var periodItems = g.Where(x => x.InvoiceDate >= period.From && x.InvoiceDate <= period.To);
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
        else if (input.GroupBy == AnalyticsGroupBy.Project)
        {
            // Per ERPNext PR #60011 / commit a3eb5e6b09: exclude invoices without a project in project view
            var projectInvoices = invoices.Where(si => si.ProjectId.HasValue).ToList();
            var projectIds = projectInvoices.Select(si => si.ProjectId!.Value).Distinct().ToList();
            var projectNames = new Dictionary<Guid, string>();
            if (projectIds.Count > 0)
            {
                var projectRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Projects.Entities.Project, Guid>>();
                var projects = (await projectRepo.GetQueryableAsync())
                    .Where(p => projectIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.ProjectName })
                    .ToList();
                projectNames = projects.ToDictionary(p => p.Id, p => p.ProjectName);
            }

            var grouped = projectInvoices.GroupBy(si => si.ProjectId!.Value);
            foreach (var g in grouped)
            {
                var projectName = projectNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8));
                var row = BuildRow(g.Key.ToString(), projectName, g.ToList(), periods, useQty, isBillingDate);
                rows.Add(row);
            }
        }
        else
        {
            // Territory/SalesPerson: group by customer as fallback
            var grouped = invoices.GroupBy(si => si.CustomerId);
            foreach (var g in grouped)
            {
                var row = BuildRow(g.Key.ToString(), customerNames.GetValueOrDefault(g.Key, g.Key.ToString().Substring(0, 8)),
                    g.ToList(), periods, useQty, isBillingDate);
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
        List<PeriodRange> periods, bool useQty, bool isBillingDate)
    {
        var periodValues = new List<decimal>();
        foreach (var period in periods)
        {
            var periodInvoices = invoices.Where(si =>
            {
                var dt = isBillingDate ? (si.FromDate ?? si.IssueDate) : si.IssueDate;
                return dt >= period.From && dt <= period.To;
            });
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
    /// - Resolve customer acquisition (first non-return invoice) BEFORE assigning revenue,
    ///   so same-day returns are matched to acquisition period regardless of row order.
    /// - Returns never acquire a customer; they only net revenue.
    /// - Without an acquiring sale (e.g. standalone credit note), the customer is never "new".
    /// - Headcount: distinct customers per period are counted once; returns net revenue but not headcount.
    /// </summary>
    public async Task<CustomerAcquisitionAndLoyaltyReportDto> GetCustomerAcquisitionAndLoyaltyReportAsync(CustomerAcquisitionAndLoyaltyInputDto input)
    {
        var siQuery = await _siRepo.GetQueryableAsync();
        siQuery = siQuery.Where(si => si.CompanyId == input.CompanyId
            && (si.Status == DocumentStatus.Submitted || si.Status == DocumentStatus.Posted)
            && si.IssueDate <= input.ToDate);

        if (input.CustomerId.HasValue)
        {
            siQuery = siQuery.Where(si => si.CustomerId == input.CustomerId.Value);
        }

        var invoices = siQuery
            .OrderBy(si => si.IssueDate)
            .ThenBy(si => si.InvoiceNumber)
            .ThenBy(si => si.CreationTime)
            .ToList();

        var customerIds = invoices.Select(si => si.CustomerId).Distinct().ToList();
        var customerTerritoryMap = new Dictionary<Guid, string>();
        var territoryRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Territory, Guid>>();
        var allTerritories = (await territoryRepo.GetQueryableAsync()).ToList();

        if (customerIds.Count > 0)
        {
            var customers = (await _customerRepo.GetQueryableAsync())
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.TerritoryId })
                .ToList();

            var territoryNames = allTerritories.ToDictionary(t => t.Id, t => t.Name);

            customerTerritoryMap = customers.ToDictionary(
                c => c.Id,
                c => c.TerritoryId.HasValue && territoryNames.TryGetValue(c.TerritoryId.Value, out var tName) ? tName : "Unassigned");
        }

        // Territory filter scoping (PR #60697 / ERPNext filters)
        if (input.TerritoryId.HasValue)
        {
            var targetTerritoryId = input.TerritoryId.Value;
            var matchingTerritoryIds = new HashSet<Guid> { targetTerritoryId };
            bool addedAny;
            do
            {
                addedAny = false;
                foreach (var t in allTerritories)
                {
                    if (t.ParentId.HasValue && matchingTerritoryIds.Contains(t.ParentId.Value) && matchingTerritoryIds.Add(t.Id))
                    {
                        addedAny = true;
                    }
                }
            } while (addedAny);

            var customersInTerritories = (await _customerRepo.GetQueryableAsync())
                .Where(c => c.TerritoryId.HasValue && matchingTerritoryIds.Contains(c.TerritoryId.Value))
                .Select(c => c.Id)
                .ToHashSet();

            invoices = invoices.Where(si => customersInTerritories.Contains(si.CustomerId)).ToList();
        }

        // 1. Resolve acquisition per customer before assigning revenue
        var acquisition = new Dictionary<Guid, (string Key, DateTime Date)>();
        foreach (var si in invoices)
        {
            if (!si.IsReturn && !acquisition.ContainsKey(si.CustomerId))
            {
                var territory = customerTerritoryMap.GetValueOrDefault(si.CustomerId, "Unassigned");
                var key = input.TreeView ? territory : si.IssueDate.ToString("yyyy-MM");
                acquisition[si.CustomerId] = (key, si.IssueDate.Date);
            }
        }

        // 2. Classify and aggregate within date range
        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;

        var counted = new Dictionary<string, (HashSet<Guid> NewCustomers, HashSet<Guid> RepeatCustomers)>();
        var revenue = new Dictionary<string, (decimal NewRevenue, decimal RepeatRevenue)>();

        foreach (var si in invoices)
        {
            var postingDate = si.IssueDate.Date;
            if (postingDate < fromDate)
                continue;

            var territory = customerTerritoryMap.GetValueOrDefault(si.CustomerId, "Unassigned");
            var key = input.TreeView ? territory : postingDate.ToString("yyyy-MM");

            var acq = acquisition.GetValueOrDefault(si.CustomerId);
            // without an acquiring sale (e.g. a standalone credit note), the customer is never "new"
            var isNew = acq != default && key == acq.Key && acq.Date >= fromDate;

            if (!counted.ContainsKey(key))
            {
                counted[key] = (new HashSet<Guid>(), new HashSet<Guid>());
                revenue[key] = (0m, 0m);
            }

            var currentCounted = counted[key];
            var currentRevenue = revenue[key];

            // count each customer once per period; returns net revenue but not the headcount
            if (!si.IsReturn)
            {
                if (isNew)
                {
                    currentCounted.NewCustomers.Add(si.CustomerId);
                }
                else
                {
                    currentCounted.RepeatCustomers.Add(si.CustomerId);
                }
            }

            var invoiceAmount = si.BaseGrandTotal != 0
                ? si.BaseGrandTotal
                : (si.GrandTotal * (si.ExchangeRate > 0 ? si.ExchangeRate : 1m));
            var netAmount = si.IsReturn ? -Math.Abs(invoiceAmount) : Math.Abs(invoiceAmount);

            if (isNew)
            {
                revenue[key] = (currentRevenue.NewRevenue + netAmount, currentRevenue.RepeatRevenue);
            }
            else
            {
                revenue[key] = (currentRevenue.NewRevenue, currentRevenue.RepeatRevenue + netAmount);
            }
        }

        var rows = new List<CustomerAcquisitionAndLoyaltyRowDto>();

        if (input.TreeView)
        {
            if (allTerritories.Count > 0)
            {
                var territoryById = allTerritories.ToDictionary(t => t.Id);

                int GetDepth(Territory t)
                {
                    int depth = 0;
                    var curr = t;
                    while (curr.ParentId.HasValue && territoryById.TryGetValue(curr.ParentId.Value, out var parent))
                    {
                        depth++;
                        curr = parent;
                    }
                    return depth;
                }

                var territoryData = allTerritories.ToDictionary(t => t.Id, t =>
                {
                    var c = counted.GetValueOrDefault(t.Name);
                    var r = revenue.GetValueOrDefault(t.Name);
                    return new TerritoryAgg
                    {
                        Territory = t,
                        Depth = GetDepth(t),
                        NewCustomers = c.NewCustomers != null ? new HashSet<Guid>(c.NewCustomers) : new HashSet<Guid>(),
                        RepeatCustomers = c.RepeatCustomers != null ? new HashSet<Guid>(c.RepeatCustomers) : new HashSet<Guid>(),
                        NewRevenue = r.NewRevenue,
                        RepeatRevenue = r.RepeatRevenue
                    };
                });

                // Roll up from leaves (deepest depth) to root
                var orderedByDepthDesc = territoryData.Values.OrderByDescending(x => x.Depth).ToList();
                foreach (var item in orderedByDepthDesc)
                {
                    if (item.Territory.ParentId.HasValue && territoryData.TryGetValue(item.Territory.ParentId.Value, out var parentItem))
                    {
                        foreach (var custId in item.NewCustomers) parentItem.NewCustomers.Add(custId);
                        foreach (var custId in item.RepeatCustomers) parentItem.RepeatCustomers.Add(custId);
                        parentItem.NewRevenue += item.NewRevenue;
                        parentItem.RepeatRevenue += item.RepeatRevenue;
                    }
                }

                var orderedHierarchy = territoryData.Values
                    .OrderBy(x => x.Depth)
                    .ThenBy(x => x.Territory.Name)
                    .ToList();

                foreach (var item in orderedHierarchy)
                {
                    rows.Add(new CustomerAcquisitionAndLoyaltyRowDto
                    {
                        Key = item.Territory.Name,
                        NewCustomerCount = item.NewCustomers.Count,
                        NewCustomerRevenue = Math.Round(item.NewRevenue, 2),
                        RepeatCustomerCount = item.RepeatCustomers.Count,
                        RepeatCustomerRevenue = Math.Round(item.RepeatRevenue, 2)
                    });
                }

                if (counted.ContainsKey("Unassigned") || revenue.ContainsKey("Unassigned"))
                {
                    var c = counted.GetValueOrDefault("Unassigned");
                    var r = revenue.GetValueOrDefault("Unassigned");
                    rows.Add(new CustomerAcquisitionAndLoyaltyRowDto
                    {
                        Key = "Unassigned",
                        NewCustomerCount = c.NewCustomers?.Count ?? 0,
                        NewCustomerRevenue = Math.Round(r.NewRevenue, 2),
                        RepeatCustomerCount = c.RepeatCustomers?.Count ?? 0,
                        RepeatCustomerRevenue = Math.Round(r.RepeatRevenue, 2)
                    });
                }
            }
            else
            {
                var allKeys = counted.Keys.Union(revenue.Keys).OrderBy(k => k).ToList();
                foreach (var key in allKeys)
                {
                    var c = counted.GetValueOrDefault(key);
                    var r = revenue.GetValueOrDefault(key);
                    rows.Add(new CustomerAcquisitionAndLoyaltyRowDto
                    {
                        Key = key,
                        NewCustomerCount = c.NewCustomers?.Count ?? 0,
                        NewCustomerRevenue = Math.Round(r.NewRevenue, 2),
                        RepeatCustomerCount = c.RepeatCustomers?.Count ?? 0,
                        RepeatCustomerRevenue = Math.Round(r.RepeatRevenue, 2)
                    });
                }
            }
        }
        else
        {
            // Monthly time series: generate every month in range
            var currentMonth = new DateTime(fromDate.Year, fromDate.Month, 1);
            var endMonth = new DateTime(toDate.Year, toDate.Month, 1);

            while (currentMonth <= endMonth)
            {
                var key = currentMonth.ToString("yyyy-MM");
                var c = counted.GetValueOrDefault(key);
                var r = revenue.GetValueOrDefault(key);

                rows.Add(new CustomerAcquisitionAndLoyaltyRowDto
                {
                    Key = key,
                    NewCustomerCount = c.NewCustomers?.Count ?? 0,
                    NewCustomerRevenue = Math.Round(r.NewRevenue, 2),
                    RepeatCustomerCount = c.RepeatCustomers?.Count ?? 0,
                    RepeatCustomerRevenue = Math.Round(r.RepeatRevenue, 2)
                });

                currentMonth = currentMonth.AddMonths(1);
            }
        }

        var totalNewCustomers = counted.Values.SelectMany(c => c.NewCustomers).Distinct().Count();
        var totalRepeatCustomers = counted.Values.SelectMany(c => c.RepeatCustomers).Distinct().Count();
        var totalNewRev = Math.Round(revenue.Values.Sum(r => r.NewRevenue), 2);
        var totalRepeatRev = Math.Round(revenue.Values.Sum(r => r.RepeatRevenue), 2);

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

    private class TerritoryAgg
    {
        public Territory Territory { get; set; } = null!;
        public int Depth { get; set; }
        public HashSet<Guid> NewCustomers { get; set; } = new();
        public HashSet<Guid> RepeatCustomers { get; set; } = new();
        public decimal NewRevenue { get; set; }
        public decimal RepeatRevenue { get; set; }
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

    /// <summary>
    /// Available Stock for Packing Items report — calculates available packable whole bundles per warehouse.
    /// Per ERPNext PR #60058 / commits bbd6f880a7 & 8d9300a549:
    /// - Only active, non-disabled Product Bundles are included.
    /// - Packable bundles = min(floor(round(projected_qty / component_qty, 9))) across all bundle components.
    /// - Genuine fractions are floored (e.g. 13/2 = 6, 8.99/3 = 2).
    /// - Float division noise is eliminated (e.g. 0.3 / 0.1 evaluates to 3, not 2).
    /// - Negative or zero projected stock cannot yield any bundles (dropped).
    /// - Scoped by company and warehouse filters.
    /// </summary>
    public async Task<List<AvailableStockForPackingItemDto>> GetAvailableStockForPackingItemsReportAsync(AvailableStockForPackingItemsInputDto input)
    {
        var bundleRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ProductBundle, Guid>>();
        var bundleQuery = (await bundleRepo.WithDetailsAsync(b => b.Items)).AsQueryable();

        bundleQuery = bundleQuery.Where(b => b.IsActive && !b.IsDisabled && b.Items.Any());

        if (input.BundleItemId.HasValue)
        {
            bundleQuery = bundleQuery.Where(b => b.ItemId == input.BundleItemId.Value);
        }

        var bundles = bundleQuery.ToList();
        if (bundles.Count == 0)
        {
            return new List<AvailableStockForPackingItemDto>();
        }

        var allComponentIds = bundles.SelectMany(b => b.Items.Select(i => i.ComponentItemId)).Distinct().ToList();

        var binRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Bin, Guid>>();
        var binQuery = await binRepo.GetQueryableAsync();

        binQuery = binQuery.Where(b => allComponentIds.Contains(b.ItemId));

        if (input.WarehouseId.HasValue)
        {
            binQuery = binQuery.Where(b => b.WarehouseId == input.WarehouseId.Value);
        }

        var bins = binQuery.ToList();

        // Scope by company warehouse if company is provided
        var whRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();
        var whQuery = await whRepo.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }

        var validWarehouses = whQuery.Select(w => new { w.Id, w.Name }).ToDictionary(w => w.Id, w => w.Name);

        // Filter bins to only allowed warehouses and compute projected_qty
        var binProjected = bins
            .Where(b => validWarehouses.ContainsKey(b.WarehouseId))
            .ToDictionary(b => (b.ItemId, b.WarehouseId), b => b.ProjectedQty);

        var candidateWarehouses = binProjected.Keys.Select(k => k.WarehouseId).Distinct().ToList();

        // Resolve bundle item codes and names
        var bundleItemIds = bundles.Select(b => b.ItemId).Distinct().ToList();
        var itemEntities = (await _itemRepo.GetQueryableAsync())
            .Where(i => bundleItemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Description, i.Uom })
            .ToList()
            .ToDictionary(i => i.Id);

        var result = new List<AvailableStockForPackingItemDto>();

        foreach (var bundle in bundles)
        {
            itemEntities.TryGetValue(bundle.ItemId, out var item);
            var itemCode = item?.ItemCode ?? bundle.ItemName ?? bundle.ItemId.ToString()[..8];
            var itemName = item?.ItemName ?? bundle.ItemName ?? itemCode;
            var description = item?.Description ?? bundle.Description;

            foreach (var whId in candidateWarehouses)
            {
                decimal minPackable = decimal.MaxValue;
                bool hasStockForAny = false;

                foreach (var component in bundle.Items)
                {
                    if (component.Qty <= 0) continue;

                    var projected = binProjected.GetValueOrDefault((component.ComponentItemId, whId), 0m);
                    if (projected <= 0)
                    {
                        minPackable = 0m;
                        break;
                    }

                    hasStockForAny = true;
                    var possible = projected / component.Qty;
                    if (possible < minPackable)
                    {
                        minPackable = possible;
                    }
                }

                if (!hasStockForAny || minPackable <= 0 || minPackable == decimal.MaxValue)
                {
                    continue;
                }

                // Strip float-division noise (round 9 places), then floor: only whole bundles can be packed
                var cleaned = Math.Round(minPackable, 9);
                var wholeBundles = Math.Max(0m, Math.Floor(cleaned));

                if (wholeBundles > 0)
                {
                    result.Add(new AvailableStockForPackingItemDto
                    {
                        BundleItemId = bundle.ItemId,
                        BundleItemCode = itemCode,
                        BundleItemName = itemName,
                        Description = description,
                        StockUom = item?.Uom ?? "Unit",
                        WarehouseId = whId,
                        WarehouseName = validWarehouses.GetValueOrDefault(whId, "—"),
                        AvailablePackableBundles = wholeBundles
                    });
                }
            }
        }

        return result
            .OrderBy(r => r.BundleItemName)
            .ThenBy(r => r.WarehouseName)
            .ToList();
    }
}
