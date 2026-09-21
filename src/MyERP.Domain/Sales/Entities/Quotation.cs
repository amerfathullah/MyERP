using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Quotation — sales proposal to customer.
/// Maps to ERPNext selling/doctype/quotation.
/// Flow: Quotation → SalesOrder → SalesInvoice
/// </summary>
public class Quotation : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string QuotationNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public Guid CustomerId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Selling Price List — defaults from Customer.DefaultPriceListId, overridable per quotation.</summary>
    public Guid? PriceListId { get; set; }
    public bool HasUnitPriceItems { get; set; }

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Reference to converted SalesOrder (if converted).</summary>
    public Guid? ConvertedToSalesOrderId { get; set; }

    /// <summary>Source Opportunity that this Quotation was created from (if any).</summary>
    public Guid? OpportunityId { get; set; }

    private readonly List<QuotationItem> _items = new();
    /// <summary>Rows in document order (Idx), since the database gives no ordering guarantee.</summary>
    public IReadOnlyList<QuotationItem> Items => _items.OrderBy(i => i.Idx).ThenBy(i => i.CreationTime).ToList();

    /// <summary>SO conversion completion %. MIN% formula per ERPNext StatusUpdater (PR #58603: compares in stock UOM).</summary>
    public decimal PerOrdered
    {
        get
        {
            // ERPNext get_valid_items: rows in an alternatives set count only once they were ordered.
            var valid = _items.Where(i => !(i.IsAlternative || i.HasAlternativeItem) || i.OrderedQty > 0).ToList();
            if (!valid.Any()) return 0;
            return valid.Min(i => i.StockQty == 0 ? 100 : Math.Min(100, i.OrderedQty / i.StockQty * 100));
        }
    }

    /// <summary>Dynamic order status matching ERPNext get_ordered_status(): Open, Partially Ordered, Ordered.</summary>
    public string OrderStatus
    {
        get
        {
            if (Status != DocumentStatus.Submitted)
                return Status == DocumentStatus.Rejected ? "Lost" : Status.ToString();

            if (PerOrdered >= 100m)
                return "Ordered";
            if (PerOrdered > 0m)
                return "Partially Ordered";
            return "Open";
        }
    }

    /// <summary>True when all items have been converted to Sales Orders (100%).</summary>
    public bool IsFullyOrdered => OrderStatus == "Ordered";

    /// <summary>True when some items have been converted to Sales Orders (0% &lt; PerOrdered &lt; 100%).</summary>
    public bool IsPartiallyOrdered => OrderStatus == "Partially Ordered";

    protected Quotation() { }

    public Quotation(Guid id, Guid companyId, Guid customerId, string quotationNumber, DateTime issueDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        CustomerId = Check.NotDefaultOrNull<Guid>(customerId, nameof(customerId));
        QuotationNumber = Check.NotNullOrWhiteSpace(quotationNumber, nameof(quotationNumber), QuotationConsts.MaxQuotationNumberLength);
        IssueDate = issueDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit", bool isAlternative = false)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new QuotationItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom) { IsAlternative = isAlternative, Idx = _items.Count });
        RecalculateTotals();
    }

    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        MarkRowsWithAlternatives();

        // Auto-correct conversion factor when UOM equals StockUOM (gotcha #6171)
        foreach (var item in _items)
        {
            if (!string.IsNullOrEmpty(item.Uom) && !string.IsNullOrEmpty(item.StockUom)
                && string.Equals(item.Uom, item.StockUom, StringComparison.OrdinalIgnoreCase)
                && item.ConversionFactor != 1.0m)
            {
                item.ConversionFactor = 1.0m;
            }
        }

        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Whether the quotation has passed its validity date.
    /// Per ERPNext: expired quotations cannot be converted to SO (unless settings allow).
    /// </summary>
    public bool IsExpired => ValidUntil.HasValue && DateTime.UtcNow.Date > ValidUntil.Value.Date
        && Status == DocumentStatus.Submitted && ConvertedToSalesOrderId == null;

    /// <summary>
    /// Mark quotation as lost (customer declined / competitor won).
    /// Per gotcha #2142: Blocked if sales order has already been made.
    /// </summary>
    public void MarkLost()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (ConvertedToSalesOrderId.HasValue || _items.Any(i => i.OrderedQty > 0))
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot set as Lost as Sales Order is made.");

        Status = DocumentStatus.Rejected; // Rejected = Lost in quotation context
    }

    /// <summary>
    /// ERPNext set_has_alternative_item: a non-alternative row directly followed by alternative
    /// row(s) is flagged as having an alternative.
    /// </summary>
    private void MarkRowsWithAlternatives()
    {
        var ordered = Items;
        for (var idx = 0; idx < ordered.Count - 1; idx++)
        {
            if (!ordered[idx].IsAlternative && ordered[idx + 1].IsAlternative)
                ordered[idx].HasAlternativeItem = true;
        }
    }

    private void RecalculateTotals()
    {
        // ERPNext taxes_and_totals: alternative rows are offers, not part of the document total.
        var counted = _items.Where(i => !i.IsAlternative).ToList();
        NetTotal = counted.Sum(i => i.LineTotal);
        TaxAmount = counted.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
        HasUnitPriceItems = _items.Any(i => i.Quantity == 0);
    }
}

public class QuotationItem : CreationAuditedEntity<Guid>
{
    public Guid QuotationId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Rate per stock UOM = UnitPrice / ConversionFactor (gotcha #198).</summary>
    public decimal StockUomRate => ConversionFactor > 0 ? Math.Round(UnitPrice / ConversionFactor, 4) : UnitPrice;

    /// <summary>Qty converted to Sales Order in stock UOM (PR #58603). Tracked by document conversion.</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Zero-based row position within the quotation (ERPNext idx).</summary>
    public int Idx { get; set; }

    /// <summary>Alternative offer for the preceding row; excluded from totals (ERPNext is_alternative).</summary>
    public bool IsAlternative { get; set; }

    /// <summary>This row is followed by at least one alternative row (ERPNext has_alternative_item).</summary>
    public bool HasAlternativeItem { get; set; }

    /// <summary>Remaining quantity to order in stock UOM (per ERPNext PR #58603).</summary>
    public decimal PendingOrderQty => Math.Max(0, StockQty - OrderedQty);

    protected QuotationItem() { }
    public QuotationItem(Guid id, Guid quotationId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        QuotationId = quotationId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
