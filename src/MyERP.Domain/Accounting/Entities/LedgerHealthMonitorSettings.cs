using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Per-company configuration for the daily ledger health check job.
/// Maps to ERPNext accounts/doctype/ledger_health_monitor (simplified to one row per company,
/// rather than ERPNext's single settings doc with a child table of companies).
/// </summary>
public class LedgerHealthMonitorSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>How many days back the JE-balance check scans. Per ERPNext: configurable lookback window.</summary>
    public int LookbackPeriodDays { get; set; } = 30;

    protected LedgerHealthMonitorSettings() { }

    public LedgerHealthMonitorSettings(Guid id, Guid companyId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        TenantId = tenantId;
    }
}
