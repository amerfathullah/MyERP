using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Mode of Payment — payment method (Cash, Credit Card, Wire Transfer, Bank Draft, Cheque).
/// Links to a default GL account per company for automatic posting.
/// </summary>
public class ModeOfPayment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Type: Cash, Bank, General.</summary>
    public string Type { get; set; } = "Bank";

    public bool IsActive { get; set; } = true;

    /// <summary>Default GL account for this payment mode (per company).</summary>
    public Guid? DefaultAccountId { get; set; }

    /// <summary>Company this default account belongs to.</summary>
    public Guid? CompanyId { get; set; }

    protected ModeOfPayment() { }

    public ModeOfPayment(Guid id, string name, string type, Guid? tenantId = null)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100);
        Type = type;
        TenantId = tenantId;
    }
}
