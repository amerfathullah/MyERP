using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

public class QualityGoalDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Goal { get; set; }
    public string Frequency { get; set; } = null!;
    public decimal TargetValue { get; set; }
    public string? Uom { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateUpdateQualityGoalDto
{
    public string Name { get; set; } = null!;
    public string? Goal { get; set; }
    public string Frequency { get; set; } = null!;
    public decimal TargetValue { get; set; }
    public string? Uom { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public bool IsEnabled { get; set; }
}

public class QualityActionDto : FullAuditedEntityDto<Guid>
{
    public int ActionType { get; set; }
    public string ProblemDescription { get; set; } = null!;
    public string? Resolution { get; set; }
    public int Status { get; set; }
    public Guid? RelatedQualityGoalId { get; set; }
    public Guid? AssignedUserId { get; set; }
}

public class CreateUpdateQualityActionDto
{
    public int ActionType { get; set; }
    public string ProblemDescription { get; set; } = null!;
    public Guid? RelatedQualityGoalId { get; set; }
    public Guid? AssignedUserId { get; set; }
}

public class ResolveQualityActionDto
{
    public string Resolution { get; set; } = null!;
}

public class QualityReviewDto : FullAuditedEntityDto<Guid>
{
    public Guid QualityGoalId { get; set; }
    public DateTime ReviewDate { get; set; }
    public decimal? ActualValue { get; set; }
    public int Status { get; set; }
    public string? Notes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}

public class CreateQualityReviewDto
{
    public Guid QualityGoalId { get; set; }
    public DateTime ReviewDate { get; set; }
    public decimal ActualValue { get; set; }
    public string? Notes { get; set; }
}
