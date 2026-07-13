using System;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Request — request for payment from customer (portal/gateway integration).
/// Can be created from Sales Invoice, Sales Order, or Purchase Order.
/// On payment completion, auto-creates Payment Entry.
/// Maps to ERPNext accounts/doctype/payment_request.
/// </summary>
public class PaymentRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Payment type: Inward (receive) or Outward (pay).</summary>
    public string PaymentRequestType { get; set; } = "Inward";

    /// <summary>Source document type: SalesInvoice, SalesOrder, PurchaseOrder.</summary>
    public string ReferenceDoctype { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }

    /// <summary>Party (Customer or Supplier).</summary>
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = "Customer";
    public string? PartyName { get; set; }

    public decimal GrandTotal { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Currency { get; set; } = "MYR";

    /// <summary>Payment gateway (if applicable).</summary>
    public string? PaymentGateway { get; set; }
    public string? PaymentUrl { get; set; }

    /// <summary>Bank account for bank transfers.</summary>
    public Guid? BankAccountId { get; set; }

    public string? EmailTo { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }

    public PaymentRequestStatus Status { get; private set; } = PaymentRequestStatus.Draft;

    /// <summary>Created Payment Entry (after payment received).</summary>
    public Guid? PaymentEntryId { get; set; }

    protected PaymentRequest() { }

    public PaymentRequest(Guid id, Guid companyId, string referenceDoctype,
        Guid referenceId, Guid partyId, string partyType,
        decimal grandTotal, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        ReferenceDoctype = referenceDoctype;
        ReferenceId = referenceId;
        PartyId = partyId;
        PartyType = partyType;
        GrandTotal = grandTotal;
        OutstandingAmount = grandTotal;
        TenantId = tenantId;
    }

    public void Submit()
    {
        if (Status != PaymentRequestStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = PaymentRequestStatus.Initiated;
    }

    public void MarkPaid(Guid paymentEntryId)
    {
        if (Status != PaymentRequestStatus.Initiated)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        PaymentEntryId = paymentEntryId;
        Status = PaymentRequestStatus.Paid;
    }

    public void Cancel()
    {
        if (Status == PaymentRequestStatus.Paid)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot cancel paid payment request");
        Status = PaymentRequestStatus.Cancelled;
    }
}

public enum PaymentRequestStatus
{
    Draft = 0,
    Initiated = 1,
    Paid = 2,
    Cancelled = 3,
    Failed = 4
}
