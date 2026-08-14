using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace MyERP.Inventory;

// ─── Quality Goal DTOs ───────────────────────────────────────────────
public class QualityGoalDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public string? Goal { get; set; }
    public string Frequency { get; set; } = null!;
    public decimal TargetValue { get; set; }
    public string? Uom { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public Guid? ProcedureId { get; set; }
    public string? Weekday { get; set; }
    public int? DayOfMonth { get; set; }
    public bool IsEnabled { get; set; }
    public List<QualityGoalObjectiveDto> Objectives { get; set; } = new();
}

public class QualityGoalObjectiveDto : EntityDto<Guid>
{
    public Guid QualityGoalId { get; set; }
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public string? Uom { get; set; }
}

public class CreateUpdateQualityGoalDto
{
    public string Name { get; set; } = null!;
    public string? Goal { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public decimal TargetValue { get; set; }
    public string? Uom { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public Guid? ProcedureId { get; set; }
    public string? Weekday { get; set; }
    public int? DayOfMonth { get; set; }
    public bool IsEnabled { get; set; } = true;
    public List<CreateUpdateQualityGoalObjectiveDto> Objectives { get; set; } = new();
}

public class CreateUpdateQualityGoalObjectiveDto
{
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public string? Uom { get; set; }
}

// ─── Quality Review DTOs ─────────────────────────────────────────────
public class QualityReviewDto : FullAuditedEntityDto<Guid>
{
    public Guid QualityGoalId { get; set; }
    public Guid? ProcedureId { get; set; }
    public DateTime ReviewDate { get; set; }
    public decimal? ActualValue { get; set; }
    public QualityReviewStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public List<QualityReviewObjectiveDto> Objectives { get; set; } = new();
}

public class QualityReviewObjectiveDto : EntityDto<Guid>
{
    public Guid QualityReviewId { get; set; }
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Uom { get; set; }
    public QualityReviewStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class CreateQualityReviewDto
{
    public Guid QualityGoalId { get; set; }
    public Guid? ProcedureId { get; set; }
    public DateTime ReviewDate { get; set; }
    public decimal? ActualValue { get; set; }
    public string? Notes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public List<CreateQualityReviewObjectiveDto> Objectives { get; set; } = new();
}

public class CreateQualityReviewObjectiveDto
{
    public string Objective { get; set; } = null!;
    public decimal Target { get; set; }
    public decimal? Actual { get; set; }
    public string? Uom { get; set; }
    public QualityReviewStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class EvaluateQualityReviewDto
{
    public decimal? ActualValue { get; set; }
    public string? Notes { get; set; }
    public bool Passed { get; set; }
}

// ─── Quality Action DTOs ─────────────────────────────────────────────
public class QualityActionDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public QualityActionType ActionType { get; set; }
    public string ProblemDescription { get; set; } = null!;
    public string? Resolution { get; set; }
    public QualityActionStatus Status { get; set; }
    public Guid? RelatedQualityGoalId { get; set; }
    public Guid? RelatedQualityReviewId { get; set; }
    public Guid? RelatedProcedureId { get; set; }
    public Guid? RelatedFeedbackId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public List<QualityActionResolutionDto> Resolutions { get; set; } = new();
}

public class QualityActionResolutionDto : EntityDto<Guid>
{
    public Guid QualityActionId { get; set; }
    public string Problem { get; set; } = null!;
    public string ResolutionDetails { get; set; } = null!;
    public QualityActionStatus Status { get; set; }
}

public class CreateUpdateQualityActionDto
{
    public Guid CompanyId { get; set; }
    public QualityActionType ActionType { get; set; }
    public string ProblemDescription { get; set; } = null!;
    public Guid? RelatedQualityGoalId { get; set; }
    public Guid? RelatedQualityReviewId { get; set; }
    public Guid? RelatedProcedureId { get; set; }
    public Guid? RelatedFeedbackId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public List<CreateQualityActionResolutionDto> Resolutions { get; set; } = new();
}

public class CreateQualityActionResolutionDto
{
    public string Problem { get; set; } = null!;
    public string ResolutionDetails { get; set; } = null!;
}

public class ResolveQualityActionDto
{
    public string Resolution { get; set; } = null!;
}

// ─── Quality Procedure DTOs ──────────────────────────────────────────
public class QualityProcedureDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public Guid? ParentQualityProcedureId { get; set; }
    public bool IsGroup { get; set; }
    public string? Description { get; set; }
    public string? ProcessOwner { get; set; }
    public int Sequence { get; set; }
    public List<QualityProcedureStepDto> Steps { get; set; } = new();
}

public class QualityProcedureStepDto : EntityDto<Guid>
{
    public Guid QualityProcedureId { get; set; }
    public string Description { get; set; } = null!;
    public int Sequence { get; set; }
    public Guid? ChildProcedureId { get; set; }
}

public class CreateUpdateQualityProcedureDto
{
    public string Name { get; set; } = null!;
    public Guid? ParentQualityProcedureId { get; set; }
    public bool IsGroup { get; set; }
    public string? Description { get; set; }
    public string? ProcessOwner { get; set; }
    public int Sequence { get; set; }
    public List<CreateUpdateQualityProcedureStepDto> Steps { get; set; } = new();
}

public class CreateUpdateQualityProcedureStepDto
{
    public string Description { get; set; } = null!;
    public int Sequence { get; set; }
    public Guid? ChildProcedureId { get; set; }
}

// ─── Non-Conformance DTOs ────────────────────────────────────────────
public class NonConformanceDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string Subject { get; set; } = null!;
    public Guid? ProcedureId { get; set; }
    public string? ProcessOwner { get; set; }
    public string? Details { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public NonConformanceStatus Status { get; set; }
    public DateTime? ResolutionDate { get; set; }
}

public class CreateUpdateNonConformanceDto
{
    public Guid CompanyId { get; set; }
    public string Subject { get; set; } = null!;
    public Guid? ProcedureId { get; set; }
    public string? ProcessOwner { get; set; }
    public string? Details { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
}

public class ResolveNonConformanceDto
{
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
}

// ─── Quality Meeting DTOs ────────────────────────────────────────────
public class QualityMeetingDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public DateTime MeetingDate { get; set; }
    public string? Chairperson { get; set; }
    public string? Attendees { get; set; }
    public QualityMeetingStatus Status { get; set; }
    public List<QualityMeetingAgendaDto> Agendas { get; set; } = new();
    public List<QualityMeetingMinutesDto> Minutes { get; set; } = new();
}

public class QualityMeetingAgendaDto : EntityDto<Guid>
{
    public Guid QualityMeetingId { get; set; }
    public string Agenda { get; set; } = null!;
}

public class QualityMeetingMinutesDto : EntityDto<Guid>
{
    public Guid QualityMeetingId { get; set; }
    public string Discussion { get; set; } = null!;
    public string? ActionPlan { get; set; }
    public Guid? AssignedUserId { get; set; }
}

public class CreateUpdateQualityMeetingDto
{
    public Guid CompanyId { get; set; }
    public DateTime MeetingDate { get; set; }
    public string? Chairperson { get; set; }
    public string? Attendees { get; set; }
    public List<string> Agendas { get; set; } = new();
    public List<CreateQualityMeetingMinutesDto> Minutes { get; set; } = new();
}

public class CreateQualityMeetingMinutesDto
{
    public string Discussion { get; set; } = null!;
    public string? ActionPlan { get; set; }
    public Guid? AssignedUserId { get; set; }
}

// ─── Quality Feedback DTOs ───────────────────────────────────────────
public class QualityFeedbackTemplateDto : FullAuditedEntityDto<Guid>
{
    public string TemplateName { get; set; } = null!;
    public List<QualityFeedbackTemplateParameterDto> Parameters { get; set; } = new();
}

public class QualityFeedbackTemplateParameterDto : EntityDto<Guid>
{
    public Guid QualityFeedbackTemplateId { get; set; }
    public string Parameter { get; set; } = null!;
}

public class CreateUpdateQualityFeedbackTemplateDto
{
    public string TemplateName { get; set; } = null!;
    public List<string> Parameters { get; set; } = new();
}

public class QualityFeedbackDto : FullAuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public QualityFeedbackDocumentType DocumentType { get; set; }
    public string DocumentName { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public string? Remarks { get; set; }
    public List<QualityFeedbackParameterDto> Parameters { get; set; } = new();
}

public class QualityFeedbackParameterDto : EntityDto<Guid>
{
    public Guid QualityFeedbackId { get; set; }
    public string Parameter { get; set; } = null!;
    public int Rating { get; set; }
    public string? Remarks { get; set; }
}

public class CreateQualityFeedbackDto
{
    public Guid CompanyId { get; set; }
    public QualityFeedbackDocumentType DocumentType { get; set; }
    public string DocumentName { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public string? Remarks { get; set; }
    public List<CreateQualityFeedbackParameterDto> Parameters { get; set; } = new();
}

public class CreateQualityFeedbackParameterDto
{
    public string Parameter { get; set; } = null!;
    public int Rating { get; set; }
    public string? Remarks { get; set; }
}
