using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a purchase invoice.
/// Maps to ERPNext accounts/doctype/purchase_invoice_item.
/// </summary>
public class PurchaseInvoiceItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseInvoiceId { get; set; }
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

    public Guid? TaxCategoryId { get; set; }

    /// <summary>Link to Purchase Order item (for billing qty tracking).</summary>
    public Guid? PurchaseOrderItemId { get; set; }

    /// <summary>Link to Purchase Receipt item (for receipt-to-bill traceability).</summary>
    public Guid? PurchaseReceiptItemId { get; set; }

    // --- Deferred Expense fields (mirrors SI deferred revenue) ---
    private bool _enableDeferredExpense;
    /// <summary>When true, expense is recognized over the service period (not at invoice post).</summary>
    public bool EnableDeferredExpense 
    { 
        get => _enableDeferredExpense;
        set 
        {
            _enableDeferredExpense = value;
            if (!value) ClearDeferredFields();
        }
    }

    /// <summary>Deferred expense account (liability account holding pre-paid expense).</summary>
    public Guid? DeferredExpenseAccountId { get; set; }

    /// <summary>Service period start date for deferred expense recognition.</summary>
    public DateTime? ServiceStartDate { get; set; }

    /// <summary>Service period end date for deferred expense recognition.</summary>
    public DateTime? ServiceEndDate { get; set; }

    protected PurchaseInvoiceItem() { }

    public PurchaseInvoiceItem(
        Guid id, Guid purchaseInvoiceId, Guid itemId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
        : base(id)
    {
        PurchaseInvoiceId = purchaseInvoiceId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
    }

    /// <summary>
    /// Clears all deferred expense fields when EnableDeferredExpense is unchecked.
    /// Per ERPNext PR #57140: clear deferred revenue/expense fields on uncheck.
    /// </summary>
    public void ClearDeferredFields()
    {
        EnableDeferredExpense = false;
        DeferredExpenseAccountId = null;
        ServiceStartDate = null;
        ServiceEndDate = null;
    }
}
