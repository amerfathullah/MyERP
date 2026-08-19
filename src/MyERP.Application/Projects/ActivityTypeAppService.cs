using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Projects;

/// <summary>
/// Manages Activity Types and Activity Costs (employee-specific rate overrides).
/// Activity Types define the categories of work for timesheet entries and their default rates.
/// </summary>
[Authorize(MyERPPermissions.Projects.Default)]
public class ActivityTypeAppService : ApplicationService, IActivityTypeAppService
{
    private readonly IRepository<ActivityType, Guid> _repository;
    private readonly IRepository<ActivityCost, Guid> _costRepository;

    public ActivityTypeAppService(
        IRepository<ActivityType, Guid> repository,
        IRepository<ActivityCost, Guid> costRepository)
    {
        _repository = repository;
        _costRepository = costRepository;
    }

    public async Task<List<ActivityTypeDto>> GetListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        return query.OrderBy(a => a.Name).ToList()
            .Select(ObjectMapper.Map<ActivityType, ActivityTypeDto>).ToList();
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ActivityTypeDto> CreateAsync(CreateActivityTypeDto input)
    {
        if (input.DefaultBillingRate < 0 || input.DefaultCostingRate < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "DefaultBillingRate/DefaultCostingRate");
        }

        var entity = new ActivityType(
            GuidGenerator.Create(),
            input.Name,
            input.DefaultBillingRate,
            input.DefaultCostingRate,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ActivityType", entity.Id,
            "Created", Guid.Empty,
            entity.Name, "Draft", "Active", CurrentUser.Id,
            $"Activity type '{entity.Name}' created (Billing: {entity.DefaultBillingRate:C}, Costing: {entity.DefaultCostingRate:C})", CurrentTenant.Id));

        return ObjectMapper.Map<ActivityType, ActivityTypeDto>(entity);
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ActivityTypeDto> UpdateAsync(Guid id, UpdateActivityTypeDto input)
    {
        if (input.DefaultBillingRate < 0 || input.DefaultCostingRate < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "DefaultBillingRate/DefaultCostingRate");
        }

        var entity = await _repository.GetAsync(id);
        entity.DefaultBillingRate = input.DefaultBillingRate;
        entity.DefaultCostingRate = input.DefaultCostingRate;
        entity.IsEnabled = input.IsEnabled;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ActivityType", entity.Id,
            "Updated", Guid.Empty,
            entity.Name, "Active", "Active", CurrentUser.Id,
            $"Activity type '{entity.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<ActivityType, ActivityTypeDto>(entity);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);

    // === Activity Cost (employee-specific rate overrides) ===

    /// <summary>
    /// Get all employee-specific cost overrides for an activity type.
    /// </summary>
    public async Task<List<ActivityCostDto>> GetCostsForActivityAsync(Guid activityTypeId)
    {
        var query = await _costRepository.GetQueryableAsync();
        return query.Where(c => c.ActivityTypeId == activityTypeId).ToList()
            .Select(ObjectMapper.Map<ActivityCost, ActivityCostDto>).ToList();
    }

    /// <summary>
    /// Set employee-specific rate override.
    /// </summary>
    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ActivityCostDto> SetEmployeeCostAsync(SetActivityCostDto input)
    {
        if (input.BillingRate < 0 || input.CostingRate < 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "BillingRate/CostingRate");
        }

        var query = await _costRepository.GetQueryableAsync();
        var existing = query.FirstOrDefault(c =>
            c.EmployeeId == input.EmployeeId && c.ActivityTypeId == input.ActivityTypeId);

        if (existing != null)
        {
            existing.BillingRate = input.BillingRate;
            existing.CostingRate = input.CostingRate;
            await _costRepository.UpdateAsync(existing);

            var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                GuidGenerator.Create(), "ActivityCost", existing.Id,
                "Updated", Guid.Empty,
                existing.Id.ToString()[..8], "Active", "Active", CurrentUser.Id,
                $"Activity cost override updated for employee {input.EmployeeId}", CurrentTenant.Id));

            return ObjectMapper.Map<ActivityCost, ActivityCostDto>(existing);
        }

        var cost = new ActivityCost(
            GuidGenerator.Create(),
            input.EmployeeId,
            input.ActivityTypeId,
            input.BillingRate,
            input.CostingRate,
            CurrentTenant.Id);

        await _costRepository.InsertAsync(cost);

        var logRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await logRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ActivityCost", cost.Id,
            "Created", Guid.Empty,
            cost.Id.ToString()[..8], "Draft", "Active", CurrentUser.Id,
            $"Activity cost override created for employee {input.EmployeeId}", CurrentTenant.Id));

        return ObjectMapper.Map<ActivityCost, ActivityCostDto>(cost);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteCostAsync(Guid id) => await _costRepository.DeleteAsync(id);
}
