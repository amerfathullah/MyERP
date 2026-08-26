using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Bank master — central record for a financial institution.
/// Maps to ERPNext accounts/doctype/bank.
/// </summary>
public class Bank : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string BankName { get; private set; } = null!;
    public string? SwiftNumber { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;

    protected Bank() { }

    public Bank(Guid id, string bankName, string? swiftNumber = null, string? website = null, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        SetBankName(bankName);
        SwiftNumber = swiftNumber;
        Website = website;
        IsActive = isActive;
        TenantId = tenantId;
    }

    public void SetBankName(string bankName)
    {
        BankName = Check.NotNullOrWhiteSpace(bankName, nameof(bankName), BankConsts.MaxBankNameLength);
    }

    public void Disable()
    {
        IsActive = false;
    }

    public void Enable()
    {
        IsActive = true;
    }
}
