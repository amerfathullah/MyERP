using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using MyERP.Sales;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Purchase Invoice — records supplier bills.
/// Maps to ERPNext accounts/doctype/purchase_invoice.
/// Implements IAccountableDocument for automatic GL posting.
/// </summary>
public class PurchaseInvoice : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    /// <summary>Supplier's own invoice number.</summary>
    public string? SupplierInvoiceNumber { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>Supplier TIN — for LHDN e-Invoice.</summary>
    public string? SupplierTin { get; set; }

    /// <summary>Buyer TIN (company's TIN) — for LHDN e-Invoice.</summary>
    public string? BuyerTin { get; set; }

    // Amounts
    public string CurrencyCode { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount => GrandTotal - AmountPaid - WriteOffAmount - TotalAdvance;

    // Discount on grand total
    public decimal AdditionalDiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }

    // Base (company) currency amounts
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount => BaseGrandTotal - (AmountPaid * ExchangeRate);

    /// <summary>If true, this is an opening balance entry (for go-live migration).</summary>
    public bool IsOpening { get; set; }

    /// <summary>Payable account (credit_to). Must match original on debit notes.</summary>
    public Guid CreditToAccountId { get; set; }

    /// <summary>Payment terms template for auto-generating due dates.</summary>
    public Guid? PaymentTermsTemplateId { get; set; }

    /// <summary>Billing address (auto-resolved from Supplier on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>If true, this is a return (debit note).</summary>
    public bool IsReturn { get; set; }

    /// <summary>If true, this invoice is for subcontracted service.</summary>
    public bool IsSubcontracted { get; set; }

    /// <summary>If true, stock movements are created on submit (direct purchase without PR).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock receipt when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Original invoice this return is against.</summary>
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>Linked Sales Invoice ID from inter-company transaction.</summary>
    public Guid? InterCompanyInvoiceId { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    // Workflow
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // LHDN e-Invoice fields
    public EInvoiceDocumentType? EInvoiceDocType { get; set; }
    public EInvoiceStatus EInvoiceStatus { get; set; } = EInvoiceStatus.NotSubmitted;
    public string? LhdnUuid { get; set; }
    public Guid? LhdnSubmissionId { get; set; }
    public DateTime? LhdnSubmittedAt { get; set; }
    public string? LhdnLongId { get; set; }

    public string? Notes { get; set; }

    // Advance payment tracking
    /// <summary>Total advance payments allocated against this invoice.</summary>
    public decimal TotalAdvance { get; set; }

    /// <summary>Write-off amount. Reduces outstanding without payment.</summary>
    public decimal WriteOffAmount { get; set; }

    /// <summary>Write-off GL account.</summary>
    public Guid? WriteOffAccountId { get; set; }

    /// <summary>Write-off cost center.</summary>
    public Guid? WriteOffCostCenterId { get; set; }

    /// <summary>Invoice-level hold, independent of Supplier.HoldType. Blocks Payment Entry against this invoice.</summary>
    public bool OnHold { get; set; }

    /// <summary>Reason for the hold. Cleared automatically when OnHold is lifted.</summary>
    public string? HoldComment { get; set; }

    /// <summary>Date the hold auto-releases. Must be a future date when set. Null = held indefinitely.</summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Cost center for departmental expense attribution.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise cost tracking.</summary>
    public Guid? ProjectId { get; set; }

    // Rounded total
    /// <summary>Grand total rounded to nearest whole number.</summary>
    public decimal RoundedTotal { get; set; }

    /// <summary>Rounding adjustment = RoundedTotal - GrandTotal.</summary>
    public decimal RoundingAdjustment { get; set; }

    /// <summary>Base currency rounded total.</summary>
    public decimal BaseRoundedTotal { get; set; }

    /// <summary>Base currency rounding adjustment.</summary>
    public decimal BaseRoundingAdjustment { get; set; }

    /// <summary>Whether rounding is disabled for this invoice.</summary>
    public bool DisableRoundedTotal { get; set; }

    /// <summary>
    /// Whether the invoice is overdue (past due date with outstanding balance).
    /// Per ERPNext: overdue detection is AMOUNT-based for payment schedules.
    /// </summary>
    public bool IsOverdue => Status == DocumentStatus.Posted
        && !IsReturn
        && OutstandingAmount > 0.01m
        && DueDate.HasValue
        && DueDate.Value.Date < DateTime.UtcNow.Date;

    private readonly List<PurchaseInvoiceItem> _items = new();
    public IReadOnlyList<PurchaseInvoiceItem> Items => _items.AsReadOnly();

    // IAccountableDocument
    string IAccountableDocument.DocumentType => "PurchaseInvoice";
    Guid? IAccountableDocument.CustomerId => null;
    Guid? IAccountableDocument.SupplierId => SupplierId;
    DateTime IAccountableDocument.PostingDate => IssueDate;

    protected PurchaseInvoice() { }

    public PurchaseInvoice(Guid id, Guid companyId, Guid supplierId, string invoiceNumber, DateTime issueDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        InvoiceNumber = Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber), PurchaseInvoiceConsts.MaxInvoiceNumberLength);
        IssueDate = issueDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));

        // Normal invoices: qty must be positive. Returns (IsReturn=true): qty must be negative.
        if (!IsReturn && quantity <= 0)
            throw new ArgumentException("Quantity must be positive for non-return invoices.", nameof(quantity));

        _items.Add(new PurchaseInvoiceItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));

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
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per DO-NOT: opening invoices with update_stock=true are blocked (accounting-only)
        if (IsOpening && UpdateStock)
            throw new BusinessException(MyERPDomainErrorCodes.OpeningInvoiceCannotUpdateStock)
                .WithData("documentType", "Purchase Invoice");

        // Per DO-NOT / gotcha #3846: update_stock cannot be enabled when items reference Purchase Receipts (prevents double SLE)
        if (UpdateStock && _items.Any(i => i.PurchaseReceiptItemId.HasValue))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Cannot enable Update Stock when invoice contains items linked to Purchase Receipts.");
        }

        // Validate deferred expense service dates (per ERPNext accounts/deferred_revenue.py)
        foreach (var item in _items.Where(i => i.EnableDeferredExpense))
        {
            if (item.ServiceStartDate.HasValue && item.ServiceEndDate.HasValue && item.ServiceStartDate > item.ServiceEndDate)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Service Start Date cannot be after Service End Date.");
            }
            if (item.ServiceStopDate.HasValue)
            {
                if (item.ServiceStartDate.HasValue && item.ServiceStopDate < item.ServiceStartDate)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Service Stop Date cannot be before Service Start Date.");
                }
                if (item.ServiceEndDate.HasValue && item.ServiceStopDate > item.ServiceEndDate)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", "Service Stop Date cannot be after Service End Date.");
                }
            }
        }

        // Validate from_warehouse and UOM conversion factor per ERPNext buying/stock controller
        foreach (var item in _items)
        {
            // Auto-correct conversion factor when UOM equals StockUOM (gotcha #6171)
            if (!string.IsNullOrEmpty(item.Uom) && !string.IsNullOrEmpty(item.StockUom)
                && string.Equals(item.Uom, item.StockUom, StringComparison.OrdinalIgnoreCase)
                && item.ConversionFactor != 1.0m)
            {
                item.ConversionFactor = 1.0m;
            }

            // From Warehouse validation (gotcha #6179)
            if (item.FromWarehouseId.HasValue)
            {
                if (IsSubcontracted)
                    throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseOnSubcontractedDocument);

                var targetWarehouse = item.WarehouseId ?? WarehouseId;
                if (targetWarehouse.HasValue && item.FromWarehouseId.Value == targetWarehouse.Value)
                    throw new BusinessException(MyERPDomainErrorCodes.FromWarehouseEqualsTargetWarehouse);
            }
        }

        Status = DocumentStatus.Submitted;
        AddLocalEvent(new PurchaseInvoiceSubmittedEvent(this));
    }

    public void Post()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Posted;
        AddLocalEvent(new PurchaseInvoicePostedEvent(this));
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
        AddLocalEvent(new PurchaseInvoiceCancelledEvent(this));
    }

    public void ApplyRounding()
    {
        if (DisableRoundedTotal)
        {
            RoundedTotal = GrandTotal;
            RoundingAdjustment = 0;
            BaseRoundedTotal = BaseGrandTotal;
            BaseRoundingAdjustment = 0;
            return;
        }

        RoundedTotal = Math.Round(GrandTotal, 0, MidpointRounding.AwayFromZero);
        RoundingAdjustment = RoundedTotal - GrandTotal;
        BaseRoundedTotal = Math.Round(BaseGrandTotal, 0, MidpointRounding.AwayFromZero);
        BaseRoundingAdjustment = BaseRoundedTotal - BaseGrandTotal;
    }

    public void SetWriteOff(decimal amount, Guid? writeOffAccountId = null, Guid? writeOffCostCenterId = null)
    {
        if (amount < 0) throw new ArgumentException("Write-off amount cannot be negative.", nameof(amount));
        if (amount > OutstandingAmount + WriteOffAmount)
            throw new ArgumentException("Write-off amount exceeds outstanding.", nameof(amount));

        WriteOffAmount = amount;
        WriteOffAccountId = writeOffAccountId;
        WriteOffCostCenterId = writeOffCostCenterId;
    }

    public void SetTotalAdvance(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("Advance amount cannot be negative.", nameof(amount));
        TotalAdvance = amount;
    }

    /// <summary>
    /// Blocks or unblocks this invoice from payment, independent of any Supplier-level hold.
    /// Per ERPNext purchase_invoice.py: release_date must be a future date when set, and is
    /// cleared automatically when the hold itself is lifted (before_save cleanup) so a stale
    /// date can't linger and silently un-block a later re-hold.
    /// </summary>
    public void SetHold(bool onHold, string? holdComment, DateTime? releaseDate)
    {
        if (onHold && releaseDate.HasValue && releaseDate.Value.Date <= DateTime.UtcNow.Date)
            throw new BusinessException(MyERPDomainErrorCodes.ReleaseDateMustBeFuture);

        OnHold = onHold;
        HoldComment = onHold ? holdComment : null;
        ReleaseDate = onHold ? releaseDate : null;
    }

    /// <summary>
    /// True when this invoice itself is blocked from payment — on hold with no release date,
    /// or a release date that hasn't arrived yet. Independent of Supplier.HoldType.
    /// </summary>
    public bool IsBlocked => OnHold && (!ReleaseDate.HasValue || ReleaseDate.Value.Date > DateTime.UtcNow.Date);

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
        BaseNetTotal = NetTotal * ExchangeRate;
        BaseTaxAmount = TaxAmount * ExchangeRate;
        BaseGrandTotal = GrandTotal * ExchangeRate;
    }
}

// Domain Events
public record PurchaseInvoiceSubmittedEvent(PurchaseInvoice Invoice);
public record PurchaseInvoicePostedEvent(PurchaseInvoice Invoice);
public record PurchaseInvoiceCancelledEvent(PurchaseInvoice Invoice);
