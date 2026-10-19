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
        WorkOrder wo, Routing routing, Guid? tenantId = null, BillOfMaterials? bom = null)
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
                    LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid(),
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

                PopulateSecondaryItemsFromBom(jc, bom, qty);

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
        WorkOrder wo, IEnumerable<BomOperation> bomOperations, Guid? tenantId = null, BillOfMaterials? bom = null)
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
                    LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid(),
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

                PopulateSecondaryItemsFromBom(jc, bom, qty);

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
    /// Distributes secondary items (scrap, byproduct, co-product) from BOM to Job Card proportionally to batch qty.
    /// Per ERPNext PR #59436 (commit 1d1562a68e): preserves secondary items across split job cards.
    /// </summary>
    public static void PopulateSecondaryItemsFromBom(JobCard jc, BillOfMaterials? bom, decimal forQty)
    {
        if (bom?.SecondaryItems == null || bom.SecondaryItems.Count == 0 || forQty <= 0)
            return;

        var bomQty = bom.Quantity > 0 ? bom.Quantity : 1m;
        foreach (var sec in bom.SecondaryItems)
        {
            var secQty = (sec.Quantity / bomQty) * forQty;
            secQty -= secQty * (sec.ProcessLossPercentage / 100m);
            if (secQty > 0)
            {
                jc.AddSecondaryItem(
                    sec.ItemId,
                    sec.ItemName ?? string.Empty,
                    secQty,
                    sec.StockUom ?? "Unit",
                    sec.SecondaryItemType,
                    description: null,
                    bomSecondaryItemId: sec.Id);
            }
        }
    }

    /// <summary>
    /// Validates workstation capacity before starting a Job Card.
    /// Per ERPNext PR #60650: multi-slot capacity check using slot allocation.
    /// </summary>
    public async Task ValidateCapacityAsync(JobCard jobCard)
    {
        if (!jobCard.WorkstationId.HasValue) return;

        var workstation = await _workstationRepository.GetAsync(jobCard.WorkstationId.Value);
        if (workstation.ProductionCapacity <= 0) return;

        var candidateFrom = DateTime.UtcNow;
        var candidateTo = candidateFrom.AddMinutes((double)(jobCard.PlannedTimeInMins > 0 ? jobCard.PlannedTimeInMins : 30m));

        var queryable = await _jobCardRepository.GetQueryableAsync();
        var activeJcs = queryable
            .Where(jc => jc.WorkstationId == jobCard.WorkstationId
                && jc.Id != jobCard.Id
                && jc.Status == JobCardStatus.WorkInProgress)
            .ToList();

        var logs = new List<JobCardTimeLogRange>();
        foreach (var other in activeJcs)
        {
            if (other.TimeLogs.Any())
            {
                logs.AddRange(other.TimeLogs
                    .Where(tl => tl.FromTime < candidateTo && tl.ToTime > candidateFrom)
                    .Select(tl => new JobCardTimeLogRange(tl.FromTime, tl.ToTime, other.Id)));
            }
            else if (other.StartedAt.HasValue)
            {
                var sFrom = other.StartedAt.Value;
                var sTo = sFrom.AddMinutes((double)(other.PlannedTimeInMins > 0 ? other.PlannedTimeInMins : 30m));
                if (sFrom < candidateTo && sTo > candidateFrom)
                {
                    logs.Add(new JobCardTimeLogRange(sFrom, sTo, other.Id));
                }
            }
        }

        if (HasCapacityOverlap(workstation.ProductionCapacity, logs))
        {
            var slotsUsed = GetAllotedCapacity(MergeTimeLogsPerJobCard(logs)).Count;
            throw new BusinessException("MyERP:10012")
                .WithData("workstation", workstation.Name)
                .WithData("capacity", workstation.ProductionCapacity)
                .WithData("current", slotsUsed);
        }
    }

    /// <summary>
    /// Merges time logs for the same Job Card when they are consecutive or overlapping.
    /// Per ERPNext PR #60650 (commit 934d62446e):
    /// Plain time logs (without JobCardId) are preserved directly in the merged collection.
    /// </summary>
    public static List<JobCardTimeLogRange> MergeTimeLogsPerJobCard(IEnumerable<JobCardTimeLogRange> timeLogs)
    {
        var logs = timeLogs.ToList();
        var merged = logs.Where(l => !l.JobCardId.HasValue || l.JobCardId == Guid.Empty).ToList();

        var namedGroups = logs
            .Where(l => l.JobCardId.HasValue && l.JobCardId != Guid.Empty)
            .GroupBy(l => l.JobCardId!.Value);

        foreach (var group in namedGroups)
        {
            var sorted = group.OrderBy(l => l.FromTime).ToList();
            foreach (var log in sorted)
            {
                if (merged.Count > 0 && merged[^1].JobCardId == log.JobCardId && log.FromTime < merged[^1].ToTime)
                {
                    var prev = merged[^1];
                    var maxToTime = log.ToTime > prev.ToTime ? log.ToTime : prev.ToTime;
                    merged[^1] = new JobCardTimeLogRange(prev.FromTime, maxToTime, prev.JobCardId);
                }
                else
                {
                    merged.Add(log);
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Allocates time logs into capacity slots.
    /// Each slot tracks the latest ToTime of sequential job cards.
    /// When a log's FromTime >= slot's last ToTime, it reuses the slot (sequential).
    /// Otherwise, a new capacity slot is opened.
    /// </summary>
    public static List<DateTime> GetAllotedCapacity(IEnumerable<JobCardTimeLogRange> timeLogs)
    {
        var sorted = timeLogs.OrderBy(l => l.FromTime).ToList();
        if (sorted.Count == 0) return new List<DateTime>();

        var allotedCapacity = new List<DateTime> { sorted[0].ToTime };
        for (int i = 1; i < sorted.Count; i++)
        {
            var log = sorted[i];
            bool sequentialFound = false;
            for (int s = 0; s < allotedCapacity.Count; s++)
            {
                if (allotedCapacity[s] <= log.FromTime)
                {
                    allotedCapacity[s] = log.ToTime;
                    sequentialFound = true;
                    break;
                }
            }

            if (!sequentialFound)
            {
                allotedCapacity.Add(log.ToTime);
            }
        }

        return allotedCapacity;
    }

    /// <summary>
    /// Determines whether the time logs exceed the workstation's production capacity.
    /// Per ERPNext PR #60650:
    /// - Capacity 1: overlaps if any logs exist (len >= 1).
    /// - Capacity N: merges logs per job card, computes slots, and overlaps if slots >= capacity.
    /// </summary>
    public static bool HasCapacityOverlap(int productionCapacity, IEnumerable<JobCardTimeLogRange> timeLogs)
    {
        if (productionCapacity <= 0) return false;
        var logs = timeLogs.ToList();
        if (logs.Count == 0) return false;
        if (productionCapacity == 1 && logs.Count >= 1) return true;

        var merged = MergeTimeLogsPerJobCard(logs);
        var alloted = GetAllotedCapacity(merged);
        return alloted.Count >= productionCapacity;
    }

    /// <summary>
    /// Validates employee overlap across time logs by TIME only.
    /// Per ERPNext PR #60650 (commit b9ae627673):
    /// Only actual datetime overlap (fromTimeA < toTimeB && toTimeA > fromTimeB) throws.
    /// An employee having another open/draft job card on a different workstation
    /// is NOT blocked unless time windows actually clash.
    /// </summary>
    public static void ValidateEmployeeTimeOverlap(
        DateTime fromTime,
        DateTime toTime,
        IEnumerable<JobCardTimeLogRange> otherEmployeeLogs,
        string? employeeName = null)
    {
        foreach (var other in otherEmployeeLogs)
        {
            if (other.FromTime < toTime && other.ToTime > fromTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Employee {(employeeName ?? "assigned")} is already working from {other.FromTime:yyyy-MM-dd HH:mm:ss} to {other.ToTime:yyyy-MM-dd HH:mm:ss} on another Job Card.");
            }
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
    /// Calculates default completion values (completed qty, pending qty) considering max completable qty.
    /// Per ERPNext PR #60275 (commits 39d99e8cf1 & 0f50d7d18d).
    /// </summary>
    public (decimal completedQty, decimal pendingQty) GetCompletionDefaults(JobCard jobCard, decimal? maxCompletableQty)
    {
        var targetQty = jobCard.PendingQty > 0 ? jobCard.PendingQty : jobCard.ForQuantity;
        decimal completedQty;
        if (maxCompletableQty.HasValue)
        {
            var allowed = Math.Max(0m, maxCompletableQty.Value);
            completedQty = Math.Min(targetQty, allowed);
        }
        else
        {
            completedQty = targetQty;
        }

        var pendingQty = Math.Max(0m, targetQty - completedQty);
        return (completedQty, pendingQty);
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
    /// Validates that later operations in the routing have not completed more quantity
    /// than this operation would have completed.
    /// Per ERPNext PR #60650 (commit e98a4a6aec):
    /// Prevents cancelling or reducing a Job Card if a successor operation already has higher completed qty.
    /// </summary>
    public async Task ValidateLaterOperationsAsync(JobCard jobCard, decimal? targetCompletedQty = null)
    {
        if (jobCard.IsCorrective || jobCard.SequenceId <= 0) return;

        var queryable = await _jobCardRepository.GetQueryableAsync();

        var currentOpJobCards = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId == jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .ToList();

        var currentOpQty = currentOpJobCards
            .Where(jc => jc.Id != jobCard.Id)
            .Sum(jc => jc.CompletedQty) + (targetCompletedQty ?? jobCard.CompletedQty);

        var laterOperations = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId > jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .GroupBy(jc => jc.SequenceId)
            .Select(g => new { SequenceId = g.Key, CompletedQty = g.Sum(jc => jc.CompletedQty) })
            .ToList();

        foreach (var later in laterOperations)
        {
            if (later.CompletedQty > currentOpQty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Operation sequence {later.SequenceId} has already completed {later.CompletedQty}, more than the {currentOpQty} that operation sequence {jobCard.SequenceId} would have. Cancel the Job Cards of later operations first.");
            }
        }
    }

    /// <summary>
    /// Validates that a completion split adds up correctly.
    /// Per ERPNext PR #57687 / PR #59104: total of split quantities must equal the job card's for_quantity.
    /// Completed qty can be 0 when process loss or pending qty is present.
    /// </summary>
    public static void ValidateCompletionSplit(decimal forQuantity, decimal completedQty, decimal processLossQty, decimal pendingQty = 0m)
    {
        if (completedQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(completedQty));

        if (pendingQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(pendingQty));

        if (processLossQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(processLossQty));

        var total = completedQty + processLossQty + pendingQty;
        if (Math.Abs(total - forQuantity) > 0.001m)
        {
            throw new BusinessException("MyERP:10021")
                .WithData("forQuantity", forQuantity)
                .WithData("completedQty", completedQty)
                .WithData("processLossQty", processLossQty)
                .WithData("pendingQty", pendingQty)
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
        if (wo == null || !wo.RequiredItems.Any() || wo.SkipTransfer || wo.Quantity <= 0) return;

        var matchingItems = jobCard.BomOperationId.HasValue
            ? wo.RequiredItems.Where(i => i.BomOperationId == jobCard.BomOperationId.Value).ToList()
            : wo.RequiredItems.ToList();

        if (!matchingItems.Any()) return;

        // Per ERPNext PR #60650 (commit b2f37a6579): check expected qty per item for this job card
        var hasShortMaterial = false;
        foreach (var row in matchingItems)
        {
            var expected = (row.RequiredQuantity / wo.Quantity) * jobCard.ForQuantity;
            if (row.TransferredQuantity < expected)
            {
                hasShortMaterial = true;
                break;
            }
        }

        if (hasShortMaterial || (jobCard.TransferredQty > 0 && jobCard.TransferredQty < jobCard.ForQuantity))
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

    /// <summary>
    /// Gets raw materials required for a Job Card from the linked Work Order.
    /// Per ERPNext workstation.py / PR #58927: throws when Job Card has no raw materials to transfer.
    /// </summary>
    public async Task<List<WorkOrderItem>> GetRawMaterialsAsync(
        JobCard jobCard, IRepository<WorkOrder, Guid> woRepository)
    {
        var wo = await woRepository.FindAsync(jobCard.WorkOrderId);
        if (wo == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                .WithData("detail", "Work Order not found for Job Card.");
        }

        var matchingItems = jobCard.BomOperationId.HasValue
            ? wo.RequiredItems.Where(i => i.BomOperationId == jobCard.BomOperationId.Value).ToList()
            : wo.RequiredItems.ToList();

        if (!matchingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "This Job Card has no raw materials to transfer.");
        }

        return matchingItems;
    }
}

/// <summary>Represents a time range for capacity and overlap validation (ERPNext PR #60650).</summary>
public record JobCardTimeLogRange(DateTime FromTime, DateTime ToTime, Guid? JobCardId = null);

