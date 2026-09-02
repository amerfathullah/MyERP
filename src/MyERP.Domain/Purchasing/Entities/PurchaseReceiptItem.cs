using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Line item in a purchase receipt.
/// Maps to ERPNext stock/doctype/purchase_receipt_item.
/// </summary>
public class PurchaseReceiptItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseReceiptId { get; set; }
    public Guid ItemId { get; set; }

    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal RejectedQty { get; set; }
    public Guid? RejectedWarehouseId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor. Used for SLE creation.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Rejected quantity in stock UOM = RejectedQty × ConversionFactor (PR #51968 / commit 343ee9695b).</summary>
    public decimal RejectedStockQty => RejectedQty * ConversionFactor;

    /// <summary>Rate per stock UOM = UnitPrice / ConversionFactor (gotcha #198).</summary>
    public decimal StockUomRate => ConversionFactor > 0 ? Math.Round(UnitPrice / ConversionFactor, 4) : UnitPrice;

    /// <summary>Link back to the Purchase Order item being received.</summary>
    public Guid? PurchaseOrderItemId { get; set; }

    /// <summary>Source warehouse for internal transfer receipts. Must differ from target warehouse.</summary>
    public Guid? FromWarehouseId { get; set; }

    /// <summary>Target warehouse (item-level override). Falls back to receipt-level WarehouseId if null.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>
    /// Quantity already billed against this PR item (via Purchase Invoice).
    /// Per ERPNext: billed_amt tracking enables PR→PI billing status (PerBilled).
    /// </summary>
    public decimal BilledQty { get; set; }

    /// <summary>
    /// Validates accepted and rejected quantities against received quantity per ERPNext buying_controller (gotchas #488, #3197).
    /// </summary>
    public void ValidateAcceptedRejectedQty(bool isReturn)
    {
        if (!isReturn)
        {
            if (Quantity < 0 || RejectedQty < 0 || ReceivedQty < 0)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Accepted, rejected, and received quantities cannot be negative on non-return receipts.");
            }

            if (ReceivedQty == 0 && (Quantity > 0 || RejectedQty > 0))
            {
                ReceivedQty = Quantity + RejectedQty;
            }
            else if (Math.Abs(ReceivedQty - (Quantity + RejectedQty)) > 0.0001m)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Received Qty ({ReceivedQty}) must equal Accepted Qty ({Quantity}) + Rejected Qty ({RejectedQty}).");
            }
        }
    }

    /// <summary>
    /// Amount allocated from Landed Cost Vouchers to this item.
    /// Per ERPNext PR #57475: When calculating purchase expense GL entries,
    /// this amount is DEDUCTED from the item's valuation-based amount to prevent
    /// double-counting (LCV creates its own GL entries for the landed cost portion).
    /// Formula: purchase_expense_gl_amount = (valuation_rate × stock_qty) - LandedCostVoucherAmount
    /// </summary>
    public decimal LandedCostVoucherAmount { get; set; }

    /// <summary>Pending billing quantity = Quantity - BilledQty.</summary>
    public decimal PendingBillingQty => Math.Max(0, Math.Abs(Quantity) - Math.Abs(BilledQty));

    /// <summary>
    /// Cumulative variance between what Purchase Invoices billed this item at and this PR item's
    /// own receipt-time UnitPrice: SUM((piItem.UnitPrice - UnitPrice) × billedQty) across every PI
    /// that has billed against this row. Positive = billed higher than received (understated cost
    /// at receipt time); negative = billed lower (overstated).
    /// Per ERPNext `billing_status.py` (`amount_difference_with_purchase_invoice`): tracked per-row
    /// as the reference for a later incoming-rate/valuation adjustment. MyERP tracks the variance
    /// itself here; the adjust-incoming-rate + stock-revaluation cascade ERPNext performs when
    /// `adjust_incoming_rate` is enabled is a separate, larger, not-yet-migrated feature — this
    /// field is deliberately informational-only for now (see purchase-receipt-full.md "Billing
    /// Rate Variance Tracking").
    /// </summary>
    public decimal AmountDifferenceWithPurchaseInvoice { get; set; }

    /// <summary>
    /// Calculated purchase expense GL amount.
    /// Per PR #57475: deducts landed cost voucher amount to prevent double-counting.
    /// </summary>
    public decimal PurchaseExpenseGlAmount => (Quantity * UnitPrice) - LandedCostVoucherAmount;

    protected PurchaseReceiptItem() { }

    public PurchaseReceiptItem(
        Guid id, Guid purchaseReceiptId, Guid itemId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount,
        string uom = "Unit", Guid? purchaseOrderItemId = null)
        : base(id)
    {
        PurchaseReceiptId = purchaseReceiptId;
        ItemId = itemId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxAmount = taxAmount;
        Uom = uom;
        PurchaseOrderItemId = purchaseOrderItemId;
    }
}
