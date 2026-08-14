using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Inventory;

public interface IQualityManagementAppService : IApplicationService
{
    // Quality Goal
    Task<QualityGoalDto> GetGoalAsync(Guid id);
    Task<PagedResultDto<QualityGoalDto>> GetGoalListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityGoalDto> CreateGoalAsync(CreateUpdateQualityGoalDto input);
    Task<QualityGoalDto> UpdateGoalAsync(Guid id, CreateUpdateQualityGoalDto input);
    Task DeleteGoalAsync(Guid id);

    // Quality Review
    Task<QualityReviewDto> GetReviewAsync(Guid id);
    Task<PagedResultDto<QualityReviewDto>> GetReviewListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityReviewDto> CreateReviewAsync(CreateQualityReviewDto input);
    Task<QualityReviewDto> EvaluateReviewAsync(Guid id, EvaluateQualityReviewDto input);

    // Quality Action
    Task<QualityActionDto> GetActionAsync(Guid id);
    Task<PagedResultDto<QualityActionDto>> GetActionListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityActionDto> CreateActionAsync(CreateUpdateQualityActionDto input);
    Task<QualityActionDto> UpdateActionAsync(Guid id, CreateUpdateQualityActionDto input);
    Task<QualityActionDto> ResolveActionAsync(Guid id, ResolveQualityActionDto input);
    Task<QualityActionDto> CloseActionAsync(Guid id);

    // Quality Procedure
    Task<QualityProcedureDto> GetProcedureAsync(Guid id);
    Task<PagedResultDto<QualityProcedureDto>> GetProcedureListAsync(PagedAndSortedResultRequestDto input);
    Task<List<QualityProcedureDto>> GetProcedureTreeAsync();
    Task<QualityProcedureDto> CreateProcedureAsync(CreateUpdateQualityProcedureDto input);
    Task<QualityProcedureDto> UpdateProcedureAsync(Guid id, CreateUpdateQualityProcedureDto input);
    Task DeleteProcedureAsync(Guid id);

    // Non-Conformance
    Task<NonConformanceDto> GetNonConformanceAsync(Guid id);
    Task<PagedResultDto<NonConformanceDto>> GetNonConformanceListAsync(PagedAndSortedResultRequestDto input);
    Task<NonConformanceDto> CreateNonConformanceAsync(CreateUpdateNonConformanceDto input);
    Task<NonConformanceDto> UpdateNonConformanceAsync(Guid id, CreateUpdateNonConformanceDto input);
    Task<NonConformanceDto> ResolveNonConformanceAsync(Guid id, ResolveNonConformanceDto input);
    Task<NonConformanceDto> CancelNonConformanceAsync(Guid id);

    // Quality Meeting
    Task<QualityMeetingDto> GetMeetingAsync(Guid id);
    Task<PagedResultDto<QualityMeetingDto>> GetMeetingListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityMeetingDto> CreateMeetingAsync(CreateUpdateQualityMeetingDto input);
    Task<QualityMeetingDto> CloseMeetingAsync(Guid id);

    // Quality Feedback Template & Feedback
    Task<QualityFeedbackTemplateDto> GetFeedbackTemplateAsync(Guid id);
    Task<PagedResultDto<QualityFeedbackTemplateDto>> GetFeedbackTemplateListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityFeedbackTemplateDto> CreateFeedbackTemplateAsync(CreateUpdateQualityFeedbackTemplateDto input);
    Task<QualityFeedbackDto> GetFeedbackAsync(Guid id);
    Task<PagedResultDto<QualityFeedbackDto>> GetFeedbackListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityFeedbackDto> CreateFeedbackAsync(CreateQualityFeedbackDto input);
}
