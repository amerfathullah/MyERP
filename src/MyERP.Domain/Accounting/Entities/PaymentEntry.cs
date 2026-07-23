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
    public ICollection<PaymentEntryReference> References { get; private set; } = new List<PaymentEntryReference>();

    /// <summary>
    /// Tax/charge rows on this payment entry. PE has its own tax engine separate from SI/PI.
    /// Per ERPNext: "On Paid Amount" charge type, direction-dependent GL posting.
    /// Per DO-NOT: "Reuse Sales/Purchase Invoice tax engine for PE — PE has its own"
    /// </summary>
    public ICollection<PaymentEntryTax> Taxes { get; private set; } = new List<PaymentEntryTax>();

    /// <summary>
    /// Total tax amount across all non-exchange-gain-loss tax rows.
    /// Excludes exchange G/L entries per gotcha #437.
    /// </summary>
    public decimal TotalTaxes => Taxes.Where(t => !t.IsExchangeGainLoss).Sum(t => t.TaxAmount);

    /// <summary>
    /// Total taxes included in the paid amount (deducted from party's share).
    /// </summary>
    public decimal TotalIncludedTaxes => Taxes.Where(t => t.IncludedInPaidAmount && !t.IsExchangeGainLoss).Sum(t => t.TaxAmount);

    /// <summary>
    /// Amount not allocated to any invoice/order. 
    /// UnallocatedAmount = PaidAmount - sum(References.AllocatedAmount) - (legacy AgainstInvoice allocation).
    /// Positive value means excess payment (advance/on-account).
    /// Per gotcha #437: exchange gain/loss deductions excluded.
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
    decimal IAccountableDocument.GrandTotal => PaidAmount + TotalTaxes - TotalIncludedTaxes;
    decimal IAccountableDocument.TaxAmount => TotalTaxes;
    Guid? IAccountableDocument.CustomerId => PartyType == "Customer" ? PartyId : null;
    Guid? IAccountableDocument.SupplierId => PartyType == "Supplier" ? PartyId : null;

    protected PaymentEntry() { }

    public PaymentEntry(
        Guid id, Guid companyId, PaymentType paymentType, DateTime postingDate,
        decimal paidAmount, Guid paidFromAccountId, Guid paidToAccountId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        PaymentType = paymentType;
        PostingDate = postingDate;
        PaidAmount = paidAmount;
        PaidFromAccountId = Check.NotDefaultOrNull<Guid>(paidFromAccountId, nameof(paidFromAccountId));
        PaidToAccountId = Check.NotDefaultOrNull<Guid>(paidToAccountId, nameof(paidToAccountId));
        TenantId = tenantId;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
        AddLocalEvent(new PaymentEntrySubmittedEvent(this));
    }

    /// <summary>
    /// Adds a tax row to this payment entry. Only allowed in Draft status.
    /// Per DO-NOT: "PE tax account currency MUST equal company currency"
    /// </summary>
    public void AddTax(PaymentEntryTax tax)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Taxes.Add(tax);
    }

    /// <summary>
    /// Calculates all tax amounts based on current PaidAmount and ExchangeRate.
    /// Called before posting to ensure tax amounts are current.
    /// </summary>
    public void RecalculateTaxes()
    {
        foreach (var tax in Taxes)
        {
            tax.Calculate(PaidAmount, ExchangeRate);
        }
    }

    public void Post()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per DO-NOT: "Allow duplicate (doctype, name, payment_term, payment_request) in Payment Entry references"
        if (References.Count > 1)
        {
            var duplicateKeys = References
                .GroupBy(r => new { r.ReferenceType, r.ReferenceId })
                .Where(g => g.Count() > 1)
                .ToList();
            if (duplicateKeys.Any())
            {
                var first = duplicateKeys.First().Key;
                throw new BusinessException(MyERPDomainErrorCodes.DuplicatePaymentReference)
                    .WithData("referenceType", first.ReferenceType)
                    .WithData("referenceId", first.ReferenceId);
            }
        }

        Status = DocumentStatus.Posted;
        AddLocalEvent(new PaymentEntryPostedEvent(this));
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
        AddLocalEvent(new PaymentEntryCancelledEvent(this));
    }
}

// Domain Events
public record PaymentEntrySubmittedEvent(PaymentEntry PaymentEntry);
public record PaymentEntryPostedEvent(PaymentEntry PaymentEntry);
public record PaymentEntryCancelledEvent(PaymentEntry PaymentEntry);
