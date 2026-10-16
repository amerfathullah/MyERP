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
/// Job Card Summary report — tracks job card execution, status, and time logs across Work Orders.
/// ERPNext equivalent: manufacturing/report/job_card_summary/job_card_summary.py
/// 
/// Key rules per ERPNext PR #60656 (commit 114526ddec):
/// 1. Row status preserves stored JobCardStatus (does NOT overwrite non-completed cards to "Open").
/// 2. Chart labels break filter period into monthly intervals; datasets initialized with 0 for every period.
/// 3. Posting date filtering is inclusive: fromDate <= postingDate <= toDate.
/// 4. Chart datasets group cards into "Open" (all non-completed) and "Completed".
/// </summary>
[Authorize(MyERPPermissions.Manufacturing.Default)]
public class JobCardSummaryAppService : ApplicationService, IJobCardSummaryAppService
{
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Workstation, Guid> _workstationRepository;
    private readonly IRepository<Operation, Guid> _operationRepository;

    public JobCardSummaryAppService(
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Workstation, Guid> workstationRepository,
        IRepository<Operation, Guid> operationRepository)
    {
        _jobCardRepository = jobCardRepository;
        _workOrderRepository = workOrderRepository;
        _itemRepository = itemRepository;
        _workstationRepository = workstationRepository;
        _operationRepository = operationRepository;
    }

    public async Task<JobCardSummaryDto> GetSummaryAsync(JobCardSummaryRequestDto input)
    {
        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;

        var jcQuery = await _jobCardRepository.WithDetailsAsync(j => j.TimeLogs);
        var query = jcQuery
            .Where(j => j.CompanyId == input.CompanyId)
            .Where(j => j.CreationTime.Date >= fromDate && j.CreationTime.Date <= toDate);

        if (input.Status.HasValue)
        {
            query = query.Where(j => j.Status == input.Status.Value);
        }

        if (input.WorkOrderIds != null && input.WorkOrderIds.Count > 0)
        {
            query = query.Where(j => input.WorkOrderIds.Contains(j.WorkOrderId));
        }

        if (input.WorkstationId.HasValue)
        {
            query = query.Where(j => j.WorkstationId == input.WorkstationId.Value);
        }

        if (input.OperationId.HasValue)
        {
            query = query.Where(j => j.OperationId == input.OperationId.Value);
        }

        var jobCards = query.OrderByDescending(j => j.CreationTime).ToList();

        if (jobCards.Count == 0)
        {
            var emptyChart = BuildChartData(new List<JobCard>(), fromDate, toDate);
            return new JobCardSummaryDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Rows = new(),
                Chart = emptyChart,
                TotalJobCards = 0,
                TotalCompletedQty = 0,
                TotalTimeInMins = 0
            };
        }

        var workOrderIds = jobCards.Select(j => j.WorkOrderId).Distinct().ToList();
        var workOrders = await _workOrderRepository.GetListAsync(wo => workOrderIds.Contains(wo.Id));
        var workOrderLookup = workOrders.ToDictionary(wo => wo.Id);

        // Filter by ProductionItemIds if specified
        if (input.ProductionItemIds != null && input.ProductionItemIds.Count > 0)
        {
            jobCards = jobCards.Where(j =>
            {
                var itemId = j.FinishedGoodItemId ?? (workOrderLookup.TryGetValue(j.WorkOrderId, out var wo) ? wo.ItemId : Guid.Empty);
                return input.ProductionItemIds.Contains(itemId);
            }).ToList();
        }

        var itemIds = jobCards.Select(j => j.FinishedGoodItemId ?? (workOrderLookup.TryGetValue(j.WorkOrderId, out var wo) ? wo.ItemId : Guid.Empty))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var items = await _itemRepository.GetListAsync(i => itemIds.Contains(i.Id));
        var itemLookup = items.ToDictionary(i => i.Id);

        var workstationIds = jobCards.Where(j => j.WorkstationId.HasValue).Select(j => j.WorkstationId!.Value).Distinct().ToList();
        var workstations = await _workstationRepository.GetListAsync(w => workstationIds.Contains(w.Id));
        var workstationLookup = workstations.ToDictionary(w => w.Id);

        var operationIds = jobCards.Select(j => j.OperationId).Distinct().ToList();
        var operations = await _operationRepository.GetListAsync(o => operationIds.Contains(o.Id));
        var operationLookup = operations.ToDictionary(o => o.Id);

        var rows = new List<JobCardSummaryRowDto>();
        foreach (var jc in jobCards)
        {
            workOrderLookup.TryGetValue(jc.WorkOrderId, out var wo);
            var prodItemId = jc.FinishedGoodItemId ?? (wo?.ItemId ?? Guid.Empty);
            itemLookup.TryGetValue(prodItemId, out var item);

            workstationLookup.TryGetValue(jc.WorkstationId ?? Guid.Empty, out var ws);
            operationLookup.TryGetValue(jc.OperationId, out var op);

            DateTime? minFromTime = jc.TimeLogs.Count > 0 ? jc.TimeLogs.Min(tl => tl.FromTime) : null;
            DateTime? maxToTime = jc.TimeLogs.Count > 0 ? jc.TimeLogs.Max(tl => tl.ToTime) : null;

            // Per ERPNext PR #60656 (commit 114526ddec):
            // Row status is NOT overwritten to "Open" for non-completed cards. It preserves stored status.
            rows.Add(new JobCardSummaryRowDto
            {
                Id = jc.Id,
                PostingDate = jc.CreationTime.Date,
                Status = jc.Status.ToString(),
                StatusCode = jc.Status,
                WorkOrderId = jc.WorkOrderId,
                WorkOrderNumber = wo?.WorkOrderNumber ?? jc.WorkOrderId.ToString()[..8],
                ProductionItemId = prodItemId,
                ProductionItemCode = item?.ItemCode ?? prodItemId.ToString()[..8],
                ProductionItemName = item?.ItemName ?? "Unknown Item",
                WorkstationId = jc.WorkstationId,
                WorkstationName = ws?.Name,
                OperationId = jc.OperationId,
                OperationName = op?.Name,
                TotalCompletedQty = jc.CompletedQty,
                FromTime = minFromTime,
                ToTime = maxToTime,
                TotalTimeInMins = jc.TotalTimeInMins
            });
        }

        var chart = BuildChartData(jobCards, fromDate, toDate);

        return new JobCardSummaryDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            Chart = chart,
            TotalJobCards = rows.Count,
            TotalCompletedQty = rows.Sum(r => r.TotalCompletedQty),
            TotalTimeInMins = rows.Sum(r => r.TotalTimeInMins)
        };
    }

    private static JobCardSummaryChartDto BuildChartData(List<JobCard> jobCards, DateTime fromDate, DateTime toDate)
    {
        var labels = new List<string>();
        var openCounts = new List<int>();
        var completedCounts = new List<int>();

        // Generate monthly date ranges
        var cur = new DateTime(fromDate.Year, fromDate.Month, 1);
        var endMonth = new DateTime(toDate.Year, toDate.Month, 1);

        while (cur <= endMonth)
        {
            var rangeStart = cur < fromDate ? fromDate : cur;
            var daysInMonth = DateTime.DaysInMonth(cur.Year, cur.Month);
            var monthLastDay = new DateTime(cur.Year, cur.Month, daysInMonth);
            var rangeEnd = monthLastDay > toDate ? toDate : monthLastDay;

            var label = cur.ToString("MMM yyyy");
            labels.Add(label);

            // Per ERPNext PR #60656:
            // 1. Missing period buckets initialized to 0.
            // 2. Date boundary check is inclusive: rangeStart <= posting_date <= rangeEnd.
            var inPeriodCards = jobCards.Where(j =>
            {
                var dt = j.CreationTime.Date;
                return dt >= rangeStart && dt <= rangeEnd;
            }).ToList();

            var completed = inPeriodCards.Count(j => j.Status == JobCardStatus.Completed);
            var open = inPeriodCards.Count(j => j.Status != JobCardStatus.Completed);

            openCounts.Add(open);
            completedCounts.Add(completed);

            cur = cur.AddMonths(1);
        }

        return new JobCardSummaryChartDto
        {
            Labels = labels,
            Datasets = new List<JobCardChartDatasetDto>
            {
                new() { Name = "Open", Values = openCounts },
                new() { Name = "Completed", Values = completedCounts }
            }
        };
    }
}
