using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize]
public class QualityManagementAppService : MyERPAppService, IQualityManagementAppService
{
    private readonly IRepository<QualityGoal, Guid> _goalRepository;
    private readonly IRepository<QualityAction, Guid> _actionRepository;
    private readonly IRepository<QualityReview, Guid> _reviewRepository;
    private readonly IRepository<QualityProcedure, Guid> _procedureRepository;
    private readonly IRepository<NonConformance, Guid> _nonConformanceRepository;
    private readonly IRepository<QualityMeeting, Guid> _meetingRepository;
    private readonly IRepository<QualityFeedbackTemplate, Guid> _feedbackTemplateRepository;
    private readonly IRepository<QualityFeedback, Guid> _feedbackRepository;

    public QualityManagementAppService(
        IRepository<QualityGoal, Guid> goalRepository,
        IRepository<QualityAction, Guid> actionRepository,
        IRepository<QualityReview, Guid> reviewRepository,
        IRepository<QualityProcedure, Guid> procedureRepository,
        IRepository<NonConformance, Guid> nonConformanceRepository,
        IRepository<QualityMeeting, Guid> meetingRepository,
        IRepository<QualityFeedbackTemplate, Guid> feedbackTemplateRepository,
        IRepository<QualityFeedback, Guid> feedbackRepository)
    {
        _goalRepository = goalRepository;
        _actionRepository = actionRepository;
        _reviewRepository = reviewRepository;
        _procedureRepository = procedureRepository;
        _nonConformanceRepository = nonConformanceRepository;
        _meetingRepository = meetingRepository;
        _feedbackTemplateRepository = feedbackTemplateRepository;
        _feedbackRepository = feedbackRepository;
    }

    // ─── Quality Goal ──────────────────────────────────────────────────
    public async Task<QualityGoalDto> GetGoalAsync(Guid id)
    {
        var entity = await _goalRepository.GetAsync(id);
        return new QualityGoalMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityGoalDto>> GetGoalListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _goalRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityGoalDto>(
            totalCount,
            entities.Select(e => new QualityGoalMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityGoalDto> CreateGoalAsync(CreateUpdateQualityGoalDto input)
    {
        var entity = new QualityGoal(GuidGenerator.Create(), input.Name, input.Frequency, input.TargetValue, CurrentTenant.Id)
        {
            Goal = input.Goal,
            Uom = input.Uom,
            ResponsibleUserId = input.ResponsibleUserId,
            ProcedureId = input.ProcedureId,
            Weekday = input.Weekday,
            DayOfMonth = input.DayOfMonth,
            IsEnabled = input.IsEnabled
        };

        if (input.Objectives != null)
        {
            foreach (var obj in input.Objectives)
            {
                entity.AddObjective(new QualityGoalObjective(GuidGenerator.Create(), entity.Id, obj.Objective, obj.Target, obj.Uom));
            }
        }

        await _goalRepository.InsertAsync(entity);
        return new QualityGoalMapper().Map(entity);
    }

    public async Task<QualityGoalDto> UpdateGoalAsync(Guid id, CreateUpdateQualityGoalDto input)
    {
        var entity = await _goalRepository.GetAsync(id);
        entity.Name = input.Name;
        entity.Goal = input.Goal;
        entity.Frequency = input.Frequency;
        entity.TargetValue = input.TargetValue;
        entity.Uom = input.Uom;
        entity.ResponsibleUserId = input.ResponsibleUserId;
        entity.ProcedureId = input.ProcedureId;
        entity.Weekday = input.Weekday;
        entity.DayOfMonth = input.DayOfMonth;
        entity.IsEnabled = input.IsEnabled;

        entity.ClearObjectives();
        if (input.Objectives != null)
        {
            foreach (var obj in input.Objectives)
            {
                entity.AddObjective(new QualityGoalObjective(GuidGenerator.Create(), entity.Id, obj.Objective, obj.Target, obj.Uom));
            }
        }

        await _goalRepository.UpdateAsync(entity);
        return new QualityGoalMapper().Map(entity);
    }

    public async Task DeleteGoalAsync(Guid id)
    {
        await _goalRepository.DeleteAsync(id);
    }

    // ─── Quality Review ────────────────────────────────────────────────
    public async Task<QualityReviewDto> GetReviewAsync(Guid id)
    {
        var entity = await _reviewRepository.GetAsync(id);
        return new QualityReviewMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityReviewDto>> GetReviewListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _reviewRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.ReviewDate)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityReviewDto>(
            totalCount,
            entities.Select(e => new QualityReviewMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityReviewDto> CreateReviewAsync(CreateQualityReviewDto input)
    {
        var entity = new QualityReview(GuidGenerator.Create(), input.QualityGoalId, input.ReviewDate, CurrentTenant.Id)
        {
            ProcedureId = input.ProcedureId,
            ActualValue = input.ActualValue,
            Notes = input.Notes,
            ReviewedByUserId = input.ReviewedByUserId
        };

        if (input.Objectives != null && input.Objectives.Count > 0)
        {
            foreach (var obj in input.Objectives)
            {
                var reviewObj = new QualityReviewObjective(GuidGenerator.Create(), entity.Id, obj.Objective, obj.Target, obj.Uom)
                {
                    Actual = obj.Actual,
                    Status = obj.Status,
                    Notes = obj.Notes
                };
                entity.AddObjective(reviewObj);
            }
        }
        else
        {
            // Auto-fetch objectives from parent QualityGoal if available
            var goal = await _goalRepository.FindAsync(input.QualityGoalId);
            if (goal != null && goal.Objectives != null)
            {
                foreach (var obj in goal.Objectives)
                {
                    entity.AddObjective(new QualityReviewObjective(GuidGenerator.Create(), entity.Id, obj.Objective, obj.Target, obj.Uom));
                }
            }
        }

        await _reviewRepository.InsertAsync(entity);
        return new QualityReviewMapper().Map(entity);
    }

    public async Task<QualityReviewDto> EvaluateReviewAsync(Guid id, EvaluateQualityReviewDto input)
    {
        var entity = await _reviewRepository.GetAsync(id);
        if (input.Passed)
        {
            entity.Pass(input.ActualValue, input.Notes);
        }
        else
        {
            entity.Fail(input.ActualValue, input.Notes);
        }
        await _reviewRepository.UpdateAsync(entity);
        return new QualityReviewMapper().Map(entity);
    }

    // ─── Quality Action ────────────────────────────────────────────────
    public async Task<QualityActionDto> GetActionAsync(Guid id)
    {
        var entity = await _actionRepository.GetAsync(id);
        return new QualityActionMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityActionDto>> GetActionListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _actionRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityActionDto>(
            totalCount,
            entities.Select(e => new QualityActionMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityActionDto> CreateActionAsync(CreateUpdateQualityActionDto input)
    {
        var entity = new QualityAction(GuidGenerator.Create(), input.CompanyId, input.ActionType, input.ProblemDescription, CurrentTenant.Id)
        {
            RelatedQualityGoalId = input.RelatedQualityGoalId,
            RelatedQualityReviewId = input.RelatedQualityReviewId,
            RelatedProcedureId = input.RelatedProcedureId,
            RelatedFeedbackId = input.RelatedFeedbackId,
            AssignedUserId = input.AssignedUserId
        };

        if (input.Resolutions != null)
        {
            foreach (var res in input.Resolutions)
            {
                entity.AddResolution(new QualityActionResolution(GuidGenerator.Create(), entity.Id, res.Problem, res.ResolutionDetails));
            }
        }

        await _actionRepository.InsertAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

    public async Task<QualityActionDto> UpdateActionAsync(Guid id, CreateUpdateQualityActionDto input)
    {
        var entity = await _actionRepository.GetAsync(id);
        entity.CompanyId = input.CompanyId;
        entity.ActionType = input.ActionType;
        entity.ProblemDescription = input.ProblemDescription;
        entity.RelatedQualityGoalId = input.RelatedQualityGoalId;
        entity.RelatedQualityReviewId = input.RelatedQualityReviewId;
        entity.RelatedProcedureId = input.RelatedProcedureId;
        entity.RelatedFeedbackId = input.RelatedFeedbackId;
        entity.AssignedUserId = input.AssignedUserId;

        entity.ClearResolutions();
        if (input.Resolutions != null)
        {
            foreach (var res in input.Resolutions)
            {
                entity.AddResolution(new QualityActionResolution(GuidGenerator.Create(), entity.Id, res.Problem, res.ResolutionDetails));
            }
        }

        await _actionRepository.UpdateAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

    public async Task<QualityActionDto> ResolveActionAsync(Guid id, ResolveQualityActionDto input)
    {
        var entity = await _actionRepository.GetAsync(id);
        entity.Resolve(input.Resolution);
        await _actionRepository.UpdateAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

    public async Task<QualityActionDto> CloseActionAsync(Guid id)
    {
        var entity = await _actionRepository.GetAsync(id);
        entity.Close();
        await _actionRepository.UpdateAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

    // ─── Quality Procedure ─────────────────────────────────────────────
    public async Task<QualityProcedureDto> GetProcedureAsync(Guid id)
    {
        var entity = await _procedureRepository.GetAsync(id);
        return new QualityProcedureMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityProcedureDto>> GetProcedureListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _procedureRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityProcedureDto>(
            totalCount,
            entities.Select(e => new QualityProcedureMapper().Map(e)).ToList()
        );
    }

    public async Task<List<QualityProcedureDto>> GetProcedureTreeAsync()
    {
        var query = await _procedureRepository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Sequence).ThenBy(x => x.Name));
        return entities.Select(e => new QualityProcedureMapper().Map(e)).ToList();
    }

    public async Task<QualityProcedureDto> CreateProcedureAsync(CreateUpdateQualityProcedureDto input)
    {
        var entity = new QualityProcedure(GuidGenerator.Create(), input.Name, input.ParentQualityProcedureId, CurrentTenant.Id)
        {
            IsGroup = input.IsGroup,
            Description = input.Description,
            ProcessOwner = input.ProcessOwner,
            Sequence = input.Sequence
        };

        if (input.Steps != null)
        {
            foreach (var step in input.Steps)
            {
                entity.AddStep(new QualityProcedureStep(GuidGenerator.Create(), entity.Id, step.Description, step.Sequence, step.ChildProcedureId));
            }
        }

        await _procedureRepository.InsertAsync(entity);
        return new QualityProcedureMapper().Map(entity);
    }

    public async Task<QualityProcedureDto> UpdateProcedureAsync(Guid id, CreateUpdateQualityProcedureDto input)
    {
        var entity = await _procedureRepository.GetAsync(id);
        entity.Name = input.Name;
        entity.ParentQualityProcedureId = input.ParentQualityProcedureId;
        entity.IsGroup = input.IsGroup;
        entity.Description = input.Description;
        entity.ProcessOwner = input.ProcessOwner;
        entity.Sequence = input.Sequence;

        entity.ClearSteps();
        if (input.Steps != null)
        {
            foreach (var step in input.Steps)
            {
                entity.AddStep(new QualityProcedureStep(GuidGenerator.Create(), entity.Id, step.Description, step.Sequence, step.ChildProcedureId));
            }
        }

        await _procedureRepository.UpdateAsync(entity);
        return new QualityProcedureMapper().Map(entity);
    }

    public async Task DeleteProcedureAsync(Guid id)
    {
        await _procedureRepository.DeleteAsync(id);
    }

    // ─── Non-Conformance ───────────────────────────────────────────────
    public async Task<NonConformanceDto> GetNonConformanceAsync(Guid id)
    {
        var entity = await _nonConformanceRepository.GetAsync(id);
        return new NonConformanceMapper().Map(entity);
    }

    public async Task<PagedResultDto<NonConformanceDto>> GetNonConformanceListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _nonConformanceRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<NonConformanceDto>(
            totalCount,
            entities.Select(e => new NonConformanceMapper().Map(e)).ToList()
        );
    }

    public async Task<NonConformanceDto> CreateNonConformanceAsync(CreateUpdateNonConformanceDto input)
    {
        var entity = new NonConformance(GuidGenerator.Create(), input.CompanyId, input.Subject, input.ProcedureId, CurrentTenant.Id)
        {
            ProcessOwner = input.ProcessOwner,
            Details = input.Details,
            CorrectiveAction = input.CorrectiveAction,
            PreventiveAction = input.PreventiveAction
        };
        await _nonConformanceRepository.InsertAsync(entity);
        return new NonConformanceMapper().Map(entity);
    }

    public async Task<NonConformanceDto> UpdateNonConformanceAsync(Guid id, CreateUpdateNonConformanceDto input)
    {
        var entity = await _nonConformanceRepository.GetAsync(id);
        entity.Subject = input.Subject;
        entity.ProcedureId = input.ProcedureId;
        entity.ProcessOwner = input.ProcessOwner;
        entity.Details = input.Details;
        entity.CorrectiveAction = input.CorrectiveAction;
        entity.PreventiveAction = input.PreventiveAction;

        await _nonConformanceRepository.UpdateAsync(entity);
        return new NonConformanceMapper().Map(entity);
    }

    public async Task<NonConformanceDto> ResolveNonConformanceAsync(Guid id, ResolveNonConformanceDto input)
    {
        var entity = await _nonConformanceRepository.GetAsync(id);
        entity.Resolve(input.CorrectiveAction, input.PreventiveAction);
        await _nonConformanceRepository.UpdateAsync(entity);
        return new NonConformanceMapper().Map(entity);
    }

    public async Task<NonConformanceDto> CancelNonConformanceAsync(Guid id)
    {
        var entity = await _nonConformanceRepository.GetAsync(id);
        entity.Cancel();
        await _nonConformanceRepository.UpdateAsync(entity);
        return new NonConformanceMapper().Map(entity);
    }

    // ─── Quality Meeting ───────────────────────────────────────────────
    public async Task<QualityMeetingDto> GetMeetingAsync(Guid id)
    {
        var entity = await _meetingRepository.GetAsync(id);
        return new QualityMeetingMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityMeetingDto>> GetMeetingListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _meetingRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.MeetingDate)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityMeetingDto>(
            totalCount,
            entities.Select(e => new QualityMeetingMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityMeetingDto> CreateMeetingAsync(CreateUpdateQualityMeetingDto input)
    {
        var entity = new QualityMeeting(GuidGenerator.Create(), input.CompanyId, input.MeetingDate, input.Chairperson, CurrentTenant.Id)
        {
            Attendees = input.Attendees
        };

        if (input.Agendas != null)
        {
            foreach (var a in input.Agendas)
            {
                entity.AddAgenda(new QualityMeetingAgenda(GuidGenerator.Create(), entity.Id, a));
            }
        }

        if (input.Minutes != null)
        {
            foreach (var m in input.Minutes)
            {
                entity.AddMinute(new QualityMeetingMinutes(GuidGenerator.Create(), entity.Id, m.Discussion, m.ActionPlan, m.AssignedUserId));
            }
        }

        await _meetingRepository.InsertAsync(entity);
        return new QualityMeetingMapper().Map(entity);
    }

    public async Task<QualityMeetingDto> CloseMeetingAsync(Guid id)
    {
        var entity = await _meetingRepository.GetAsync(id);
        entity.Close();
        await _meetingRepository.UpdateAsync(entity);
        return new QualityMeetingMapper().Map(entity);
    }

    // ─── Quality Feedback Template & Feedback ─────────────────────────
    public async Task<QualityFeedbackTemplateDto> GetFeedbackTemplateAsync(Guid id)
    {
        var entity = await _feedbackTemplateRepository.GetAsync(id);
        return new QualityFeedbackTemplateMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityFeedbackTemplateDto>> GetFeedbackTemplateListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _feedbackTemplateRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.TemplateName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityFeedbackTemplateDto>(
            totalCount,
            entities.Select(e => new QualityFeedbackTemplateMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityFeedbackTemplateDto> CreateFeedbackTemplateAsync(CreateUpdateQualityFeedbackTemplateDto input)
    {
        var entity = new QualityFeedbackTemplate(GuidGenerator.Create(), input.TemplateName, CurrentTenant.Id);
        if (input.Parameters != null)
        {
            foreach (var p in input.Parameters)
            {
                entity.AddParameter(new QualityFeedbackTemplateParameter(GuidGenerator.Create(), entity.Id, p));
            }
        }
        await _feedbackTemplateRepository.InsertAsync(entity);
        return new QualityFeedbackTemplateMapper().Map(entity);
    }

    public async Task<QualityFeedbackDto> GetFeedbackAsync(Guid id)
    {
        var entity = await _feedbackRepository.GetAsync(id);
        return new QualityFeedbackMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityFeedbackDto>> GetFeedbackListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _feedbackRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount)
        );
        return new PagedResultDto<QualityFeedbackDto>(
            totalCount,
            entities.Select(e => new QualityFeedbackMapper().Map(e)).ToList()
        );
    }

    public async Task<QualityFeedbackDto> CreateFeedbackAsync(CreateQualityFeedbackDto input)
    {
        var entity = new QualityFeedback(GuidGenerator.Create(), input.CompanyId, input.DocumentType, input.DocumentName, input.TemplateId, CurrentTenant.Id)
        {
            Remarks = input.Remarks
        };

        if (input.Parameters != null)
        {
            foreach (var p in input.Parameters)
            {
                entity.AddParameter(new QualityFeedbackParameter(GuidGenerator.Create(), entity.Id, p.Parameter, p.Rating, p.Remarks));
            }
        }

        await _feedbackRepository.InsertAsync(entity);
        return new QualityFeedbackMapper().Map(entity);
    }
}
