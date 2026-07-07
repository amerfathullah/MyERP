using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Entry — records payments received from customers or paid to suppliers.
/// Maps to ERPNext accounts/doctype/payment_entry.
/// </summary>
public class PaymentEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
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

    public string CurrencyCode { get; set; } = "MYR";
    public decimal PaidAmount { get; set; }

    /// <summary>Bank reference / cheque number.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    // Linked invoice (optional — can allocate to multiple invoices in future)
    public Guid? AgainstInvoiceId { get; set; }
    public string? AgainstInvoiceType { get; set; } // "SalesInvoice" or "PurchaseInvoice"

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

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
