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
public class ActivityTypeAppService : ApplicationService
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
        return query.OrderBy(a => a.Name).ToList().Select(a => new ActivityTypeDto
        {
            Id = a.Id,
            Name = a.Name,
            DefaultBillingRate = a.DefaultBillingRate,
            DefaultCostingRate = a.DefaultCostingRate,
            IsEnabled = a.IsEnabled
        }).ToList();
    }

    [Authorize(MyERPPermissions.Projects.Create)]
    public async Task<ActivityTypeDto> CreateAsync(CreateActivityTypeDto input)
    {
        var entity = new ActivityType(
            GuidGenerator.Create(),
            input.Name,
            input.DefaultBillingRate,
            input.DefaultCostingRate,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new ActivityTypeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DefaultBillingRate = entity.DefaultBillingRate,
            DefaultCostingRate = entity.DefaultCostingRate,
            IsEnabled = entity.IsEnabled
        };
    }

    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ActivityTypeDto> UpdateAsync(Guid id, UpdateActivityTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.DefaultBillingRate = input.DefaultBillingRate;
        entity.DefaultCostingRate = input.DefaultCostingRate;
        entity.IsEnabled = input.IsEnabled;
        await _repository.UpdateAsync(entity);
        return new ActivityTypeDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DefaultBillingRate = entity.DefaultBillingRate,
            DefaultCostingRate = entity.DefaultCostingRate,
            IsEnabled = entity.IsEnabled
        };
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
            .Select(c => new ActivityCostDto
            {
                Id = c.Id,
                EmployeeId = c.EmployeeId,
                ActivityTypeId = c.ActivityTypeId,
                BillingRate = c.BillingRate,
                CostingRate = c.CostingRate
            }).ToList();
    }

    /// <summary>
    /// Set employee-specific rate override.
    /// </summary>
    [Authorize(MyERPPermissions.Projects.Edit)]
    public async Task<ActivityCostDto> SetEmployeeCostAsync(SetActivityCostDto input)
    {
        var query = await _costRepository.GetQueryableAsync();
        var existing = query.FirstOrDefault(c =>
            c.EmployeeId == input.EmployeeId && c.ActivityTypeId == input.ActivityTypeId);

        if (existing != null)
        {
            existing.BillingRate = input.BillingRate;
            existing.CostingRate = input.CostingRate;
            await _costRepository.UpdateAsync(existing);
            return MapCostToDto(existing);
        }

        var cost = new ActivityCost(
            GuidGenerator.Create(),
            input.EmployeeId,
            input.ActivityTypeId,
            input.BillingRate,
            input.CostingRate,
            CurrentTenant.Id);

        await _costRepository.InsertAsync(cost);
        return MapCostToDto(cost);
    }

    [Authorize(MyERPPermissions.Projects.Delete)]
    public async Task DeleteCostAsync(Guid id) => await _costRepository.DeleteAsync(id);

    private static ActivityCostDto MapCostToDto(ActivityCost c) => new()
    {
        Id = c.Id,
        EmployeeId = c.EmployeeId,
        ActivityTypeId = c.ActivityTypeId,
        BillingRate = c.BillingRate,
        CostingRate = c.CostingRate
    };
}

#region DTOs

public class ActivityTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateActivityTypeDto
{
    public string Name { get; set; } = null!;
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
}

public class UpdateActivityTypeDto
{
    public decimal DefaultBillingRate { get; set; }
    public decimal DefaultCostingRate { get; set; }
    public bool IsEnabled { get; set; }
}

public class ActivityCostDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ActivityTypeId { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
}

public class SetActivityCostDto
{
    public Guid EmployeeId { get; set; }
    public Guid ActivityTypeId { get; set; }
    public decimal BillingRate { get; set; }
    public decimal CostingRate { get; set; }
}

#endregion
