using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Workflow.Entities;

/// <summary>
/// Tracks an individual approval request for a specific document.
/// Created when a document enters a workflow requiring approval.
/// </summary>
public class ApprovalRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>The approval rule that triggered this request.</summary>
    public Guid ApprovalRuleId { get; set; }

    /// <summary>Document type (denormalized for querying).</summary>
    public string DocumentType { get; private set; } = null!;

    /// <summary>ID of the document being approved.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Approval level this request corresponds to.</summary>
    public int Level { get; set; }

    /// <summary>Current status of this approval step.</summary>
    public ApprovalStatus Status { get; private set; } = ApprovalStatus.Pending;

    /// <summary>User who performed the approval/rejection action.</summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>When the approval/rejection was performed.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Remarks from the approver (reason for rejection, notes, etc.).</summary>
    public string? Remarks { get; set; }

    /// <summary>User who requested the approval (document submitter).</summary>
    public Guid RequestedByUserId { get; set; }

    protected ApprovalRequest() { }

    public ApprovalRequest(
        Guid id, Guid approvalRuleId, string documentType,
        Guid documentId, int level, Guid requestedByUserId,
        Guid? tenantId = null) : base(id)
    {
        ApprovalRuleId = approvalRuleId;
        DocumentType = documentType;
        DocumentId = documentId;
        Level = level;
        RequestedByUserId = requestedByUserId;
        TenantId = tenantId;
    }

    public void Approve(Guid reviewerUserId, string? remarks = null)
    {
        if (Status != ApprovalStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = ApprovalStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;
        Remarks = remarks;
    }

    public void Reject(Guid reviewerUserId, string? remarks = null)
    {
        if (Status != ApprovalStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = ApprovalStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAt = DateTime.UtcNow;
        Remarks = remarks;
    }

    public void Cancel()
    {
        if (Status != ApprovalStatus.Pending)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = ApprovalStatus.Cancelled;
    }
}
