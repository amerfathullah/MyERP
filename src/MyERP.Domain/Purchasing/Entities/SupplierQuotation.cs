using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Supplier Quotation — supplier's response to an RFQ with quoted rates.
/// Used in the procurement cycle: RFQ → SQ → PO.
/// Maps to ERPNext buying/doctype/supplier_quotation.
/// </summary>
public class SupplierQuotation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }

    public string? QuotationNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }

    /// <summary>Currency of the quotation.</summary>
    public string Currency { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1;

    /// <summary>Link to Request for Quotation (if created from RFQ).</summary>
    public Guid? RequestForQuotationId { get; set; }

    public decimal NetTotal { get; private set; }
    public decimal GrandTotal { get; private set; }
    public bool HasUnitPriceItems { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public string? Notes { get; set; }

    private readonly List<SupplierQuotationItem> _items = new();
    public IReadOnlyList<SupplierQuotationItem> Items => _items.AsReadOnly();

    protected SupplierQuotation() { }

    public SupplierQuotation(Guid id, Guid companyId, Guid supplierId,
        DateTime transactionDate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        TransactionDate = transactionDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, decimal qty, decimal rate, string? itemName = null, string? uom = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new SupplierQuotationItem(Guid.NewGuid(), Id, itemId, qty, rate, itemName, uom));
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

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
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (_items.Any(i => i.OrderedQty > 0))
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("reason", "Cannot cancel Supplier Quotation with active purchase orders. Cancel linked purchase orders first.");
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Gets human-readable order status matching ERPNext status updater:
    /// "Not Ordered" (Submitted with 0 ordered), "Partially Ordered", "Ordered".
    /// </summary>
    public string OrderStatus
    {
        get
        {
            if (Status == DocumentStatus.Draft) return "Draft";
            if (Status == DocumentStatus.Cancelled) return "Cancelled";
            if (_items.Count > 0 && _items.All(i => i.StockQty <= 0 || i.OrderedQty >= i.StockQty))
                return "Ordered";
            if (_items.Any(i => i.OrderedQty > 0))
                return "Partially Ordered";
            return "Not Ordered";
        }
    }

    /// <summary>Updates ordered quantity for a child item and recalculates quotation order status.</summary>
    public void UpdateOrderedQty(Guid itemId, decimal deltaQty)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId || i.ItemId == itemId);
        if (item == null)
            return;

        item.OrderedQty = Math.Max(0, item.OrderedQty + deltaQty);
        UpdateOrderStatus();
    }

    /// <summary>Recalculates status based on item ordered quantities.</summary>
    public void UpdateOrderStatus()
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled)
            return;

        if (_items.Count > 0 && _items.All(i => i.StockQty <= 0 || i.OrderedQty >= i.StockQty))
            Status = DocumentStatus.Completed; // Ordered
        else if (_items.Any(i => i.OrderedQty > 0))
            Status = DocumentStatus.ToDeliverAndBill; // Partially Ordered
        else
            Status = DocumentStatus.Submitted;
    }

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.Amount);
        GrandTotal = NetTotal;
        HasUnitPriceItems = _items.Any(i => i.Quantity == 0);
    }
}

public class SupplierQuotationItem : FullAuditedEntity<Guid>
{
    public Guid SupplierQuotationId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Qty * ConversionFactor;

    /// <summary>Quantity already ordered via Purchase Orders (in stock UOM).</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Remaining quantity to order (in stock UOM).</summary>
    public decimal PendingOrderQty => Math.Max(0, StockQty - OrderedQty);

    /// <summary>Link to Material Request item (if applicable).</summary>
    public Guid? MaterialRequestItemId { get; set; }

    /// <summary>Supplier-quoted lead time in days for this item.</summary>
    public int? LeadTimeDays { get; set; }

    // Aliases for unified access across comparison/conversion services
    public decimal UnitPrice => Rate;
    public decimal Quantity => Qty;
    public string Description => ItemName ?? "";

    protected SupplierQuotationItem() { }

    public SupplierQuotationItem(Guid id, Guid sqId, Guid itemId,
        decimal qty, decimal rate, string? itemName, string? uom) : base(id)
    {
        SupplierQuotationId = sqId;
        ItemId = itemId;
        Qty = qty;
        Rate = rate;
        ItemName = itemName;
        Uom = uom;
    }
}
