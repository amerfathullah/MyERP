using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Entry — records payments received from customers or paid to suppliers.
/// Maps to ERPNext accounts/doctype/payment_entry.
/// </summary>
public class PaymentEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? PaymentNumber { get; set; }
    public PaymentType PaymentType { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>Mode: Cash, Bank Transfer, Cheque, Online, etc.</summary>
    public string? ModeOfPayment { get; set; }

    // Party
    public string? PartyType { get; set; } // "Customer" or "Supplier"
    public Guid? PartyId { get; set; }

    // Accounts
    public Guid PaidFromAccountId { get; set; }
    public Guid PaidToAccountId { get; set; }

    /// <summary>Mode of payment (Cash, Bank Transfer, Cheque, etc.).</summary>
    public Guid? ModeOfPaymentId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal PaidAmount { get; set; }

    /// <summary>Exchange rate of payment currency to company base currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Exchange rate of the source invoice (for gain/loss calculation).</summary>
    public decimal SourceExchangeRate { get; set; } = 1m;

    /// <summary>Base currency amount (PaidAmount × ExchangeRate).</summary>
    public decimal BaseAmount => PaidAmount * ExchangeRate;

    /// <summary>Exchange gain/loss = PaidAmount × (ExchangeRate - SourceExchangeRate).</summary>
    public decimal ExchangeGainLoss => PaidAmount * (ExchangeRate - SourceExchangeRate);

    /// <summary>Bank reference / cheque number.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    // Linked invoice (optional — can allocate to multiple invoices in future)
    public Guid? AgainstInvoiceId { get; set; }
    public string? AgainstInvoiceType { get; set; } // "SalesInvoice" or "PurchaseInvoice"

    // Advance payment against order (when no invoice exists yet)
    /// <summary>Linked Sales Order or Purchase Order for advance payments.</summary>
    public Guid? AgainstOrderId { get; set; }
    /// <summary>"SalesOrder" or "PurchaseOrder".</summary>
    public string? AgainstOrderType { get; set; }

    /// <summary>Whether this is an advance/deposit payment (before invoice).</summary>
    public bool IsAdvance => AgainstOrderId.HasValue && !AgainstInvoiceId.HasValue;

    /// <summary>
    /// Multi-invoice allocation references. Enables split payments where one PE
    /// allocates amounts across multiple invoices/orders.
    /// When populated, these take priority over the legacy AgainstInvoiceId field.
    /// </summary>
    public ICollection<PaymentEntryReference> References { get; set; } = new List<PaymentEntryReference>();

    /// <summary>
    /// Amount not allocated to any invoice/order. 
    /// UnallocatedAmount = PaidAmount - sum(References.AllocatedAmount) - (legacy AgainstInvoice allocation).
    /// Positive value means excess payment (advance/on-account).
    /// </summary>
    public decimal UnallocatedAmount
    {
        get
        {
            var allocated = References.Sum(r => r.AllocatedAmount);
            if (allocated == 0 && AgainstInvoiceId.HasValue)
                allocated = PaidAmount; // Legacy single-invoice: fully allocated
            return Math.Max(0, PaidAmount - allocated);
        }
    }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // IAccountableDocument
    string IAccountableDocument.DocumentType => "PaymentEntry";
    decimal IAccountableDocument.NetTotal => PaidAmount;
    decimal IAccountableDocument.GrandTotal => PaidAmount;
    decimal IAccountableDocument.TaxAmount => 0;
    Guid? IAccountableDocument.CustomerId => PartyType == "Customer" ? PartyId : null;
    Guid? IAccountableDocument.SupplierId => PartyType == "Supplier" ? PartyId : null;

    protected PaymentEntry() { }

    public PaymentEntry(
        Guid id, Guid companyId, PaymentType paymentType, DateTime postingDate,
        decimal paidAmount, Guid paidFromAccountId, Guid paidToAccountId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PaymentType = paymentType;
        PostingDate = postingDate;
        PaidAmount = paidAmount;
        PaidFromAccountId = paidFromAccountId;
        PaidToAccountId = paidToAccountId;
        TenantId = tenantId;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
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
}
