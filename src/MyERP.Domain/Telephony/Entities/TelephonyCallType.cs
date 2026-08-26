using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Telephony.Entities;

/// <summary>
/// Telephony Call Type — category classification for telephony calls (e.g., Sales, Support, Billing, Follow-up).
/// Maps to ERPNext telephony/doctype/telephony_call_type.
/// </summary>
public class TelephonyCallType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string CallTypeName { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    protected TelephonyCallType() { }

    public TelephonyCallType(Guid id, string callTypeName, bool isActive = true, Guid? tenantId = null)
        : base(id)
    {
        CallTypeName = Check.NotNullOrWhiteSpace(callTypeName, nameof(callTypeName), TelephonyConsts.MaxCallTypeNameLength);
        IsActive = isActive;
        TenantId = tenantId;
    }
}
