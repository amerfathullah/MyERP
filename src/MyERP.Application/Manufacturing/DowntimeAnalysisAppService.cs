using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Downtime Analysis report — tracks and aggregates machine downtime, operator stoppages,
/// and root-cause reasons across production workstations.
/// ERPNext equivalent: manufacturing/report/downtime_analysis/downtime_analysis.py
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class DowntimeAnalysisAppService : ApplicationService, IDowntimeAnalysisAppService
{
    private readonly IRepository<DowntimeEntry, Guid> _downtimeRepository;
    private readonly IRepository<Workstation, Guid> _workstationRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public DowntimeAnalysisAppService(
        IRepository<DowntimeEntry, Guid> downtimeRepository,
        IRepository<Workstation, Guid> workstationRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _downtimeRepository = downtimeRepository;
        _workstationRepository = workstationRepository;
        _employeeRepository = employeeRepository;
    }

    public async Task<DowntimeAnalysisReportDto> GetReportAsync(DowntimeAnalysisRequestDto input)
    {
        var queryable = await _downtimeRepository.GetQueryableAsync();
        var query = queryable.Where(d => d.CompanyId == input.CompanyId);

        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value.Date;
            query = query.Where(d => d.FromTime >= from);
        }

        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(d => d.ToTime <= to);
        }

        if (input.WorkstationId.HasValue)
        {
            query = query.Where(d => d.WorkstationId == input.WorkstationId.Value);
        }

        if (input.OperatorId.HasValue)
        {
            query = query.Where(d => d.OperatorId == input.OperatorId.Value);
        }

        var entries = query.OrderByDescending(d => d.FromTime).ToList();

        if (entries.Count == 0)
        {
            return new DowntimeAnalysisReportDto();
        }

        var wsIds = entries.Select(e => e.WorkstationId).Distinct().ToList();
        var opIds = entries.Select(e => e.OperatorId).Distinct().ToList();

        var wsQuery = await _workstationRepository.GetQueryableAsync();
        var workstations = wsQuery.Where(w => wsIds.Contains(w.Id)).ToList();
        var wsMap = workstations.ToDictionary(w => w.Id, w => w.Name);

        var empQuery = await _employeeRepository.GetQueryableAsync();
        var employees = empQuery.Where(e => opIds.Contains(e.Id)).ToList();
        var empMap = employees.ToDictionary(e => e.Id, e => e.FullName);

        var rows = new List<DowntimeAnalysisRowDto>();
        foreach (var entry in entries)
        {
            wsMap.TryGetValue(entry.WorkstationId, out var wsName);
            empMap.TryGetValue(entry.OperatorId, out var opName);

            var hours = Math.Round(entry.DowntimeMinutes / 60m, 2);

            rows.Add(new DowntimeAnalysisRowDto
            {
                Id = entry.Id,
                WorkstationId = entry.WorkstationId,
                WorkstationName = wsName ?? "Unknown",
                OperatorId = entry.OperatorId,
                OperatorName = opName ?? "Unknown",
                FromTime = entry.FromTime,
                ToTime = entry.ToTime,
                DowntimeMinutes = entry.DowntimeMinutes,
                DowntimeHours = hours,
                StopReason = entry.StopReason,
                Remarks = entry.Remarks
            });
        }

        // Aggregate by workstation
        var wsSummaries = rows
            .GroupBy(r => r.WorkstationName)
            .Select(g => new DowntimeSummaryItemDto
            {
                Label = g.Key,
                Hours = Math.Round(g.Sum(r => r.DowntimeHours), 2),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Hours)
            .ToList();

        // Aggregate by stop reason
        var reasonSummaries = rows
            .GroupBy(r => r.StopReason)
            .Select(g => new DowntimeSummaryItemDto
            {
                Label = g.Key,
                Hours = Math.Round(g.Sum(r => r.DowntimeHours), 2),
                Count = g.Count()
            })
            .OrderByDescending(s => s.Hours)
            .ToList();

        var totalMinutes = rows.Sum(r => r.DowntimeMinutes);
        var totalHours = Math.Round(totalMinutes / 60m, 2);

        return new DowntimeAnalysisReportDto
        {
            Rows = rows,
            TotalEntries = rows.Count,
            TotalDowntimeMinutes = totalMinutes,
            TotalDowntimeHours = totalHours,
            WorkstationSummaries = wsSummaries,
            ReasonSummaries = reasonSummaries
        };
    }
}
