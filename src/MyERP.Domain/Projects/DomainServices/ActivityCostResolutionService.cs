using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Projects.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Projects.DomainServices;

/// <summary>
/// Resolves billing and costing rates for timesheet entries.
/// Uses 2-level fallback: employee-specific ActivityCost → ActivityType global rates.
/// 
/// Source: erpnext/projects/doctype/timesheet/timesheet.py
/// </summary>
public class ActivityCostResolutionService : DomainService
{
    private readonly IRepository<ActivityCost, Guid> _activityCostRepository;
    private readonly IRepository<ActivityType, Guid> _activityTypeRepository;

    public ActivityCostResolutionService(
        IRepository<ActivityCost, Guid> activityCostRepository,
        IRepository<ActivityType, Guid> activityTypeRepository)
    {
        _activityCostRepository = activityCostRepository;
        _activityTypeRepository = activityTypeRepository;
    }

    /// <summary>
    /// Resolve the billing and costing rates for an employee + activity type combination.
    /// Level 1: Employee-specific ActivityCost (highest priority)
    /// Level 2: ActivityType global defaults (fallback)
    /// </summary>
    public async Task<(decimal BillingRate, decimal CostingRate)> ResolveRatesAsync(
        Guid employeeId, Guid activityTypeId)
    {
        // Level 1: Employee-specific rate
        var costQuery = await _activityCostRepository.GetQueryableAsync();
        var employeeCost = costQuery
            .FirstOrDefault(c => c.EmployeeId == employeeId && c.ActivityTypeId == activityTypeId);

        if (employeeCost != null)
            return (employeeCost.BillingRate, employeeCost.CostingRate);

        // Level 2: ActivityType global default
        var activityType = await _activityTypeRepository.FindAsync(activityTypeId);
        if (activityType != null)
            return (activityType.DefaultBillingRate, activityType.DefaultCostingRate);

        // No rate found
        return (0m, 0m);
    }
}
