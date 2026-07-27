using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank Account — links a physical bank account to a GL account.
/// Each bank account has a 1:1 relationship with a GL account.
/// Used for bank reconciliation, payment processing, and statement imports.
/// Per DO-NOT: "Link same GL account to multiple Bank Accounts within a company
/// (one-to-one GL→BankAccount mapping enforced)".
/// </summary>
public class BankAccount : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Display name for the bank account (e.g., "CIMB Savings 1234").</summary>
    public string AccountName { get; private set; } = null!;

    /// <summary>The GL Account (type: Bank/Cash) this bank account links to.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Bank name (e.g., "CIMB Bank", "Maybank").</summary>
    public string BankName { get; set; } = null!;

    /// <summary>Bank account number.</summary>
    public string? BankAccountNo { get; set; }

    /// <summary>IBAN for international transfers.</summary>
    public string? Iban { get; set; }

    /// <summary>SWIFT/BIC code.</summary>
    public string? SwiftCode { get; set; }

    /// <summary>Branch code.</summary>
    public string? BranchCode { get; set; }

    /// <summary>Whether this is a company-owned account (vs customer/supplier account).</summary>
    public bool IsCompanyAccount { get; set; }

    /// <summary>Default bank account for the company.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Party type (Customer/Supplier) if not a company account.</summary>
    public string? PartyType { get; set; }

    /// <summary>Party ID (Customer/Supplier ID) if not a company account.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>ISO 4217 currency code. Derived from GL Account currency.</summary>
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>Integration ID from bank connector (e.g., Plaid account ID).</summary>
    public string? IntegrationId { get; set; }

    /// <summary>Last date bank statements were synced.</summary>
    public DateTime? LastIntegrationDate { get; set; }

    /// <summary>Whether this account is disabled.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Whether this is a credit card account.</summary>
    public bool IsCreditCard { get; set; }

    protected BankAccount() { }

    public BankAccount(Guid id, Guid companyId, string accountName, Guid accountId,
        string bankName, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        AccountName = accountName;
        AccountId = accountId;
        BankName = bankName;
        TenantId = tenantId;
        IsCompanyAccount = true;
    }

    /// <summary>
    /// Set as default bank account. Clears previous default externally.
    /// Per DO-NOT: "is_default exclusivity: setting is_default=1 clears ALL OTHER defaults for same company."
    /// </summary>
    public void SetAsDefault()
    {
        if (IsDisabled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("message", "Cannot set disabled account as default.");
        IsDefault = true;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    public void Disable()
    {
        IsDisabled = true;
        IsDefault = false;
    }

    public void Enable()
    {
        IsDisabled = false;
    }

    public void UpdateDetails(string accountName, string bankName, string? bankAccountNo,
        string? iban, string? swiftCode, string? branchCode)
    {
        AccountName = accountName;
        BankName = bankName;
        BankAccountNo = bankAccountNo;
        Iban = iban;
        SwiftCode = swiftCode;
        BranchCode = branchCode;
    }
}
