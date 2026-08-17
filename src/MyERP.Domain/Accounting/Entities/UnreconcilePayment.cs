using System;
using System.Collections.Generic;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Unreconcile Payment — unlinks a Payment Entry or Journal Entry from the invoices/orders it was
/// allocated against, by delinking the corresponding Payment Ledger Entry rows.
/// Maps to ERPNext accounts/doctype/unreconcile_payment.
/// </summary>
public class UnreconcilePayment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public UnreconcileVoucherType VoucherType { get; set; }
    public Guid VoucherId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<UnreconcilePaymentEntry> _allocations = new();
    public IReadOnlyList<UnreconcilePaymentEntry> Allocations => _allocations.AsReadOnly();

    protected UnreconcilePayment() { }

    public UnreconcilePayment(Guid id, Guid companyId, UnreconcileVoucherType voucherType, Guid voucherId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        VoucherType = voucherType;
        VoucherId = Check.NotDefaultOrNull<Guid>(voucherId, nameof(voucherId));
        TenantId = tenantId;
    }

    public void AddAllocation(Guid paymentLedgerEntryId, string againstVoucherType, Guid againstVoucherId, decimal amount)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _allocations.Add(new UnreconcilePaymentEntry(Guid.NewGuid(), Id, paymentLedgerEntryId, againstVoucherType, againstVoucherId, amount));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (_allocations.Count == 0)
            throw new BusinessException(MyERPDomainErrorCodes.UnreconcilePaymentHasNoAllocations);

        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

/// <summary>One Payment Ledger Entry allocation unlinked by an Unreconcile Payment.</summary>
public class UnreconcilePaymentEntry : Entity<Guid>
{
    public Guid UnreconcilePaymentId { get; set; }
    public Guid PaymentLedgerEntryId { get; set; }

    /// <summary>The voucher (invoice/order) the allocation was against.</summary>
    public string AgainstVoucherType { get; set; } = null!;
    public Guid AgainstVoucherId { get; set; }

    public decimal Amount { get; set; }
    public bool Unlinked { get; set; }

    protected UnreconcilePaymentEntry() { }

    public UnreconcilePaymentEntry(Guid id, Guid unreconcilePaymentId, Guid paymentLedgerEntryId,
        string againstVoucherType, Guid againstVoucherId, decimal amount)
        : base(id)
    {
        UnreconcilePaymentId = unreconcilePaymentId;
        PaymentLedgerEntryId = paymentLedgerEntryId;
        AgainstVoucherType = againstVoucherType;
        AgainstVoucherId = againstVoucherId;
        Amount = amount;
    }
}
