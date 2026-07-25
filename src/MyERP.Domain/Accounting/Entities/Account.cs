using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Chart of Accounts — tree-structured account entity.
/// Maps to ERPNext accounts/doctype/account.
/// </summary>
public class Account : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string AccountCode { get; private set; } = null!;
    public string AccountName { get; private set; } = null!;
    public AccountType AccountType { get; set; }
    public AccountSubType? AccountSubType { get; set; }

    /// <summary>Parent account for tree hierarchy. Null = root level.</summary>
    public Guid? ParentAccountId { get; set; }

    /// <summary>If true, this account is a group and cannot have direct postings.</summary>
    public bool IsGroup { get; set; }

    /// <summary>ISO 4217 currency code. Null = company default currency.</summary>
    public string? Currency { get; set; }

    public string? Description { get; set; }

    /// <summary>Frozen accounts cannot receive new journal entries.</summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Enforces balance direction. Per DO-NOT: "Skip GL balance_must_be enforcement
    /// (Debit accounts can't go credit, Credit accounts can't go debit)".
    /// Null = no enforcement. "Debit" = balance must be ≥ 0. "Credit" = balance must be ≤ 0.
    /// </summary>
    public BalanceDirection? BalanceMustBe { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Account Category for financial report template formulas.
    /// Per gotcha #158: v16 adds account_category Link field. 28 standard categories.
    /// </summary>
    public Guid? AccountCategoryId { get; set; }

    protected Account() { }

    public Account(
        Guid id,
        Guid companyId,
        string accountCode,
        string accountName,
        AccountType accountType,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SetAccountCode(accountCode);
        SetAccountName(accountName);
        AccountType = accountType;
        TenantId = tenantId;
    }

    public void SetAccountCode(string accountCode)
    {
        AccountCode = Check.NotNullOrWhiteSpace(accountCode, nameof(accountCode), AccountConsts.MaxAccountCodeLength);
    }

    public void SetAccountName(string accountName)
    {
        AccountName = Check.NotNullOrWhiteSpace(accountName, nameof(accountName), AccountConsts.MaxAccountNameLength);
    }

    /// <summary>
    /// Sets the parent account ID with self-reference guard.
    /// Per DO-NOT: circular reference detection on parent chains.
    /// </summary>
    public void SetParent(Guid? parentAccountId)
    {
        if (parentAccountId.HasValue && parentAccountId.Value == Id)
            throw new BusinessException(MyERPDomainErrorCodes.AccountIsGroup)
                .WithData("message", "Account cannot reference itself as parent.");

        ParentAccountId = parentAccountId;
    }
}
