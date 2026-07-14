using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Invoice — core transactional document.
/// Maps to ERPNext accounts/doctype/sales_invoice.
/// Implements IAccountableDocument for automatic GL posting via AccountingRuleEngine.
/// </summary>
public class SalesInvoice : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string InvoiceNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }

    // Customer
    public Guid CustomerId { get; set; }

    /// <summary>Supplier TIN (company's TIN) — for LHDN e-Invoice.</summary>
    public string? SupplierTin { get; set; }

    /// <summary>Buyer TIN (customer's TIN) — for LHDN e-Invoice.</summary>
    public string? BuyerTin { get; set; }

    // Amounts
    public string CurrencyCode { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingAmount => GrandTotal - AmountPaid;

    // Discount on grand total
    /// <summary>Additional discount percentage applied on net total.</summary>
    public decimal AdditionalDiscountPercentage { get; set; }
    /// <summary>Fixed discount amount (mutually exclusive with percentage).</summary>
    public decimal DiscountAmount { get; set; }

    // Base (company) currency amounts
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount => BaseGrandTotal - (AmountPaid * ExchangeRate);

    /// <summary>If true, this is an opening balance entry (for go-live migration). Bypasses payment terms.</summary>
    public bool IsOpening { get; set; }

    /// <summary>Payment terms template for auto-generating due dates.</summary>
    public Guid? PaymentTermsTemplateId { get; set; }

    /// <summary>Billing address (auto-resolved from Customer on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Shipping address (auto-resolved from Customer on create).</summary>
    public Guid? ShippingAddressId { get; set; }

    /// <summary>If true, this is a return (credit note).</summary>
    public bool IsReturn { get; set; }

    /// <summary>If true, stock movements are created on submit (direct sales without DN).</summary>
    public bool UpdateStock { get; set; }

    /// <summary>Warehouse for stock deduction when UpdateStock=true.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Original invoice this return is against.</summary>
    public Guid? ReturnAgainstId { get; set; }

    // Amendment (cancel-and-amend workflow)
    /// <summary>Reference to the cancelled invoice this was amended from.</summary>
    public Guid? AmendedFromId { get; set; }
    /// <summary>Amendment index (0 = original, 1+ = amendments).</summary>
    public int AmendmentIndex { get; set; }

    // Workflow
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // LHDN e-Invoice fields
    public EInvoiceDocumentType? EInvoiceDocType { get; set; }
    public EInvoiceStatus EInvoiceStatus { get; set; } = EInvoiceStatus.NotSubmitted;
    public string? LhdnUuid { get; set; }
    public string? LhdnLongId { get; set; }
    public DateTime? LhdnValidationDate { get; set; }
    public string? QrCodeUrl { get; set; }

    public string? Notes { get; set; }

    /// <summary>Link to project for timesheet-based billing.</summary>
    public Guid? ProjectId { get; set; }

    // Line items
    private readonly List<SalesInvoiceItem> _items = new();
    public IReadOnlyList<SalesInvoiceItem> Items => _items.AsReadOnly();

    // IAccountableDocument implementation
    string IAccountableDocument.DocumentType => "SalesInvoice";
    Guid? IAccountableDocument.CustomerId => CustomerId;
    Guid? IAccountableDocument.SupplierId => null;
    DateTime IAccountableDocument.PostingDate => IssueDate;

    protected SalesInvoice() { }

    public SalesInvoice(Guid id, Guid companyId, Guid customerId, string invoiceNumber, DateTime issueDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        InvoiceNumber = Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber), SalesInvoiceConsts.MaxInvoiceNumberLength);
        IssueDate = issueDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Normal invoices: qty must be positive. Returns (IsReturn=true): qty must be negative.
        if (!IsReturn && quantity <= 0)
            throw new ArgumentException("Quantity must be positive for non-return invoices.", nameof(quantity));
        if (IsReturn && quantity >= 0)
            throw new ArgumentException("Quantity must be negative for return invoices (credit notes).", nameof(quantity));

        _items.Add(new SalesInvoiceItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));

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
                .WithData("documentType", "Sales Invoice");

        Status = DocumentStatus.Submitted;
        AddLocalEvent(new SalesInvoiceSubmittedEvent(this));
    }

    public void Post()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Posted;
        AddLocalEvent(new SalesInvoicePostedEvent(this));
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Cancelled;
        AddLocalEvent(new SalesInvoiceCancelledEvent(this));
    }

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
public record SalesInvoiceSubmittedEvent(SalesInvoice Invoice);
public record SalesInvoicePostedEvent(SalesInvoice Invoice);
public record SalesInvoiceCancelledEvent(SalesInvoice Invoice);
