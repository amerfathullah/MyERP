using System;
using System.ComponentModel.DataAnnotations;
using MyERP.Workflow;
using Volo.Abp.Application.Dtos;

namespace MyERP.Workflow.DTOs;

public class ApprovalRuleDto : EntityDto<Guid>
{
    public string DocumentType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Level { get; set; }
    public string? ApproverRoleName { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? ConditionExpression { get; set; }
    public decimal? MinimumAmount { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

public class CreateApprovalRuleDto
{
    [Required]
    [StringLength(ApprovalRuleConsts.MaxDocumentTypeLength)]
    public string DocumentType { get; set; } = null!;

    [Required]
    [StringLength(ApprovalRuleConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [Range(1, 10)]
    public int Level { get; set; } = 1;

    public string? ApproverRoleName { get; set; }
    public Guid? ApproverUserId { get; set; }

    [StringLength(ApprovalRuleConsts.MaxConditionExpressionLength)]
    public string? ConditionExpression { get; set; }

    public decimal? MinimumAmount { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; } = true;

    [StringLength(ApprovalRuleConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class UpdateApprovalRuleDto
{
    [Required]
    [StringLength(ApprovalRuleConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [Range(1, 10)]
    public int Level { get; set; }

    public string? ApproverRoleName { get; set; }
    public Guid? ApproverUserId { get; set; }

    [StringLength(ApprovalRuleConsts.MaxConditionExpressionLength)]
    public string? ConditionExpression { get; set; }

    public decimal? MinimumAmount { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; }

    [StringLength(ApprovalRuleConsts.MaxDescriptionLength)]
    public string? Description { get; set; }
}

public class ApprovalRequestDto : EntityDto<Guid>
{
    public Guid ApprovalRuleId { get; set; }
    public string DocumentType { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public int Level { get; set; }
    public ApprovalStatus Status { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Remarks { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime CreationTime { get; set; }

    // Denormalized from rule for display
    public string? RuleName { get; set; }
}

public class ReviewApprovalDto
{
    [Required]
    public Guid RequestId { get; set; }

    [StringLength(ApprovalRequestConsts.MaxRemarksLength)]
    public string? Remarks { get; set; }
}
