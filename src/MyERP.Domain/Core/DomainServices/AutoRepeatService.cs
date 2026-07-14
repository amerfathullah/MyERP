using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Processes due auto-repeat configurations and generates document copies.
/// Called by the nightly background worker or on-demand.
/// 
/// Per ERPNext:
/// - Creates documents as Draft (never auto-submits)
/// - Updates date fields on the copy to the schedule date
/// - Clears submission status, amended_from, links to other docs
/// - Skips if identical schedule_date already has a document (dedup)
/// - Auto-disables when end_date is passed
/// 
/// Source: frappe/automation/doctype/auto_repeat/auto_repeat.py
/// </summary>
public class AutoRepeatService : DomainService
{
    private readonly IRepository<AutoRepeat, Guid> _repository;

    public AutoRepeatService(IRepository<AutoRepeat, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get all auto-repeat configurations that are due for execution.
    /// </summary>
    public async Task<List<AutoRepeat>> GetDueAutoRepeatsAsync(DateTime asOfDate, Guid? companyId = null)
    {
        var query = await _repository.GetQueryableAsync();
        var dueItems = query
            .Where(ar => ar.IsEnabled && ar.NextScheduleDate <= asOfDate)
            .Where(ar => !ar.EndDate.HasValue || ar.EndDate.Value >= asOfDate);

        if (companyId.HasValue)
            dueItems = dueItems.Where(ar => ar.CompanyId == companyId.Value);

        return dueItems.OrderBy(ar => ar.NextScheduleDate).ToList();
    }

    /// <summary>
    /// Mark an auto-repeat as having generated a document and advance the schedule.
    /// </summary>
    public async Task RecordGenerationAsync(Guid autoRepeatId, DateTime generatedAt)
    {
        var autoRepeat = await _repository.GetAsync(autoRepeatId);
        autoRepeat.RecordGeneration(generatedAt);
        await _repository.UpdateAsync(autoRepeat);
    }

    /// <summary>
    /// Disable all auto-repeats that have passed their end date.
    /// Called during nightly processing.
    /// </summary>
    public async Task<int> DisableExpiredAsync(DateTime asOfDate)
    {
        var query = await _repository.GetQueryableAsync();
        var expired = query
            .Where(ar => ar.IsEnabled && ar.EndDate.HasValue && ar.EndDate.Value < asOfDate)
            .ToList();

        foreach (var ar in expired)
        {
            ar.Disable();
            await _repository.UpdateAsync(ar);
        }

        return expired.Count;
    }
}
