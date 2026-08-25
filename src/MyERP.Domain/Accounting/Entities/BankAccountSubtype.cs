using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank Account Subtype — e.g. Corporate, Personal, High-Yield, Fixed Deposit.
/// Maps to ERPNext accounts/doctype/bank_account_subtype.
/// </summary>
public class BankAccountSubtype : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string AccountSubtypeName { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected BankAccountSubtype() { }

    public BankAccountSubtype(Guid id, string accountSubtypeName, string? description = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetAccountSubtypeName(accountSubtypeName);
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetAccountSubtypeName(string accountSubtypeName)
    {
        AccountSubtypeName = Check.NotNullOrWhiteSpace(accountSubtypeName, nameof(accountSubtypeName), BankAccountSubtypeConsts.MaxAccountSubtypeLength);
    }
}
