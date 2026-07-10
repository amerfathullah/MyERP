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
public class PurchaseInvoice : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument
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
    public decimal OutstandingAmount => GrandTotal - AmountPaid;

    // Base (company) currency amounts
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTaxAmount { get; set; }
    public decimal BaseGrandTotal { get; set; }
    public decimal BaseOutstandingAmount => BaseGrandTotal - (AmountPaid * ExchangeRate);

    // Workflow
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // LHDN e-Invoice fields
    public EInvoiceDocumentType? EInvoiceDocType { get; set; }
    public EInvoiceStatus EInvoiceStatus { get; set; } = EInvoiceStatus.NotSubmitted;
    public string? LhdnUuid { get; set; }

    public string? Notes { get; set; }

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
        CompanyId = companyId;
        SupplierId = supplierId;
        InvoiceNumber = Check.NotNullOrWhiteSpace(invoiceNumber, nameof(invoiceNumber), PurchaseInvoiceConsts.MaxInvoiceNumberLength);
        IssueDate = issueDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _items.Add(new PurchaseInvoiceItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));

        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Submitted;
    }

    public void Post()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Posted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
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
