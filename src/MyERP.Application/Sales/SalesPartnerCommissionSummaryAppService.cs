using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

/// <summary>
/// Sales Partner Commission Summary and Sales Partner Transaction Summary reports.
/// Per ERPNext selling/report/sales_partner_commission_summary and sales_partner_transaction_summary.
/// </summary>
[Authorize(MyERPPermissions.SalesOrders.Default)]
public class SalesPartnerCommissionSummaryAppService : ApplicationService, ISalesPartnerCommissionSummaryAppService
{
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo;
    private readonly IRepository<SalesPartner, Guid> _salesPartnerRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Territory, Guid> _territoryRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo;

    public SalesPartnerCommissionSummaryAppService(
        IRepository<SalesOrder, Guid> salesOrderRepo,
        IRepository<SalesInvoice, Guid> salesInvoiceRepo,
        IRepository<SalesPartner, Guid> salesPartnerRepo,
        IRepository<Customer, Guid> customerRepo,
        IRepository<Territory, Guid> territoryRepo,
        IRepository<Company, Guid> companyRepo,
        IRepository<Item, Guid> itemRepo,
        IRepository<ItemGroup, Guid> itemGroupRepo)
    {
        _salesOrderRepo = salesOrderRepo;
        _salesInvoiceRepo = salesInvoiceRepo;
        _salesPartnerRepo = salesPartnerRepo;
        _customerRepo = customerRepo;
        _territoryRepo = territoryRepo;
        _companyRepo = companyRepo;
        _itemRepo = itemRepo;
        _itemGroupRepo = itemGroupRepo;
    }

    public async Task<SalesPartnerCommissionSummaryReportDto> GetReportAsync(GetSalesPartnerCommissionSummaryInput input)
    {
        var context = await LoadPartnerDocsAsync(input, includeItems: false);

        var rows = context.Documents
            .Select(doc => new SalesPartnerCommissionSummaryRowDto
            {
                TransactionId = doc.Id,
                TransactionNumber = doc.Number,
                DocType = context.NormalizedDocType,
                PostingDate = doc.PostingDate,
                CustomerId = doc.CustomerId,
                CustomerName = doc.CustomerName,
                TerritoryName = doc.TerritoryName,
                SalesPartnerId = doc.SalesPartnerId,
                SalesPartnerName = doc.SalesPartnerName,
                CommissionRate = doc.CommissionRate,
                Amount = Math.Round(doc.BaseNetAmount, 2),
                TotalCommission = Math.Round(doc.TotalCommission, 2),
            })
            .OrderByDescending(r => r.TransactionNumber)
            .ThenBy(r => r.SalesPartnerName)
            .ToList();

        return new SalesPartnerCommissionSummaryReportDto
        {
            Currency = context.Currency,
            DocType = context.NormalizedDocType,
            Rows = rows,
            TotalAmount = Math.Round(rows.Sum(r => r.Amount), 2),
            TotalCommission = Math.Round(rows.Sum(r => r.TotalCommission), 2),
            TransactionCount = rows.Count,
        };
    }

    public async Task<SalesPartnerTransactionSummaryReportDto> GetTransactionSummaryAsync(GetSalesPartnerCommissionSummaryInput input)
    {
        var context = await LoadPartnerDocsAsync(input, includeItems: true);

        if (context.Documents.Count == 0)
        {
            return new SalesPartnerTransactionSummaryReportDto
            {
                Currency = context.Currency,
                DocType = context.NormalizedDocType,
            };
        }

        var itemIds = context.Documents.SelectMany(d => d.Items.Select(i => i.ItemId)).Distinct().ToList();
        var itemMap = itemIds.Count > 0
            ? (await _itemRepo.GetListAsync(i => itemIds.Contains(i.Id))).ToDictionary(i => i.Id)
            : new Dictionary<Guid, Item>();

        var itemGroups = await _itemGroupRepo.GetListAsync();
        var itemGroupNames = itemGroups.ToDictionary(g => g.Id, g => g.Name);
        HashSet<Guid>? allowedGroupIds = null;
        if (input.ItemGroupId.HasValue)
        {
            allowedGroupIds = CollectDescendantItemGroupIds(input.ItemGroupId.Value, itemGroups);
        }

        var brandFilter = input.Brand?.Trim();
        var rows = new List<SalesPartnerTransactionSummaryRowDto>();

        foreach (var doc in context.Documents.OrderByDescending(d => d.Number).ThenBy(d => d.SalesPartnerName))
        {
            foreach (var line in doc.Items)
            {
                if (!input.ShowReturnEntries && line.Quantity <= 0m)
                    continue;

                itemMap.TryGetValue(line.ItemId, out var item);

                if (allowedGroupIds != null && (!item?.ItemGroupId.HasValue == true || !allowedGroupIds.Contains(item!.ItemGroupId!.Value)))
                    continue;

                if (!string.IsNullOrWhiteSpace(brandFilter) &&
                    !string.Equals(item?.Brand, brandFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? itemGroupName = item?.ItemGroup;
                if (string.IsNullOrWhiteSpace(itemGroupName) && item?.ItemGroupId.HasValue == true)
                {
                    itemGroupNames.TryGetValue(item.ItemGroupId.Value, out itemGroupName);
                }

                var rate = Math.Round(line.UnitPrice * doc.ExchangeRate, 2);
                var amount = Math.Round(line.LineTotal * doc.ExchangeRate, 2);
                var grantCommission = item?.GrantCommission ?? true;
                var commission = grantCommission
                    ? Math.Round(amount * doc.CommissionRate / 100m, 2)
                    : 0m;

                rows.Add(new SalesPartnerTransactionSummaryRowDto
                {
                    TransactionId = doc.Id,
                    TransactionNumber = doc.Number,
                    DocType = context.NormalizedDocType,
                    PostingDate = doc.PostingDate,
                    CustomerId = doc.CustomerId,
                    CustomerName = doc.CustomerName,
                    TerritoryName = doc.TerritoryName,
                    ItemId = line.ItemId,
                    ItemCode = item?.ItemCode ?? line.ItemId.ToString()[..8],
                    ItemName = item?.ItemName ?? line.Description,
                    ItemGroupName = itemGroupName,
                    Brand = item?.Brand,
                    Quantity = line.Quantity,
                    Rate = rate,
                    Amount = amount,
                    SalesPartnerId = doc.SalesPartnerId,
                    SalesPartnerName = doc.SalesPartnerName,
                    CommissionRate = doc.CommissionRate,
                    Commission = commission,
                });
            }
        }

        return new SalesPartnerTransactionSummaryReportDto
        {
            Currency = context.Currency,
            DocType = context.NormalizedDocType,
            Rows = rows,
            TotalQty = rows.Sum(r => r.Quantity),
            TotalAmount = Math.Round(rows.Sum(r => r.Amount), 2),
            TotalCommission = Math.Round(rows.Sum(r => r.Commission), 2),
        };
    }

    private async Task<PartnerQueryContext> LoadPartnerDocsAsync(
        GetSalesPartnerCommissionSummaryInput input,
        bool includeItems)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var rawDocType = input.DocType?.Trim() ?? "Sales Invoice";
        string normalizedDocType;
        if (string.Equals(rawDocType, "Sales Order", StringComparison.OrdinalIgnoreCase))
            normalizedDocType = "Sales Order";
        else if (string.Equals(rawDocType, "Sales Invoice", StringComparison.OrdinalIgnoreCase))
            normalizedDocType = "Sales Invoice";
        else if (string.Equals(rawDocType, "POS Invoice", StringComparison.OrdinalIgnoreCase))
            normalizedDocType = "POS Invoice";
        else
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Unsupported document type '{rawDocType}'. Must be Sales Order, Sales Invoice, or POS Invoice.");

        var company = await _companyRepo.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var customers = (await _customerRepo.GetListAsync(c => c.CompanyId == input.CompanyId))
            .ToDictionary(c => c.Id);
        var territories = (await _territoryRepo.GetListAsync())
            .ToDictionary(t => t.Id, t => t.Name);
        var partners = (await _salesPartnerRepo.GetListAsync())
            .ToDictionary(p => p.Id, p => p.Name);

        var fromDate = input.FromDate.HasValue ? DateRangeHelper.StartOfDay(input.FromDate.Value) : (DateTime?)null;
        var toDateExclusive = input.ToDate.HasValue ? DateRangeHelper.ExclusiveNextDay(input.ToDate.Value) : (DateTime?)null;

        var docs = new List<PartnerDocumentProjection>();

        if (normalizedDocType == "Sales Order")
        {
            var soQuery = includeItems
                ? await _salesOrderRepo.WithDetailsAsync(so => so.Items)
                : await _salesOrderRepo.GetQueryableAsync();

            var orders = soQuery
                .Where(so => so.CompanyId == input.CompanyId
                    && so.SalesPartnerId.HasValue
                    && so.Status != DocumentStatus.Draft
                    && so.Status != DocumentStatus.Cancelled
                    && (!input.SalesPartnerId.HasValue || so.SalesPartnerId == input.SalesPartnerId.Value)
                    && (!input.CustomerId.HasValue || so.CustomerId == input.CustomerId.Value)
                    && (!fromDate.HasValue || so.OrderDate >= fromDate.Value)
                    && (!toDateExclusive.HasValue || so.OrderDate < toDateExclusive.Value))
                .ToList();

            foreach (var so in orders)
            {
                customers.TryGetValue(so.CustomerId, out var cust);
                if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                    continue;

                string? terrName = null;
                if (cust?.TerritoryId.HasValue == true)
                    territories.TryGetValue(cust.TerritoryId.Value, out terrName);

                var spId = so.SalesPartnerId!.Value;
                partners.TryGetValue(spId, out var spName);

                var baseNet = so.NetTotal * so.ExchangeRate;
                var totalComm = so.TotalCommission != 0m ? so.TotalCommission * so.ExchangeRate : Math.Round(baseNet * so.CommissionRate / 100m, 2);

                docs.Add(new PartnerDocumentProjection(
                    so.Id,
                    so.OrderNumber,
                    so.OrderDate,
                    so.CustomerId,
                    cust?.Name ?? so.CustomerId.ToString()[..8],
                    terrName,
                    spId,
                    spName ?? spId.ToString()[..8],
                    so.CommissionRate,
                    so.ExchangeRate,
                    baseNet,
                    totalComm,
                    includeItems
                        ? so.Items.Select(i => new PartnerLineProjection(i.ItemId, i.Description, i.Quantity, i.UnitPrice, i.LineTotal)).ToList()
                        : Array.Empty<PartnerLineProjection>()));
            }
        }
        else
        {
            var onlyPos = normalizedDocType == "POS Invoice";
            var siQuery = includeItems
                ? await _salesInvoiceRepo.WithDetailsAsync(si => si.Items)
                : await _salesInvoiceRepo.GetQueryableAsync();

            var invoices = siQuery
                .Where(si => si.CompanyId == input.CompanyId
                    && si.SalesPartnerId.HasValue
                    && si.Status == DocumentStatus.Posted
                    && (!onlyPos || si.IsPos)
                    && (!input.SalesPartnerId.HasValue || si.SalesPartnerId == input.SalesPartnerId.Value)
                    && (!input.CustomerId.HasValue || si.CustomerId == input.CustomerId.Value)
                    && (!fromDate.HasValue || si.IssueDate >= fromDate.Value)
                    && (!toDateExclusive.HasValue || si.IssueDate < toDateExclusive.Value))
                .ToList();

            foreach (var si in invoices)
            {
                customers.TryGetValue(si.CustomerId, out var cust);
                if (input.TerritoryId.HasValue && cust?.TerritoryId != input.TerritoryId.Value)
                    continue;

                string? terrName = null;
                if (cust?.TerritoryId.HasValue == true)
                    territories.TryGetValue(cust.TerritoryId.Value, out terrName);

                var spId = si.SalesPartnerId!.Value;
                partners.TryGetValue(spId, out var spName);

                var baseNet = si.BaseNetTotal != 0m ? si.BaseNetTotal : si.NetTotal * si.ExchangeRate;
                var totalComm = si.TotalCommission != 0m ? si.TotalCommission : Math.Round(baseNet * si.CommissionRate / 100m, 2);

                docs.Add(new PartnerDocumentProjection(
                    si.Id,
                    si.InvoiceNumber,
                    si.IssueDate,
                    si.CustomerId,
                    cust?.Name ?? si.CustomerId.ToString()[..8],
                    terrName,
                    spId,
                    spName ?? spId.ToString()[..8],
                    si.CommissionRate,
                    si.ExchangeRate,
                    baseNet,
                    totalComm,
                    includeItems
                        ? si.Items.Select(i => new PartnerLineProjection(i.ItemId, i.Description, i.Quantity, i.UnitPrice, i.LineTotal)).ToList()
                        : Array.Empty<PartnerLineProjection>()));
            }
        }

        return new PartnerQueryContext(currency, normalizedDocType, docs);
    }

    private static HashSet<Guid> CollectDescendantItemGroupIds(Guid rootId, List<ItemGroup> allGroups)
    {
        var result = new HashSet<Guid> { rootId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in allGroups.Where(g => g.ParentId == current))
            {
                if (result.Add(child.Id))
                    queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private sealed record PartnerLineProjection(
        Guid ItemId,
        string Description,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    private sealed record PartnerDocumentProjection(
        Guid Id,
        string Number,
        DateTime PostingDate,
        Guid CustomerId,
        string CustomerName,
        string? TerritoryName,
        Guid SalesPartnerId,
        string SalesPartnerName,
        decimal CommissionRate,
        decimal ExchangeRate,
        decimal BaseNetAmount,
        decimal TotalCommission,
        IReadOnlyList<PartnerLineProjection> Items);

    private sealed record PartnerQueryContext(
        string Currency,
        string NormalizedDocType,
        List<PartnerDocumentProjection> Documents);
}
