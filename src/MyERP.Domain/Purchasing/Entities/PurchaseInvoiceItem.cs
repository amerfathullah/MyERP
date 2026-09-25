using System;
using MyERP;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a purchase invoice.
/// Maps to ERPNext accounts/doctype/purchase_invoice_item.
/// </summary>
public class PurchaseInvoiceItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseInvoiceId { get; set; }
    /// <summary>Zero-based row position within the document (ERPNext idx); the database gives no ordering guarantee.</summary>
    public int Idx { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    /// <summary>Total received quantity. Equals Quantity + RejectedQty.</summary>
    public decimal ReceivedQty { get; set; }
    /// <summary>Rejected quantity (damaged, wrong spec, failed inspection).</summary>
    public decimal RejectedQty { get; set; }
    /// <summary>Warehouse where rejected goods are stored (when UpdateStock=true).</summary>
    public Guid? RejectedWarehouseId { get; set; }
    /// <summary>When true, this item bills the rejected quantity on a stock updating invoice (PR #59258 / commit 16b1be814c).</summary>
    public bool BillsRejectedQuantity { get; set; }
    /// <summary>Quantity billed to supplier: Quantity + RejectedQty when BillsRejectedQuantity is true, else Quantity.</summary>
    public decimal BilledQuantity => (BillsRejectedQuantity && RejectedQty != 0) ? (Quantity + RejectedQty) : Quantity;
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LineTotal => BilledQuantity * UnitPrice;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Rejected quantity in stock UOM = RejectedQty × ConversionFactor.</summary>
    public decimal RejectedStockQty => RejectedQty * ConversionFactor;

    /// <summary>Rate per stock UOM = UnitPrice / ConversionFactor (gotcha #198).</summary>
    public decimal StockUomRate => ConversionFactor > 0 ? Math.Round(UnitPrice / ConversionFactor, 4) : UnitPrice;

    public Guid? TaxCategoryId { get; set; }

    /// <summary>Link to Purchase Order item (for billing qty tracking).</summary>
    public Guid? PurchaseOrderItemId { get; set; }

    /// <summary>Link to Purchase Receipt item (for receipt-to-bill traceability).</summary>
    public Guid? PurchaseReceiptItemId { get; set; }

    /// <summary>Source warehouse for internal transfer (when UpdateStock=true).</summary>
    public Guid? FromWarehouseId { get; set; }

    /// <summary>Target warehouse (when UpdateStock=true).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Whether goods are drop-shipped (delivered directly to customer by supplier).</summary>
    public bool DeliveredBySupplier { get; set; }

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

    /// <summary>Service stop date (for early termination of service contracts). Per ERPNext: accounts/deferred_revenue.py.</summary>
    public DateTime? ServiceStopDate { get; set; }

    /// <summary>
    /// Cumulative landed cost voucher amount allocated to this item.
    /// Per ERPNext PR #57475 / #58575: prorated landed cost charge deduction from purchase expense.
    /// </summary>
    public decimal LandedCostVoucherAmount { get; set; }

    /// <summary>
    /// Calculated purchase expense GL amount.
    /// Per PR #57475: deducts landed cost voucher amount to prevent double-counting.
    /// </summary>
    public decimal PurchaseExpenseGlAmount => LineTotal - LandedCostVoucherAmount;

    /// <summary>
    /// Computes purchase expense GL amount after deducting landed cost voucher amount in transaction currency.
    /// Per ERPNext PR #58575 / commit 9cb736a271: prorate landed cost charge into transaction currency.
    /// </summary>
    public decimal GetPurchaseExpenseGlAmount(decimal exchangeRate = 1m)
    {
        var lcvInTxnCurrency = exchangeRate > 0
            ? LandedCostVoucherAmount / exchangeRate
            : LandedCostVoucherAmount;
        return LineTotal - lcvInTxnCurrency;
    }

    /// <summary>
    /// Validates accepted and rejected quantities against received quantity per ERPNext buying_controller (gotchas #488, #3197).
    /// </summary>
    public void ValidateAcceptedRejectedQty(bool isReturn)
    {
        if (isReturn)
        {
            if (Quantity > 0 || RejectedQty > 0 || ReceivedQty > 0)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Accepted, rejected, and received quantities must be negative for return invoices.");

            if (ReceivedQty == 0 && (Quantity < 0 || RejectedQty < 0))
                ReceivedQty = Quantity + RejectedQty;
            else if (Math.Abs(ReceivedQty - (Quantity + RejectedQty)) > 0.0001m)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Received Qty ({ReceivedQty}) must equal Accepted Qty ({Quantity}) + Rejected Qty ({RejectedQty}).");
        }
        else
        {
            if (Quantity < 0 || RejectedQty < 0 || ReceivedQty < 0)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Accepted, rejected, and received quantities cannot be negative for normal invoices.");

            if (ReceivedQty == 0 && (Quantity > 0 || RejectedQty > 0))
                ReceivedQty = Quantity + RejectedQty;
            else if (Math.Abs(ReceivedQty - (Quantity + RejectedQty)) > 0.0001m)
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Received Qty ({ReceivedQty}) must equal Accepted Qty ({Quantity}) + Rejected Qty ({RejectedQty}).");
        }
    }

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
        _enableDeferredExpense = false;
        DeferredExpenseAccountId = null;
        ServiceStartDate = null;
        ServiceEndDate = null;
        ServiceStopDate = null;
    }
}
