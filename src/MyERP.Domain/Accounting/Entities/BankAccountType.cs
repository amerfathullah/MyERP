using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank Account Type — e.g. Current, Savings, Checking, Credit Card, Loan, Investment.
/// Maps to ERPNext accounts/doctype/bank_account_type.
/// </summary>
public class BankAccountType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string AccountTypeName { get; private set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    protected BankAccountType() { }

    public BankAccountType(Guid id, string accountTypeName, string? description = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetAccountTypeName(accountTypeName);
        Description = description;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetAccountTypeName(string accountTypeName)
    {
        AccountTypeName = Check.NotNullOrWhiteSpace(accountTypeName, nameof(accountTypeName), BankAccountTypeConsts.MaxAccountTypeLength);
    }
}
