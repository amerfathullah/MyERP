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

    /// <summary>Link to the parent issue if this issue was split from another issue.</summary>
    public Guid? SplitFromIssueId { get; set; }

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

    /// <summary>When the current hold started (null when not on hold).</summary>
    public DateTime? HoldStartedOn { get; set; }

    /// <summary>Service Level Agreement entity ID (if assigned).</summary>
    public Guid? ServiceLevelAgreementId { get; set; }

    /// <summary>SLA breach: true if resolution exceeded target time.</summary>
    public bool IsSlaBreach { get; set; }

    /// <summary>SLA agreement status, recomputed on every state transition.</summary>
    public AgreementStatus AgreementStatus { get; private set; } = AgreementStatus.FirstResponseDue;

    /// <summary>
    /// Actual resolution time in hours (excludes hold time).
    /// Calculated: (ResolutionDate - OpeningDate).TotalHours - TotalHoldTime
    /// </summary>
    public decimal ActualResolutionTimeHours
    {
        get
        {
            if (!ResolutionDate.HasValue) return 0;
            var totalHours = (decimal)(ResolutionDate.Value - OpeningDate).TotalHours;
            return Math.Max(0, totalHours - TotalHoldTime);
        }
    }

    /// <summary>
    /// Actual first response time in hours.
    /// Calculated: (FirstRespondedOn - OpeningDate).TotalHours
    /// </summary>
    public decimal ActualFirstResponseTimeHours =>
        FirstRespondedOn.HasValue
            ? (decimal)(FirstRespondedOn.Value - OpeningDate).TotalHours
            : 0;

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

    /// <summary>Assigns an SLA and its priority-specific (or SLA-default) response/resolution targets.</summary>
    public void ApplySla(Guid serviceLevelAgreementId, decimal responseTimeHours, decimal resolutionTimeHours)
    {
        ServiceLevelAgreementId = serviceLevelAgreementId;
        FirstResponseTime = responseTimeHours;
        ResolutionTime = resolutionTimeHours;
    }

    public void Reply()
    {
        if (Status == IssueStatus.Open && !FirstRespondedOn.HasValue)
            FirstRespondedOn = DateTime.UtcNow;

        if (Status == IssueStatus.Open)
            Status = IssueStatus.Replied;

        UpdateAgreementStatus();
    }

    public void Hold()
    {
        if (Status is IssueStatus.Closed or IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.OnHold;
        HoldStartedOn = DateTime.UtcNow;
        AgreementStatus = AgreementStatus.Paused;
    }

    public void Reopen()
    {
        if (Status is not (IssueStatus.Closed or IssueStatus.Replied or IssueStatus.OnHold))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Accumulate hold time if we're coming off a hold
        if (Status == IssueStatus.OnHold && HoldStartedOn.HasValue)
        {
            TotalHoldTime += (decimal)(DateTime.UtcNow - HoldStartedOn.Value).TotalHours;
            HoldStartedOn = null;
        }

        Status = IssueStatus.Open;
        ResolutionDate = null;
        UpdateAgreementStatus();
    }

    public void Resolve(string? resolution = null)
    {
        if (Status is IssueStatus.Closed or IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // If resolved while on hold, close the hold period first
        if (Status == IssueStatus.OnHold && HoldStartedOn.HasValue)
        {
            TotalHoldTime += (decimal)(DateTime.UtcNow - HoldStartedOn.Value).TotalHours;
            HoldStartedOn = null;
        }

        Status = IssueStatus.Closed;
        ResolutionDate = DateTime.UtcNow;
        Resolution = resolution;

        // Check SLA breach: actual resolution time > target
        if (ResolutionTime.HasValue && ActualResolutionTimeHours > ResolutionTime.Value)
            IsSlaBreach = true;

        AgreementStatus = IsSlaBreach ? AgreementStatus.Failed : AgreementStatus.Fulfilled;
    }

    public void Cancel()
    {
        if (Status == IssueStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = IssueStatus.Cancelled;
    }

    /// <summary>Recomputes AgreementStatus from the current response/resolution state (called on non-terminal transitions).</summary>
    private void UpdateAgreementStatus()
    {
        if (Status is IssueStatus.Closed or IssueStatus.Cancelled)
            return;

        if (Status == IssueStatus.OnHold)
        {
            AgreementStatus = AgreementStatus.Paused;
            return;
        }

        AgreementStatus = FirstRespondedOn.HasValue
            ? AgreementStatus.ResolutionDue
            : AgreementStatus.FirstResponseDue;
    }
}
