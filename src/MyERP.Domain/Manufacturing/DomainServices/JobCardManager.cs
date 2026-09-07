using System;
using System.Collections.Generic;
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
                    PlannedTimeInMins = op.TimeInMins * (qty / batchSize),
                    BatchSplit = op.BatchSplit,
                    WeightPerPiece = op.WeightPerPiece
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
    /// Creates Job Cards from direct BOM Operations (when routing is embedded directly in BOM).
    /// Maps to ERPNext manufacturing/doctype/bom_operation/bom_operation.py.
    /// </summary>
    public async Task<JobCard[]> CreateJobCardsFromBomOperationsAsync(
        WorkOrder wo, IEnumerable<BomOperation> bomOperations, Guid? tenantId = null)
    {
        var jobCards = new System.Collections.Generic.List<JobCard>();
        var sequence = 0;

        foreach (var op in bomOperations.OrderBy(o => o.SequenceId))
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
                    PlannedTimeInMins = op.TimeInMins * (qty / batchSize),
                    FinishedGoodItemId = op.FinishedGoodItemId,
                    BatchSplit = op.BatchSplit,
                    WeightPerPiece = op.WeightPerPiece
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
    /// Calculates the maximum completable quantity for a Job Card capped by previous operation completions.
    /// Per ERPNext PR #58256 (commit b68324ce78):
    /// If not first operation sequence, max completable = min(previous_op_completed_qty) - current_op_completed_qty.
    /// Returns null if first operation sequence or corrective job card.
    /// </summary>
    public async Task<decimal?> GetMaxCompletableQtyAsync(JobCard jobCard)
    {
        if (jobCard.IsCorrective || jobCard.SequenceId <= 1)
            return null;

        var queryable = await _jobCardRepository.GetQueryableAsync();

        var previousOperations = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId < jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .GroupBy(jc => jc.SequenceId)
            .Select(g => g.Sum(jc => jc.CompletedQty))
            .ToList();

        if (previousOperations.Count == 0)
            return null;

        var minPrevCompleted = previousOperations.Min();
        return Math.Max(0, minPrevCompleted - jobCard.CompletedQty);
    }

    /// <summary>
    /// Validates that the previous operation in the routing has been manufactured
    /// before allowing this operation's job card to start or complete.
    /// Per ERPNext PR #57684 and PR #58256: each operation must wait for prior operation output.
    /// </summary>
    public async Task ValidatePreviousOperationManufacturedAsync(JobCard jobCard, decimal? attemptingQty = null)
    {
        if (jobCard.SequenceId <= 1 || jobCard.IsCorrective) return; // First operation has no predecessor

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

        var qtyToCheck = attemptingQty ?? jobCard.ForQuantity;
        if (previousOpCompletedQty < qtyToCheck)
        {
            throw new BusinessException("MyERP:10020")
                .WithData("sequenceId", jobCard.SequenceId)
                .WithData("previousCompleted", previousOpCompletedQty)
                .WithData("required", qtyToCheck);
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

    /// <summary>
    /// Validates material transfer before Job Card start or completion.
    /// Per ERPNext PR #58009: when material transfer is required,
    /// start and completion require transferred materials to be ready (transferred_qty >= required_qty).
    /// Exempt: corrective job cards and job cards without required items.
    /// </summary>
    public async Task ValidateMaterialTransferAsync(JobCard jobCard, IRepository<WorkOrder, Guid> woRepository)
    {
        if (jobCard.IsCorrective) return;

        var wo = await woRepository.FindAsync(jobCard.WorkOrderId);
        if (wo == null || !wo.RequiredItems.Any() || wo.SkipTransfer) return;

        var matchingItems = jobCard.BomOperationId.HasValue
            ? wo.RequiredItems.Where(i => i.BomOperationId == jobCard.BomOperationId.Value).ToList()
            : wo.RequiredItems;

        if (!matchingItems.Any()) return;

        var pendingItems = matchingItems.Where(i => i.TransferredQuantity < i.RequiredQuantity).ToList();
        if (pendingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Materials need to be transferred to the work in progress warehouse before starting or completing Job Card (sequence {jobCard.SequenceId}).");
        }
    }

    /// <summary>
    /// Disallows all operations on Job Card if the linked Work Order is Closed, Stopped, or Cancelled.
    /// Per ERPNext PR #53157 / commit ee19c32c3a.
    /// </summary>
    public async Task ValidateWorkOrderNotClosedAsync(JobCard jobCard, IRepository<WorkOrder, Guid> woRepository)
    {
        var wo = await woRepository.FindAsync(jobCard.WorkOrderId);
        if (wo != null && (wo.Status == WorkOrderStatus.Stopped || wo.Status == WorkOrderStatus.Cancelled || wo.Status == WorkOrderStatus.Completed))
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", $"Cannot perform action on Job Card when linked Work Order is {wo.Status}.");
        }
    }
}
