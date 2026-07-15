using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Manufacturing.DomainServices;

/// <summary>
/// Domain service for Job Card business rules.
/// Manages capacity planning, auto-assignment, and completion tracking.
/// </summary>
public class JobCardManager : DomainService
{
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly IRepository<Workstation, Guid> _workstationRepository;

    public JobCardManager(
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<Workstation, Guid> workstationRepository)
    {
        _jobCardRepository = jobCardRepository;
        _workstationRepository = workstationRepository;
    }

    /// <summary>
    /// Creates Job Cards from Work Order operations.
    /// Splits by batch_size when routing specifies batch sizes.
    /// Per ERPNext: one JC per batch × operation.
    /// </summary>
    public async Task<JobCard[]> CreateJobCardsFromWorkOrderAsync(
        WorkOrder wo, Routing routing, Guid? tenantId = null)
    {
        var jobCards = new System.Collections.Generic.List<JobCard>();
        var sequence = 0;

        foreach (var op in routing.Operations.OrderBy(o => o.SequenceId))
        {
            var batchSize = op.BatchSize > 0 ? op.BatchSize : wo.Quantity;
            var remaining = wo.Quantity;

            while (remaining > 0)
            {
                var qty = Math.Min(batchSize, remaining);
                sequence++;

                var jc = new JobCard(
                    GuidGenerator.Create(),
                    wo.CompanyId,
                    wo.Id,
                    op.OperationId,
                    qty,
                    sequence,
                    tenantId
                )
                {
                    WorkstationId = op.WorkstationId,
                    WipWarehouseId = wo.WipWarehouseId,
                    PlannedTimeInMins = op.TimeInMins * (qty / batchSize)
                };

                jobCards.Add(jc);
                remaining -= qty;
            }
        }

        foreach (var jc in jobCards)
        {
            await _jobCardRepository.InsertAsync(jc);
        }

        return jobCards.ToArray();
    }

    /// <summary>
    /// Validates workstation capacity before starting a Job Card.
    /// Per ERPNext: if num_slots >= workstation.ProductionCapacity → overlap error.
    /// </summary>
    public async Task ValidateCapacityAsync(JobCard jobCard, DateTime fromTime, DateTime toTime)
    {
        if (!jobCard.WorkstationId.HasValue) return;

        var workstation = await _workstationRepository.GetAsync(jobCard.WorkstationId.Value);
        if (workstation.ProductionCapacity <= 0) return;

        // Count overlapping time logs from other active job cards on the same workstation
        var queryable = await _jobCardRepository.GetQueryableAsync();
        var overlappingCount = queryable
            .Where(jc => jc.WorkstationId == jobCard.WorkstationId
                && jc.Id != jobCard.Id
                && jc.Status == JobCardStatus.WorkInProgress)
            .Count();

        if (overlappingCount >= workstation.ProductionCapacity)
        {
            throw new BusinessException("MyERP:10012")
                .WithData("workstation", workstation.Name)
                .WithData("capacity", workstation.ProductionCapacity)
                .WithData("current", overlappingCount);
        }
    }

    /// <summary>
    /// Calculates total completed quantity for a Work Order from all its Job Cards.
    /// Per ERPNext: total_completed = MIN(per-operation completed) when operations exist.
    /// This is the bottleneck formula — the slowest operation limits WO completion.
    /// </summary>
    public async Task<decimal> GetWorkOrderCompletedQtyAsync(Guid workOrderId)
    {
        var queryable = await _jobCardRepository.GetQueryableAsync();

        var perOperationQty = queryable
            .Where(jc => jc.WorkOrderId == workOrderId
                && jc.Status != JobCardStatus.Cancelled)
            .GroupBy(jc => jc.OperationId)
            .Select(g => g.Sum(jc => jc.CompletedQty))
            .ToList();

        if (perOperationQty.Count == 0) return 0;

        // Bottleneck: minimum across all operations
        return perOperationQty.Min();
    }
}
