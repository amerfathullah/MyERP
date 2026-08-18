using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// A persisted anomaly detected by a ledger health check run.
/// Maps to ERPNext's "Ledger Health" record created by the daily monitor job.
/// </summary>
public class LedgerHealthRecord : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string CheckType { get; set; } = null!;
    public string Severity { get; set; } = "Warning";
    public string Description { get; set; } = null!;

    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }

    public decimal? ExpectedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? Difference { get; set; }

    public DateTime CheckedAt { get; set; }

    protected LedgerHealthRecord() { }

    public LedgerHealthRecord(Guid id, Guid companyId, string checkType, string severity, string description, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        CheckType = checkType;
        Severity = severity;
        Description = description;
        CheckedAt = DateTime.UtcNow;
        TenantId = tenantId;
    }
}
