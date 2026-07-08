using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace MyERP.Automation.DTOs;

public class AutomationRuleDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public AutomationTrigger Trigger { get; set; }
    public string? DocumentType { get; set; }
    public string? ConditionExpression { get; set; }
    public AutomationAction Action { get; set; }
    public string? ActionConfig { get; set; }
    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}

public class CreateAutomationRuleDto
{
    [Required]
    [StringLength(AutomationRuleConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(AutomationRuleConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [Required]
    public AutomationTrigger Trigger { get; set; }

    [StringLength(AutomationRuleConsts.MaxDocumentTypeLength)]
    public string? DocumentType { get; set; }

    [StringLength(AutomationRuleConsts.MaxConditionExpressionLength)]
    public string? ConditionExpression { get; set; }

    [Required]
    public AutomationAction Action { get; set; }

    [StringLength(AutomationRuleConsts.MaxActionConfigLength)]
    public string? ActionConfig { get; set; }

    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
}

public class UpdateAutomationRuleDto
{
    [Required]
    [StringLength(AutomationRuleConsts.MaxNameLength)]
    public string Name { get; set; } = null!;

    [StringLength(AutomationRuleConsts.MaxDescriptionLength)]
    public string? Description { get; set; }

    [StringLength(AutomationRuleConsts.MaxConditionExpressionLength)]
    public string? ConditionExpression { get; set; }

    [Required]
    public AutomationAction Action { get; set; }

    [StringLength(AutomationRuleConsts.MaxActionConfigLength)]
    public string? ActionConfig { get; set; }

    public Guid? CompanyId { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}

public class AutomationExecutionLogDto : EntityDto<Guid>
{
    public Guid AutomationRuleId { get; set; }
    public string? RuleName { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? SourceDocumentType { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExecutionDurationMs { get; set; }
    public DateTime CreationTime { get; set; }
}
