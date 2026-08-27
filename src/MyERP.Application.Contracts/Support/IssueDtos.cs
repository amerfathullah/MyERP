using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Support;

/// <summary>Issue DTO for read operations.</summary>
public class IssueDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Subject { get; set; } = null!;
    public string? Description { get; set; }
    public IssueStatus Status { get; set; }
    public string Priority { get; set; } = null!;
    public string? IssueType { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? RaisedVia { get; set; }
    public DateTime OpeningDate { get; set; }
    public DateTime? ResolutionDate { get; set; }
    public string? Resolution { get; set; }
    public DateTime? FirstRespondedOn { get; set; }
    public double? TotalHoldTimeSeconds { get; set; }
    public bool IsSlaBreach { get; set; }
    public Guid? ServiceLevelAgreementId { get; set; }
    public decimal? FirstResponseTime { get; set; }
    public decimal? ResolutionTime { get; set; }
    public DateTime? ResponseByDate { get; set; }
    public DateTime? ResolutionByDate { get; set; }
    public AgreementStatus AgreementStatus { get; set; }
    public Guid? SplitFromIssueId { get; set; }
}

/// <summary>DTO for creating a new issue.</summary>
public class CreateIssueDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required][StringLength(IssueConsts.MaxSubjectLength)] public string Subject { get; set; } = null!;
    [StringLength(IssueConsts.MaxDescriptionLength)] public string? Description { get; set; }
    [StringLength(IssueConsts.MaxPriorityLength)] public string? Priority { get; set; }
    [StringLength(IssueConsts.MaxIssueTypeLength)] public string? IssueType { get; set; }
    public Guid? CustomerId { get; set; }
    [StringLength(IssueConsts.MaxRaisedViaLength)] public string? RaisedVia { get; set; }
}

/// <summary>DTO for list filtering.</summary>
public class GetIssueListDto : PagedAndSortedResultRequestDto
{
    public IssueStatus? Status { get; set; }
    public Guid? CompanyId { get; set; }
    public string? Filter { get; set; }
    public string? Priority { get; set; }
}

/// <summary>DTO for resolving an issue.</summary>
public class ResolveIssueDto
{
    [StringLength(IssueConsts.MaxResolutionLength)]
    public string? Resolution { get; set; }
}

/// <summary>DTO for splitting an issue into a new sub-issue.</summary>
public class SplitIssueDto
{
    [Required]
    [StringLength(IssueConsts.MaxSubjectLength)]
    public string Subject { get; set; } = null!;
}

/// <summary>DTO for resetting SLA on an issue (Gotcha #6007).</summary>
public class ResetIssueSlaDto
{
    [Required]
    [StringLength(500)]
    public string ResetReason { get; set; } = null!;

    [StringLength(IssueConsts.MaxPriorityLength)]
    public string? NewPriority { get; set; }

    public Guid? NewServiceLevelAgreementId { get; set; }
}

