using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using System.Linq;

namespace MyERP.Inventory;

[Authorize]
public class QualityManagementAppService : MyERPAppService, IQualityManagementAppService
{
    private readonly IRepository<QualityGoal, Guid> _goalRepository;
    private readonly IRepository<QualityAction, Guid> _actionRepository;
    private readonly IRepository<QualityReview, Guid> _reviewRepository;

    public QualityManagementAppService(
        IRepository<QualityGoal, Guid> goalRepository,
        IRepository<QualityAction, Guid> actionRepository,
        IRepository<QualityReview, Guid> reviewRepository)
    {
        _goalRepository = goalRepository;
        _actionRepository = actionRepository;
        _reviewRepository = reviewRepository;
    }

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
            IsEnabled = input.IsEnabled
        };
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
        entity.IsEnabled = input.IsEnabled;
        await _goalRepository.UpdateAsync(entity);
        return new QualityGoalMapper().Map(entity);
    }

    public async Task DeleteGoalAsync(Guid id)
    {
        await _goalRepository.DeleteAsync(id);
    }

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
        var entity = new QualityAction(GuidGenerator.Create(), Guid.Empty, (QualityActionType)input.ActionType, input.ProblemDescription, CurrentTenant.Id)
        {
            RelatedQualityGoalId = input.RelatedQualityGoalId,
            AssignedUserId = input.AssignedUserId
        };
        await _actionRepository.InsertAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

    public async Task<QualityActionDto> ResolveActionAsync(Guid id, ResolveQualityActionDto input)
    {
        var entity = await _actionRepository.GetAsync(id);
        entity.Resolve(input.Resolution);
        await _actionRepository.UpdateAsync(entity);
        return new QualityActionMapper().Map(entity);
    }

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
            Notes = input.Notes,
            Status = QualityReviewStatus.Open
        };
        await _reviewRepository.InsertAsync(entity);
        return new QualityReviewMapper().Map(entity);
    }
}
