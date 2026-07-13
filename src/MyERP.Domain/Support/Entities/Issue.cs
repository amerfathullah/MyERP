using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Support.Entities;

/// <summary>
/// Issue — customer support ticket.
/// Tracks issues raised by customers through various channels.
/// Supports SLA tracking (response/resolution time).
/// </summary>
public class Issue : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Subject { get; set; } = null!;
    public string? Description { get; set; }

    public IssueStatus Status { get; private set; } = IssueStatus.Open;

    /// <summary>Priority: Low, Medium, High, Urgent.</summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>Issue type (e.g., "Bug", "Feature Request", "Complaint").</summary>
    public string? IssueType { get; set; }

    /// <summary>Customer who raised the issue.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Contact person at customer.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Assigned employee/agent.</summary>
    public Guid? AssignedToId { get; set; }

    /// <summary>Channel: Email, Phone, Website, Social.</summary>
    public string? RaisedVia { get; set; }

    public DateTime OpeningDate { get; set; }
    public DateTime? ResolutionDate { get; set; }

    /// <summary>SLA: first response target (hours).</summary>
    public decimal? FirstResponseTime { get; set; }

    /// <summary>SLA: resolution target (hours).</summary>
    public decimal? ResolutionTime { get; set; }

    /// <summary>Actual first response datetime.</summary>
    public DateTime? FirstRespondedOn { get; set; }

    /// <summary>Total hold time in hours (deducted from resolution time for SLA).</summary>
    public decimal TotalHoldTime { get; set; }

    public string? Resolution { get; set; }

    protected Issue() { }

    public Issue(Guid id, Guid companyId, string subject, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Subject = Check.NotNullOrWhiteSpace(subject, nameof(subject), 500);
        OpeningDate = DateTime.UtcNow;
        TenantId = tenantId;
    }

    public void Reply()
    {
        if (Status == IssueStatus.Open && !FirstRespondedOn.HasValue)
            FirstRespondedOn = DateTime.UtcNow;

        if (Status == IssueStatus.Open)
            Status = IssueStatus.Replied;
    }

    public void Hold()
    {
        if (Status is IssueStatus.Closed or IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.OnHold;
    }

    public void Reopen()
    {
        if (Status is not (IssueStatus.Closed or IssueStatus.Replied or IssueStatus.OnHold))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.Open;
        ResolutionDate = null;
    }

    public void Resolve(string? resolution = null)
    {
        if (Status is IssueStatus.Closed or IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.Closed;
        ResolutionDate = DateTime.UtcNow;
        Resolution = resolution;
    }

    public void Cancel()
    {
        if (Status == IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.Cancelled;
    }
}

public enum IssueStatus
{
    Open = 0,
    Replied = 1,
    OnHold = 2,
    Closed = 3,
    Cancelled = 4,
}
