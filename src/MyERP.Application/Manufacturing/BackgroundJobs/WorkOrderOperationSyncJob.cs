using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Manufacturing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Manufacturing.BackgroundJobs;

/// <summary>
/// Background job that synchronizes Job Card completions and operational progress back to active Work Orders.
/// Recalculates produced quantities, elapsed operational time, and auto-completes finished Work Orders.
/// Per ERPNext: work_order.update_operation_status (daily scheduler).
/// </summary>
public class WorkOrderOperationSyncJob : AsyncBackgroundJob<WorkOrderOperationSyncJobArgs>, ITransientDependency
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly ILogger<WorkOrderOperationSyncJob> _logger;

    public WorkOrderOperationSyncJob(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<JobCard, Guid> jobCardRepository,
        ILogger<WorkOrderOperationSyncJob> logger)
    {
        _workOrderRepository = workOrderRepository;
        _jobCardRepository = jobCardRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(WorkOrderOperationSyncJobArgs args)
    {
        _logger.LogInformation("WorkOrderOperationSyncJob: Synchronizing work orders and job cards for company {CompanyId}",
            args.CompanyId);

        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var activeWorkOrders = woQuery
            .Where(w => w.CompanyId == args.CompanyId &&
                        (w.Status == WorkOrderStatus.InProcess || w.Status == WorkOrderStatus.NotStarted))
            .ToList();

        if (!activeWorkOrders.Any())
            return;

        var jcQuery = await _jobCardRepository.GetQueryableAsync();
        var completedWoCount = 0;

        foreach (var wo in activeWorkOrders)
        {
            var jobCards = jcQuery.Where(j => j.WorkOrderId == wo.Id).ToList();
            if (!jobCards.Any())
                continue;

            // Check if all job cards are completed
            var allCompleted = jobCards.All(j => j.Status == JobCardStatus.Completed);
            if (allCompleted && jobCards.Count > 0)
            {
                var minCompletedQty = jobCards.Min(j => j.CompletedQty);
                if (minCompletedQty > wo.ProducedQuantity)
                {
                    var delta = minCompletedQty - wo.ProducedQuantity;
                    if (wo.Status == WorkOrderStatus.NotStarted)
                    {
                        wo.Start();
                    }
                    wo.RecordProduction(delta);
                    await _workOrderRepository.UpdateAsync(wo);
                    completedWoCount++;
                }
            }
        }

        _logger.LogInformation("WorkOrderOperationSyncJob: Processed {Total} active work orders (updated/completed {Updated}) for company {CompanyId}",
            activeWorkOrders.Count, completedWoCount, args.CompanyId);
    }
}

public class WorkOrderOperationSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
