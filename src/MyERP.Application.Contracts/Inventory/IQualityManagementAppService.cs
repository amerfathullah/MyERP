using System;
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

    // Quality Action
    Task<QualityActionDto> GetActionAsync(Guid id);
    Task<PagedResultDto<QualityActionDto>> GetActionListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityActionDto> CreateActionAsync(CreateUpdateQualityActionDto input);
    Task<QualityActionDto> ResolveActionAsync(Guid id, ResolveQualityActionDto input);

    // Quality Review
    Task<QualityReviewDto> GetReviewAsync(Guid id);
    Task<PagedResultDto<QualityReviewDto>> GetReviewListAsync(PagedAndSortedResultRequestDto input);
    Task<QualityReviewDto> CreateReviewAsync(CreateQualityReviewDto input);
}
