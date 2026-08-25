using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Projects.Entities;

/// <summary>
/// Project Update — checkpoint progress update, status log, and milestone report for a project.
/// Maps to ERPNext projects/doctype/project_update.
/// </summary>
public class ProjectUpdate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid ProjectId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? Time { get; set; }
    public decimal PercentComplete { get; set; }
    public string? Summary { get; set; }
    public string? Notes { get; set; }
    public bool Sent { get; set; }

    protected ProjectUpdate() { }

    public ProjectUpdate(
        Guid id,
        Guid projectId,
        DateTime date,
        decimal percentComplete = 0,
        string? summary = null,
        string? notes = null,
        TimeSpan? time = null,
        Guid? tenantId = null)
        : base(id)
    {
        ProjectId = projectId;
        Date = date;
        PercentComplete = percentComplete;
        Summary = summary;
        Notes = notes;
        Time = time ?? DateTime.Now.TimeOfDay;
        TenantId = tenantId;
    }
}
