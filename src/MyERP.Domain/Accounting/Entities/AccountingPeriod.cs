using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Accounting Period — controls which document types are allowed to post in a given date range.
/// When an accounting period is "Closed" for a doctype, that doctype cannot submit documents
/// with posting dates within the period (unless the user has the exempted role).
/// </summary>
public class AccountingPeriod : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string PeriodName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>If true, this period is closed for all applicable document types.</summary>
    public bool IsClosed { get; set; }

    /// <summary>Role that can bypass the period closure check.</summary>
    public string? ExemptedRole { get; set; }

    protected AccountingPeriod() { }

    public AccountingPeriod(Guid id, Guid companyId, string periodName, DateTime startDate, DateTime endDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PeriodName = Check.NotNullOrWhiteSpace(periodName, nameof(periodName), 100);
        StartDate = startDate;
        EndDate = endDate;
        TenantId = tenantId;
    }

    public void Close()
    {
        IsClosed = true;
    }

    public void Reopen()
    {
        IsClosed = false;
    }

    /// <summary>Check if a posting date falls within this period.</summary>
    public bool ContainsDate(DateTime postingDate)
    {
        return postingDate >= StartDate && postingDate <= EndDate;
    }
}
