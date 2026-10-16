using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing;

/// <summary>
/// Cost of Poor Quality Report — tracks rework and repair costs from corrective Job Cards and operations.
/// ERPNext equivalent: manufacturing/report/cost_of_poor_quality_report/cost_of_poor_quality_report.py
/// Formula: operating_cost = hour_rate * total_time_in_mins / 60
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class CostOfPoorQualityReportAppService : ApplicationService, ICostOfPoorQualityReportAppService
{
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly IRepository<Operation, Guid> _operationRepository;
    private readonly IRepository<Workstation, Guid> _workstationRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public CostOfPoorQualityReportAppService(
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<Operation, Guid> operationRepository,
        IRepository<Workstation, Guid> workstationRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _jobCardRepository = jobCardRepository;
        _operationRepository = operationRepository;
        _workstationRepository = workstationRepository;
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
    }

    public async Task<CostOfPoorQualityReportDto> GetReportAsync(CostOfPoorQualityReportRequestDto input)
    {
        // 1. Identify corrective operations
        var correctiveOps = await _operationRepository.GetListAsync(o => o.IsCorrectiveOperation);
        var correctiveOpIds = correctiveOps.Select(o => o.Id).ToHashSet();

        // 2. Query JobCards with TimeLogs
        var jcQuery = await _jobCardRepository.WithDetailsAsync(j => j.TimeLogs);
        var query = jcQuery
            .Where(j => j.CompanyId == input.CompanyId)
            .Where(j => j.Status != JobCardStatus.Cancelled)
            .Where(j => j.IsCorrective || correctiveOpIds.Contains(j.OperationId));

        if (input.OperationId.HasValue)
        {
            query = query.Where(j => j.OperationId == input.OperationId.Value);
        }

        if (input.WorkOrderId.HasValue)
        {
            query = query.Where(j => j.WorkOrderId == input.WorkOrderId.Value);
        }

        if (input.WorkstationId.HasValue)
        {
            query = query.Where(j => j.WorkstationId == input.WorkstationId.Value);
        }

        if (input.FromDate.HasValue)
        {
            var from = input.FromDate.Value.Date;
            query = query.Where(j => j.TimeLogs.Any(tl => tl.FromTime >= from) || j.CreationTime.Date >= from);
        }

        if (input.ToDate.HasValue)
        {
            var to = input.ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(j => j.TimeLogs.Any(tl => tl.ToTime <= to) || j.CreationTime.Date <= to.Date);
        }

        var jobCards = query.OrderByDescending(j => j.CreationTime).ToList();

        if (jobCards.Count == 0)
        {
            return new CostOfPoorQualityReportDto
            {
                CompanyId = input.CompanyId,
                Rows = new(),
                TotalJobCards = 0,
                TotalTimeInMins = 0,
                TotalOperatingCost = 0
            };
        }

        // 3. Lookups: WorkOrders, Operations, Workstations, Items
        var woIds = jobCards.Select(j => j.WorkOrderId).Distinct().ToList();
        var workOrders = await _workOrderRepository.GetListAsync(wo => woIds.Contains(wo.Id));
        var woLookup = workOrders.ToDictionary(wo => wo.Id);

        // Filter by ProductionItemId if requested
        if (input.ProductionItemId.HasValue)
        {
            jobCards = jobCards.Where(j =>
            {
                var prodItemId = j.FinishedGoodItemId ?? (woLookup.TryGetValue(j.WorkOrderId, out var wo) ? wo.ItemId : Guid.Empty);
                return prodItemId == input.ProductionItemId.Value;
            }).ToList();
        }

        var opIds = jobCards.Select(j => j.OperationId).Distinct().ToList();
        var operations = await _operationRepository.GetListAsync(o => opIds.Contains(o.Id));
        var opLookup = operations.ToDictionary(o => o.Id);

        var wsIds = jobCards.Where(j => j.WorkstationId.HasValue).Select(j => j.WorkstationId!.Value).Distinct().ToList();
        var workstations = await _workstationRepository.GetListAsync(w => wsIds.Contains(w.Id));
        var wsLookup = workstations.ToDictionary(w => w.Id);

        var itemIds = jobCards.Select(j => j.FinishedGoodItemId ?? (woLookup.TryGetValue(j.WorkOrderId, out var wo) ? wo.ItemId : Guid.Empty))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemLookup = items.ToDictionary(i => i.Id);

        var rows = new List<CostOfPoorQualityReportRowDto>();

        foreach (var jc in jobCards)
        {
            woLookup.TryGetValue(jc.WorkOrderId, out var wo);
            var prodItemId = jc.FinishedGoodItemId ?? (wo?.ItemId ?? Guid.Empty);
            itemLookup.TryGetValue(prodItemId, out var item);

            opLookup.TryGetValue(jc.OperationId, out var op);
            wsLookup.TryGetValue(jc.WorkstationId ?? Guid.Empty, out var ws);

            var hourRate = ws?.HourRate ?? 0m;
            var operatingCost = Math.Round(hourRate * jc.TotalTimeInMins / 60m, 2);

            rows.Add(new CostOfPoorQualityReportRowDto
            {
                JobCardId = jc.Id,
                WorkOrderId = jc.WorkOrderId,
                WorkOrderNumber = wo?.WorkOrderNumber ?? jc.WorkOrderId.ToString()[..8],
                ProductionItemId = prodItemId,
                ProductionItemCode = item?.ItemCode ?? prodItemId.ToString()[..8],
                ProductionItemName = item?.ItemName ?? "Unknown Item",
                OperationId = jc.OperationId,
                OperationName = op?.Name ?? "Corrective Operation",
                WorkstationId = jc.WorkstationId,
                WorkstationName = ws?.Name,
                HourRate = hourRate,
                TotalTimeInMins = jc.TotalTimeInMins,
                OperatingCost = operatingCost,
                StartedAt = jc.StartedAt,
                CompletedAt = jc.CompletedAt
            });
        }

        return new CostOfPoorQualityReportDto
        {
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalJobCards = rows.Count,
            TotalTimeInMins = rows.Sum(r => r.TotalTimeInMins),
            TotalOperatingCost = rows.Sum(r => r.OperatingCost)
        };
    }
}
