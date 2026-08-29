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
                    BomOperationId = op.Id,
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
    public async Task ValidateCapacityAsync(JobCard jobCard)
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

        // Per PR bde118e7cf: exclude corrective job cards from the aggregate
        // Corrective JCs represent rework/repair, not new production output
        var perOperationQty = queryable
            .Where(jc => jc.WorkOrderId == workOrderId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .GroupBy(jc => jc.OperationId)
            .Select(g => g.Sum(jc => jc.CompletedQty))
            .ToList();

        if (perOperationQty.Count == 0) return 0;

        // Bottleneck: minimum across all operations
        return perOperationQty.Min();
    }

    /// <summary>
    /// Aggregates semi-FG produced qty across split job cards for a specific operation.
    /// Per PR #5548f0726a: previously assigned single JC's manufactured_qty, overwriting prior JCs.
    /// Now sums MAX(manufactured_qty, total_completed_qty) across ALL submitted non-corrective JCs.
    /// Per PR #bde118e7cf: corrective JCs are excluded from the aggregate.
    /// </summary>
    public async Task<(decimal completedQty, decimal manufacturedQty)> GetSemiFgAggregatedQtyAsync(
        Guid workOrderId, Guid operationId)
    {
        var queryable = await _jobCardRepository.GetQueryableAsync();

        var jobCards = queryable
            .Where(jc => jc.WorkOrderId == workOrderId
                && jc.OperationId == operationId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .Select(jc => new { jc.CompletedQty, ManufacturedQty = jc.CompletedQty }) // CompletedQty serves as manufactured_qty in our model
            .ToList();

        if (jobCards.Count == 0) return (0, 0);

        var completedQty = jobCards.Sum(jc => Math.Max(jc.ManufacturedQty, jc.CompletedQty));
        var manufacturedQty = jobCards.Sum(jc => jc.ManufacturedQty);

        return (completedQty, manufacturedQty);
    }

    /// <summary>
    /// Validates that the previous operation in the routing has been manufactured
    /// before allowing this operation's job card to start or complete.
    /// Per ERPNext PR #57684: with semi-FG tracking, each operation must wait for
    /// the prior operation to produce its output before consuming it as input.
    /// </summary>
    public async Task ValidatePreviousOperationManufacturedAsync(JobCard jobCard)
    {
        if (jobCard.SequenceId <= 1) return; // First operation has no predecessor

        var queryable = await _jobCardRepository.GetQueryableAsync();

        // Find the previous operation's job cards (lower sequence, same WO)
        var previousOpCompletedQty = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId < jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .OrderByDescending(jc => jc.SequenceId)
            .GroupBy(jc => jc.SequenceId)
            .Select(g => g.Sum(jc => jc.CompletedQty))
            .FirstOrDefault();

        if (previousOpCompletedQty < jobCard.ForQuantity)
        {
            throw new BusinessException("MyERP:10020")
                .WithData("sequenceId", jobCard.SequenceId)
                .WithData("previousCompleted", previousOpCompletedQty)
                .WithData("required", jobCard.ForQuantity);
        }
    }

    /// <summary>
    /// Validates that a completion split adds up correctly.
    /// Per ERPNext PR #57687: total of split quantities must equal the job card's for_quantity.
    /// </summary>
    public static void ValidateCompletionSplit(decimal forQuantity, decimal completedQty, decimal processLossQty)
    {
        var total = completedQty + processLossQty;
        if (Math.Abs(total - forQuantity) > 0.001m)
        {
            throw new BusinessException("MyERP:10021")
                .WithData("forQuantity", forQuantity)
                .WithData("completedQty", completedQty)
                .WithData("processLossQty", processLossQty)
                .WithData("total", total);
        }
    }

    /// <summary>
    /// Gets total effective job card quantity for a work order operation.
    /// Per ERPNext PR #58466: accounts for pending_qty so partially completed job cards
    /// don't block creating follow-up job cards for remaining quantity.
    /// Formula: SUM(for_quantity - pending_qty) for non-cancelled job cards.
    /// </summary>
    public async Task<decimal> GetTotalJobCardQtyAsync(Guid workOrderId, Guid operationId)
    {
        var queryable = await _jobCardRepository.GetQueryableAsync();
        var total = queryable
            .Where(jc => jc.WorkOrderId == workOrderId
                && jc.OperationId == operationId
                && jc.Status != JobCardStatus.Cancelled)
            .Sum(jc => (decimal?)(jc.ForQuantity - jc.PendingQty)) ?? 0m;

        return total;
    }
}
