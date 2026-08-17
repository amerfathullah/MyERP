using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Payment Order — batches pending Payment Requests or Payment Entries for a single bank
/// submission run (cheque printing / bank file). Maps to ERPNext accounts/doctype/payment_order.
/// </summary>
public class PaymentOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? OrderNumber { get; set; }
    public PaymentOrderType PaymentOrderType { get; set; }
    public DateTime PostingDate { get; set; }

    /// <summary>Supplier — required and meaningful only for PaymentOrderType.PaymentRequest.</summary>
    public Guid? PartyId { get; set; }

    public Guid CompanyBankAccountId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>If this order amends a cancelled one.</summary>
    public Guid? AmendedFromId { get; set; }

    private readonly List<PaymentOrderReference> _references = new();
    public IReadOnlyList<PaymentOrderReference> References => _references.AsReadOnly();

    protected PaymentOrder() { }

    public PaymentOrder(Guid id, Guid companyId, PaymentOrderType paymentOrderType, DateTime postingDate,
        Guid companyBankAccountId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        PaymentOrderType = paymentOrderType;
        PostingDate = postingDate;
        CompanyBankAccountId = Check.NotDefaultOrNull<Guid>(companyBankAccountId, nameof(companyBankAccountId));
        TenantId = tenantId;
    }

    public PaymentOrderReference AddReference(string referenceType, Guid referenceId, decimal amount,
        Guid? supplierId, string? modeOfPayment, Guid bankAccountId, string? paymentReference = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var reference = new PaymentOrderReference(Guid.NewGuid(), Id, referenceType, referenceId, amount)
        {
            SupplierId = supplierId,
            ModeOfPayment = modeOfPayment,
            BankAccountId = bankAccountId,
            PaymentReference = paymentReference,
        };
        _references.Add(reference);
        return reference;
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (_references.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.PaymentOrderHasNoReferences);

        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>Reference rows for one supplier, batched by <see cref="PaymentOrderAppService.MakePaymentRecordsAsync"/> into a single Journal Entry.</summary>
    public IEnumerable<PaymentOrderReference> ReferencesForSupplier(Guid supplierId) =>
        _references.Where(r => r.SupplierId == supplierId);
}

/// <summary>Payment Order Reference — one payable/receivable document batched into the order.</summary>
public class PaymentOrderReference : Entity<Guid>
{
    public Guid PaymentOrderId { get; set; }

    /// <summary>Document type: "PaymentRequest" or "PaymentEntry".</summary>
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }

    public decimal Amount { get; set; }
    public Guid? SupplierId { get; set; }
    public string? ModeOfPayment { get; set; }
    public Guid BankAccountId { get; set; }
    public string? PaymentReference { get; set; }

    protected PaymentOrderReference() { }

    public PaymentOrderReference(Guid id, Guid paymentOrderId, string referenceType, Guid referenceId, decimal amount)
        : base(id)
    {
        PaymentOrderId = paymentOrderId;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Amount = amount;
    }
}
