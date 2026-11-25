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
            sequence++;
            var opSequence = op.SequenceId > 0 ? op.SequenceId : sequence;

            while (remaining > 0)
            {
                var qty = Math.Min(batchSize, remaining);

                var jc = new JobCard(
                    LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid(),
                    wo.CompanyId,
                    wo.Id,
                    op.OperationId,
                    qty,
                    opSequence,
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
    /// <summary>
    /// Gets the target production quantity for an operation.
    /// Per ERPNext PR #62257 / PR #62140 (commits 74de7638bf, df8fe9bb15, 2d55c8ed4b):
    /// When TrackSemiFinishedGoods is active, scales operation quantity by the component's per-unit BOM quantity.
    /// </summary>
    public static decimal GetOperationTargetQty(WorkOrder wo, BomOperation op, BillOfMaterials? bom)
    {
        if (!wo.TrackSemiFinishedGoods || !op.FinishedGoodItemId.HasValue || op.IsFinalFinishedGood || bom == null)
            return wo.Quantity;

        var matchingBomItem = bom.Items.FirstOrDefault(i => i.ItemId == op.FinishedGoodItemId.Value);
        if (matchingBomItem != null)
        {
            var perUnit = matchingBomItem.GetQtyConsumedPerUnit(bom.Quantity);
            if (perUnit > 0)
            {
                return Math.Round(perUnit * wo.Quantity, 4);
            }
        }

        return wo.Quantity;
    }

    public async Task<JobCard[]> CreateJobCardsFromBomOperationsAsync(
        WorkOrder wo, IEnumerable<BomOperation> bomOperations, Guid? tenantId = null, BillOfMaterials? bom = null)
    {
        var jobCards = new System.Collections.Generic.List<JobCard>();
        var sequence = 0;

        foreach (var op in bomOperations.OrderBy(o => o.SequenceId))
        {
            var targetQty = GetOperationTargetQty(wo, op, bom);
            var batchSize = op.BatchSize > 0 ? op.BatchSize : targetQty;
            var remaining = targetQty;
            sequence++;
            var opSequence = op.SequenceId > 0 ? op.SequenceId : sequence;

            Guid? resolvedWorkstationId = op.WorkstationId;
            string? resolvedWorkstationType = null;
            if (op.WorkstationTypeId.HasValue)
            {
                var activeWorkstations = await GetWorkstationsByTypeAsync(op.WorkstationTypeId.Value, wo.CompanyId);
                var firstWs = activeWorkstations.FirstOrDefault();
                resolvedWorkstationType = firstWs?.WorkstationType;
                if (!resolvedWorkstationId.HasValue)
                {
                    resolvedWorkstationId = firstWs?.Id;
                }
            }

            while (remaining > 0)
            {
                var qty = Math.Min(batchSize, remaining);

                var jc = new JobCard(
                    LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid(),
                    wo.CompanyId,
                    wo.Id,
                    op.OperationId,
                    qty,
                    opSequence,
                    tenantId
                )
                {
                    BomOperationId = op.Id,
                    WorkstationId = resolvedWorkstationId,
                    WorkstationType = resolvedWorkstationType,
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
    /// Gets active workstations matching a Workstation Type, ordered by production capacity descending.
    /// Per ERPNext PR #60517 (commit 78323c94aa): excludes disabled workstations (IsActive == false).
    /// </summary>
    public async Task<List<Workstation>> GetWorkstationsByTypeAsync(Guid workstationTypeId, Guid? companyId = null)
    {
        var wsQueryable = await _workstationRepository.GetQueryableAsync();
        if (wsQueryable == null) return new List<Workstation>();

        return wsQueryable
            .Where(w => w.IsActive
                && w.WorkstationTypeId == workstationTypeId
                && (!companyId.HasValue || w.CompanyId == companyId.Value))
            .OrderByDescending(w => w.ProductionCapacity)
            .ToList();
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
    /// Per ERPNext PR #60650 &amp; PR #65888: multi-slot capacity check using slot allocation,
    /// with spare workstation selection when only WorkstationType is specified.
    /// </summary>
    public async Task ValidateCapacityAsync(JobCard jobCard)
    {
        var candidateFrom = DateTime.UtcNow;
        var candidateTo = candidateFrom.AddMinutes((double)(jobCard.PlannedTimeInMins > 0 ? jobCard.PlannedTimeInMins : 30m));

        if (!jobCard.WorkstationId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(jobCard.WorkstationType))
                return;

            var wsQueryable = await _workstationRepository.GetQueryableAsync();
            if (wsQueryable == null)
                return;

            var typeWorkstations = wsQueryable
                .Where(w => w.IsActive
                    && w.CompanyId == jobCard.CompanyId
                    && w.WorkstationType == jobCard.WorkstationType)
                .OrderByDescending(w => w.ProductionCapacity)
                .ToList();

            if (typeWorkstations.Count == 0)
            {
                // Keep the WorkstationType filter when no enabled workstation matches (ERPNext cb6e1bdee7)
                var jcQueryable = await _jobCardRepository.GetQueryableAsync();
                var activeTypeJcs = jcQueryable == null
                    ? new List<JobCard>()
                    : jcQueryable
                        .Where(jc => jc.CompanyId == jobCard.CompanyId
                            && jc.WorkstationType == jobCard.WorkstationType
                            && jc.Id != jobCard.Id
                            && jc.Status == JobCardStatus.WorkInProgress)
                        .ToList();
                var typeLogs = ExtractOverlappingLogs(activeTypeJcs, candidateFrom, candidateTo);
                if (HasCapacityOverlap(1, typeLogs))
                {
                    var typeUsed = GetAllotedCapacity(MergeTimeLogsPerJobCard(typeLogs)).Count;
                    throw new BusinessException("MyERP:10012")
                        .WithData("workstation", jobCard.WorkstationType)
                        .WithData("capacity", 1)
                        .WithData("current", typeUsed);
                }
                return;
            }

            var freeWs = await FindFreeWorkstationAsync(typeWorkstations, jobCard.Id, candidateFrom, candidateTo);
            if (freeWs != null)
            {
                jobCard.WorkstationId = freeWs.Id;
                return;
            }

            var firstWs = typeWorkstations[0];
            var firstLogs = await GetOverlappingLogsAsync(firstWs.Id, jobCard.Id, candidateFrom, candidateTo);
            var used = GetAllotedCapacity(MergeTimeLogsPerJobCard(firstLogs)).Count;
            throw new BusinessException("MyERP:10012")
                .WithData("workstation", firstWs.Name)
                .WithData("capacity", firstWs.ProductionCapacity)
                .WithData("current", used);
        }

        var workstation = await _workstationRepository.GetAsync(jobCard.WorkstationId.Value);
        if (workstation.ProductionCapacity <= 0) return;

        var logs = await GetOverlappingLogsAsync(workstation.Id, jobCard.Id, candidateFrom, candidateTo);
        if (HasCapacityOverlap(workstation.ProductionCapacity, logs))
        {
            var slotsUsed = GetAllotedCapacity(MergeTimeLogsPerJobCard(logs)).Count;
            throw new BusinessException("MyERP:10012")
                .WithData("workstation", workstation.Name)
                .WithData("capacity", workstation.ProductionCapacity)
                .WithData("current", slotsUsed);
        }
    }

    private async Task<Workstation?> FindFreeWorkstationAsync(
        IReadOnlyList<Workstation> candidates,
        Guid excludeJobCardId,
        DateTime candidateFrom,
        DateTime candidateTo)
    {
        var candidateIds = candidates.Select(w => w.Id).ToList();
        var queryable = await _jobCardRepository.GetQueryableAsync();
        var activeJcs = queryable == null
            ? new List<JobCard>()
            : queryable
                .Where(jc => jc.WorkstationId.HasValue
                    && candidateIds.Contains(jc.WorkstationId.Value)
                    && jc.Id != excludeJobCardId
                    && jc.Status == JobCardStatus.WorkInProgress)
                .ToList();

        foreach (var ws in candidates)
        {
            if (ws.ProductionCapacity <= 0)
                return ws;

            var logs = ExtractOverlappingLogs(activeJcs.Where(jc => jc.WorkstationId == ws.Id), candidateFrom, candidateTo);
            if (!HasCapacityOverlap(ws.ProductionCapacity, logs))
                return ws;
        }

        return null;
    }

    /// <summary>
    /// Sentinel end time for an open/running Job Card time log (<c>ToTime == default</c>)
    /// per ERPNext PR #67447 (commits f2fe397926, caa7472ff0, ae63e778bb: <c>RUNNING_TIME_LOG_END</c>).
    /// Keeps the workstation slot occupied until the running time log is stopped.
    /// </summary>
    public static readonly DateTime RunningTimeLogEnd = new(9999, 12, 31);

    private async Task<List<JobCardTimeLogRange>> GetOverlappingLogsAsync(
        Guid workstationId,
        Guid excludeJobCardId,
        DateTime candidateFrom,
        DateTime candidateTo,
        bool includeRunning = true)
    {
        var queryable = await _jobCardRepository.GetQueryableAsync();
        if (queryable == null)
            return new List<JobCardTimeLogRange>();

        var activeJcs = queryable
            .Where(jc => jc.WorkstationId == workstationId
                && jc.Id != excludeJobCardId
                && jc.Status == JobCardStatus.WorkInProgress)
            .ToList();

        return ExtractOverlappingLogs(activeJcs, candidateFrom, candidateTo, includeRunning);
    }

    /// <summary>
    /// Extracts time log ranges on other active Job Cards overlapping <paramref name="candidateFrom"/>..<paramref name="candidateTo"/>.
    /// Per ERPNext PR #67447:
    /// - Qty-only logs (<c>FromTime == default</c>) skip overlap detection and save beside running Job Cards.
    /// - Running time logs (<c>ToTime == default</c>) on draft/WIP Job Cards occupy the workstation up to
    ///   <see cref="RunningTimeLogEnd"/> when <paramref name="includeRunning"/> is true.
    /// </summary>
    public static List<JobCardTimeLogRange> ExtractOverlappingLogs(
        IEnumerable<JobCard> activeJcs,
        DateTime candidateFrom,
        DateTime candidateTo,
        bool includeRunning = true)
    {
        var logs = new List<JobCardTimeLogRange>();
        if (candidateFrom == default)
        {
            return logs;
        }

        var effectiveCandidateTo = (includeRunning && candidateTo == default) ? RunningTimeLogEnd : candidateTo;
        if (effectiveCandidateTo == default)
        {
            return logs;
        }

        foreach (var other in activeJcs)
        {
            if (other.TimeLogs.Any())
            {
                foreach (var tl in other.TimeLogs)
                {
                    if (tl.FromTime == default)
                    {
                        continue;
                    }

                    if (tl.ToTime == default)
                    {
                        if (includeRunning
                            && other.Status != JobCardStatus.Completed
                            && other.Status != JobCardStatus.Cancelled
                            && tl.FromTime < effectiveCandidateTo)
                        {
                            logs.Add(new JobCardTimeLogRange(tl.FromTime, RunningTimeLogEnd, other.Id));
                        }
                        continue;
                    }

                    if (tl.FromTime < effectiveCandidateTo && tl.ToTime > candidateFrom)
                    {
                        logs.Add(new JobCardTimeLogRange(tl.FromTime, tl.ToTime, other.Id));
                    }
                }
            }
            else if (other.StartedAt.HasValue)
            {
                var sFrom = other.StartedAt.Value;
                var sTo = sFrom.AddMinutes((double)(other.PlannedTimeInMins > 0 ? other.PlannedTimeInMins : 30m));
                if (sFrom < effectiveCandidateTo && sTo > candidateFrom)
                {
                    logs.Add(new JobCardTimeLogRange(sFrom, sTo, other.Id));
                }
            }
        }

        return logs;
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
        if (fromTime == default)
            return;

        var effectiveTo = toTime == default ? RunningTimeLogEnd : toTime;
        foreach (var other in otherEmployeeLogs)
        {
            if (other.FromTime == default)
                continue;

            var otherEnd = other.ToTime == default ? RunningTimeLogEnd : other.ToTime;
            if (other.FromTime < effectiveTo && otherEnd > fromTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Employee {(employeeName ?? "assigned")} is already working from {other.FromTime:yyyy-MM-dd HH:mm:ss} to {otherEnd:yyyy-MM-dd HH:mm:ss} on another Job Card.");
            }
        }
    }

    /// <summary>
    /// Calculates total completed quantity for a Work Order from all its Job Cards.
    /// Per ERPNext: total_completed = MIN(per-operation completed) when operations exist.
    /// This is the bottleneck formula — the slowest operation limits WO completion.
    /// </summary>
    public async Task<decimal> GetWorkOrderCompletedQtyAsync(Guid workOrderId, decimal? workOrderQty = null)
    {
        var queryable = await _jobCardRepository.GetQueryableAsync();

        // Per PR bde118e7cf: exclude corrective job cards from the aggregate
        // Corrective JCs represent rework/repair, not new production output
        var nonCorrectiveJcs = queryable
            .Where(jc => jc.WorkOrderId == workOrderId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .ToList();

        if (nonCorrectiveJcs.Count == 0) return 0;

        var perOperationNormalized = nonCorrectiveJcs
            .GroupBy(jc => jc.OperationId)
            .Select(g =>
            {
                var opForQty = g.Sum(jc => jc.ForQuantity);
                var opCompleted = g.Sum(jc => jc.CompletedQty + jc.ProcessLossQty);
                if (workOrderQty.HasValue && workOrderQty.Value > 0 && opForQty > 0)
                {
                    // Scale operation output quantity to parent work order quantity (ERPNext PR #62257 / #62140)
                    return Math.Round(opCompleted * workOrderQty.Value / opForQty, 4);
                }
                return g.Sum(jc => jc.CompletedQty);
            })
            .ToList();

        return perOperationNormalized.Count > 0 ? perOperationNormalized.Min() : 0;
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
        var previousOpCards = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId < jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .OrderByDescending(jc => jc.SequenceId)
            .ToList();

        if (previousOpCards.Count == 0) return;

        var lastSeq = previousOpCards.Max(jc => jc.SequenceId);
        var lastSeqCards = previousOpCards.Where(jc => jc.SequenceId == lastSeq).ToList();
        var prevCompletedQty = lastSeqCards.Sum(jc => jc.CompletedQty);
        var prevForQty = lastSeqCards.Sum(jc => jc.ForQuantity);

        var currentSeqCards = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.SequenceId == jobCard.SequenceId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .ToList();
        var currentForQty = currentSeqCards.Sum(jc => jc.ForQuantity);
        if (currentForQty <= 0) currentForQty = jobCard.ForQuantity;

        var qtyToCheck = attemptingQty ?? jobCard.ForQuantity;

        // Scale previous completed qty to current operation's units if output rates differ (ERPNext PR #62257 / PR #62140)
        var scaledPrevCompleted = prevForQty > 0 && currentForQty > 0 && prevForQty != currentForQty
            ? Math.Round(prevCompletedQty * currentForQty / prevForQty, 4)
            : prevCompletedQty;

        if (scaledPrevCompleted < qtyToCheck)
        {
            throw new BusinessException("MyERP:10020")
                .WithData("sequenceId", jobCard.SequenceId)
                .WithData("previousCompleted", scaledPrevCompleted)
                .WithData("required", qtyToCheck);
        }
    }

    /// <summary>
    /// Validates that CompletedQty + ProcessLossQty of an operation does not exceed what the
    /// slowest previous operation actually completed. Once that previous operation has finished
    /// with no remaining pending quantity, its process loss is deducted from this card's
    /// ProcessLossQty and ForQuantity (rounded to 4 decimals).
    /// Per ERPNext PR #60275 / commit a147d2c3cc / test 79ee0fb448.
    /// </summary>
    public async Task ValidateInputFromPreviousOperationsAsync(JobCard jobCard, bool trackSemiFinishedGoods = false)
    {
        if (jobCard.IsCorrective || trackSemiFinishedGoods || jobCard.SequenceId <= 1)
            return;

        var queryable = await _jobCardRepository.GetQueryableAsync();
        var woCards = queryable
            .Where(jc => jc.WorkOrderId == jobCard.WorkOrderId
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective
                && jc.SequenceId <= jobCard.SequenceId)
            .ToList();

        var previousGroups = woCards
            .Where(jc => jc.Id != jobCard.Id && jc.SequenceId < jobCard.SequenceId)
            .GroupBy(jc => jc.SequenceId)
            .Select(g => new
            {
                SequenceId = g.Key,
                CompletedQty = g.Sum(c => c.CompletedQty),
                ForQuantity = g.Sum(c => c.ForQuantity),
                IsFullyCompleted = g.All(c => c.Status == JobCardStatus.Completed && Math.Round(c.PendingQty, 4) <= 0m)
            })
            .ToList();

        if (previousGroups.Count == 0)
            return;

        var bottleneckPrev = previousGroups.OrderBy(g => g.CompletedQty).First();

        var otherCurrentCards = woCards
            .Where(jc => jc.Id != jobCard.Id && jc.SequenceId == jobCard.SequenceId)
            .ToList();
        var currentForQty = otherCurrentCards.Sum(c => c.ForQuantity) + jobCard.ForQuantity;
        var scaledPrevCompleted = bottleneckPrev.ForQuantity > 0 && currentForQty > 0 && bottleneckPrev.ForQuantity != currentForQty
            ? Math.Round(bottleneckPrev.CompletedQty * currentForQty / bottleneckPrev.ForQuantity, 4, MidpointRounding.AwayFromZero)
            : bottleneckPrev.CompletedQty;

        var totalInputQty = Math.Round(
            otherCurrentCards.Sum(c => c.CompletedQty + c.ProcessLossQty) + jobCard.CompletedQty + jobCard.ProcessLossQty,
            4,
            MidpointRounding.AwayFromZero);

        var excessQty = Math.Round(totalInputQty - scaledPrevCompleted, 4, MidpointRounding.AwayFromZero);
        if (excessQty <= 0m)
            return;

        if (bottleneckPrev.IsFullyCompleted && excessQty <= Math.Round(jobCard.ProcessLossQty, 4, MidpointRounding.AwayFromZero))
        {
            jobCard.ProcessLossQty = Math.Max(0m, Math.Round(jobCard.ProcessLossQty - excessQty, 4, MidpointRounding.AwayFromZero));
            jobCard.ForQuantity = Math.Max(0m, Math.Round(jobCard.ForQuantity - excessQty, 4, MidpointRounding.AwayFromZero));
            return;
        }

        throw new BusinessException("MyERP:10020")
            .WithData("sequenceId", jobCard.SequenceId)
            .WithData("previousCompleted", scaledPrevCompleted)
            .WithData("required", totalInputQty);
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
                && jc.Status != JobCardStatus.Cancelled
                && !jc.IsCorrective)
            .Sum(jc => (decimal?)(jc.ForQuantity - jc.PendingQty)) ?? 0m;

        return total;
    }

    /// <summary>
    /// Validates that the total Job Card quantity for an operation does not exceed the allowed Work Order quantity
    /// (including overproduction allowance and net of upstream process loss).
    /// Per ERPNext commit 12c81a9dd5 (validate_job_card):
    /// Does NOT subtract the operation's completed_qty from total_job_card_qty, so extra Job Cards over a
    /// lowered allowance or after operation completion are refused.
    /// </summary>
    public static void ValidateTotalJobCardQty(
        decimal totalJobCardQty,
        decimal allowedWorkOrderQty,
        bool isCorrective = false,
        int precision = 4)
    {
        if (isCorrective)
            return;

        if (Math.Round(totalJobCardQty, precision) > Math.Round(allowedWorkOrderQty, precision))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("totalJobCardQty", Math.Round(totalJobCardQty, precision))
                .WithData("allowedWorkOrderQty", Math.Round(allowedWorkOrderQty, precision))
                .WithData("detail",
                    $"Total Job Card quantity ({Math.Round(totalJobCardQty, precision)}) exceeds allowed Work Order quantity ({Math.Round(allowedWorkOrderQty, precision)}).");
        }
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
            : (wo.RequiredItems.Any(i => i.BomOperationId.HasValue)
                ? wo.RequiredItems.Where(i => !i.BomOperationId.HasValue).ToList()
                : wo.RequiredItems.ToList());

        if (!matchingItems.Any()) return;

        // Per ERPNext PR #60650 (commit b2f37a6579), PR #65171 (commit e7870754ec), and PR #65183 (commit 98e9ed6914, d15ae49fe4):
        // check expected qty per item for this job card using precision so fractional transfers (e.g. 1/3) succeed after full transfer
        var hasShortMaterial = false;
        foreach (var row in matchingItems)
        {
            var expected = CalculateJobCardItemRequiredQty(row.RequiredQuantity, jobCard.ForQuantity, wo.Quantity, 4);
            if (Math.Round(row.TransferredQuantity, 4) < expected)
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
    /// Rounds the Job Card item's required quantity using the configured precision.
    /// Per ERPNext PR #65183 (commits 98e9ed6914, d15ae49fe4):
    /// Uses the Job Card's precision for item required qty calculation so full transfers
    /// of fractional material quantities match the required quantity.
    /// </summary>
    public static decimal CalculateJobCardItemRequiredQty(
        decimal itemRequiredQty,
        decimal jobCardForQuantity,
        decimal operationOrWoQty,
        int precision = 4)
    {
        if (operationOrWoQty <= 0) return 0m;
        return Math.Round((itemRequiredQty * jobCardForQuantity) / operationOrWoQty, precision);
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
            : (wo.RequiredItems.Any(i => i.BomOperationId.HasValue)
                ? wo.RequiredItems.Where(i => !i.BomOperationId.HasValue).ToList()
                : wo.RequiredItems.ToList());

        if (!matchingItems.Any())
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "This Job Card has no raw materials to transfer.");
        }

        return matchingItems;
    }

    /// <summary>
    /// Updates Job Card requested quantity from linked Material Requests.
    /// Per ERPNext PR #60650 (commit 15c19b0931):
    /// Calculates the minimum fulfillment fraction across required materials,
    /// and scales by ForQuantity.
    /// </summary>
    public async Task UpdateRequestedQtyAsync(
        JobCard jobCard,
        IRepository<WorkOrder, Guid> woRepository,
        IRepository<Purchasing.Entities.MaterialRequest, Guid> mrRepository)
    {
        var mrQuery = await mrRepository.GetQueryableAsync();
        var submittedMrs = mrQuery
            .Where(mr => mr.JobCardId == jobCard.Id
                && mr.Status != Core.DocumentStatus.Draft
                && mr.Status != Core.DocumentStatus.Cancelled
                && mr.Status != Core.DocumentStatus.Closed)
            .ToList();

        var matchingItems = await GetRawMaterialsAsync(jobCard, woRepository);
        if (!matchingItems.Any())
        {
            jobCard.RequestedQty = 0m;
            return;
        }

        var requestedByItem = submittedMrs
            .SelectMany(mr => mr.Items)
            .GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.StockQty));

        var wo = await woRepository.FindAsync(jobCard.WorkOrderId);
        var woQty = wo?.Quantity > 0 ? wo.Quantity : 1m;

        decimal minFraction = 1m;
        bool hasItems = false;

        foreach (var rm in matchingItems)
        {
            var expectedQty = (rm.RequiredQuantity / woQty) * jobCard.ForQuantity;
            if (expectedQty > 0)
            {
                hasItems = true;
                var requestedQty = requestedByItem.GetValueOrDefault(rm.ItemId, 0m);
                var fraction = requestedQty / expectedQty;
                if (fraction < minFraction)
                {
                    minFraction = fraction;
                }
            }
        }

        jobCard.RequestedQty = hasItems
            ? Math.Min(jobCard.ForQuantity, Math.Round(minFraction * jobCard.ForQuantity, 4))
            : 0m;
    }

    /// <summary>
    /// Validates on Job Card cancellation that the remaining completed and process-loss quantity
    /// on the operation still covers the Work Order's produced quantity.
    /// Per ERPNext commit 6f328657c8 (validate_produced_quantity):
    /// Handed-over pending quantity (<c>pending_qty</c>) is handed to another Job Card, so it is NOT
    /// counted as completed by the remaining cards when checking whether cancellation would leave
    /// the operation's completed total below the Work Order's produced quantity.
    /// </summary>
    public static void ValidateProducedQuantityOnCancel(
        decimal remainingOperationCompletedQty,
        decimal remainingOperationProcessLossQty,
        decimal workOrderQty,
        decimal operationRequiredQty,
        decimal workOrderProducedQty,
        int precision = 4)
    {
        if (workOrderProducedQty <= 0m || operationRequiredQty <= 0m)
        {
            return;
        }

        var operationTotal = ((remainingOperationCompletedQty + remainingOperationProcessLossQty) * workOrderQty) / operationRequiredQty;
        var roundedOperationTotal = Math.Round(operationTotal, precision, MidpointRounding.AwayFromZero);
        var roundedProducedQty = Math.Round(workOrderProducedQty, precision, MidpointRounding.AwayFromZero);

        if (roundedOperationTotal < roundedProducedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot cancel Job Card because the Work Order has already produced {roundedProducedQty} which exceeds the remaining completed operation quantity ({roundedOperationTotal}).");
        }
    }

    /// <summary>
    /// Computes the pending finished-good divisor over which Job Card secondary items are spread on a Manufacture Stock Entry
    /// per ERPNext commits 691ab99981 and 47095f5dc7 (<c>_get_pending_secondary_qty</c> in <c>stock_entry/services/manufacturing.py</c>):
    /// When the entry is not for a single Job Card, adds unbooked Job Card <paramref name="pendingJobCardProcessLossQty"/>
    /// (excluding BOM-estimated loss) to <c>completedJobCardQty - workOrderProducedQty</c>.
    /// </summary>
    public static decimal CalculatePendingSecondaryItemDivisorQty(
        bool hasDirectJobCardOnStockEntry,
        decimal stockEntryFgCompletedQty,
        decimal completedJobCardQty,
        decimal workOrderProducedQty,
        decimal pendingJobCardProcessLossQty)
    {
        if (hasDirectJobCardOnStockEntry)
        {
            return stockEntryFgCompletedQty;
        }

        return completedJobCardQty - workOrderProducedQty + pendingJobCardProcessLossQty;
    }

    /// <summary>
    /// Determines whether operation sequence validation (<c>validate_sequence_id</c>) must run on a Job Card save
    /// per ERPNext commits aef2c70d91, 0cf78ce8f5, and 51bf79c376 (<c>job_card.py</c>):
    /// An unstarted draft Job Card without time logs or process loss can be edited before previous operations produce,
    /// whereas starting/adding time logs (<paramref name="isMakeTimeLogAction"/> or <paramref name="hasTimeLogs"/>),
    /// recording process loss, or submitting enforces sequence order.
    /// </summary>
    public static bool ShouldValidateSequenceOnSave(
        bool isSubmitted,
        bool isMakeTimeLogAction,
        bool hasTimeLogs,
        decimal processLossQty)
    {
        return isSubmitted || isMakeTimeLogAction || hasTimeLogs || processLossQty != 0m;
    }

    /// <summary>
    /// Computes the workstation minutes and operating cost for a Job Card in the Cost of Poor Quality Report date range,
    /// skipping open time logs (missing <c>FromTime</c> or <c>ToTime</c>), clipping logs to <c>[fromDate, toDate]</c>,
    /// and taking the union of overlapping intervals so parallel operators count once
    /// per ERPNext commits 0eef3bc575 and d507648396 (<c>cost_of_poor_quality_report.py</c>).
    /// </summary>
    public static (decimal TotalTimeInMins, decimal OperatingCost) CalculateCostOfPoorQualityClippedTimeAndCost(
        decimal hourRate,
        DateTime? reportFromDate,
        DateTime? reportToDate,
        IEnumerable<(DateTime? FromTime, DateTime? ToTime)>? timeLogs)
    {
        if (timeLogs == null)
        {
            return (0m, 0m);
        }

        var clipped = new List<(DateTime Start, DateTime End)>();
        foreach (var log in timeLogs)
        {
            if (!log.FromTime.HasValue || !log.ToTime.HasValue)
            {
                continue;
            }

            var start = reportFromDate.HasValue && reportFromDate.Value > log.FromTime.Value
                ? reportFromDate.Value
                : log.FromTime.Value;
            var end = reportToDate.HasValue && reportToDate.Value < log.ToTime.Value
                ? reportToDate.Value
                : log.ToTime.Value;

            if (start < end)
            {
                clipped.Add((start, end));
            }
        }

        if (clipped.Count == 0)
        {
            return (0m, 0m);
        }

        var sorted = clipped.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
        var merged = new List<(DateTime Start, DateTime End)> { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var last = merged[^1];
            if (current.Start < last.End)
            {
                if (current.End > last.End)
                {
                    merged[^1] = (last.Start, current.End);
                }
            }
            else
            {
                merged.Add(current);
            }
        }

        var totalMinutes = merged.Sum(i => (decimal)(i.End - i.Start).TotalMinutes);
        var roundedMins = Math.Round(totalMinutes, 2, MidpointRounding.AwayFromZero);
        var operatingCost = hourRate * totalMinutes / 60m;

        return (roundedMins, operatingCost);
    }

    /// <summary>
    /// Rescales Job Card required items in place when <c>ForQuantity</c> (or operation) changes
    /// per ERPNext commits 25e2944174, 28bfdc29fc, c530d6f106, and 3b6942f4dd (<c>job_card.py</c> / <c>job_card.js</c>):
    /// 1. Matches existing Job Card item rows by <c>ItemId</c> and updates their <c>RequiredQty</c> in place
    ///    (preserving <c>RowId</c>, <c>TransferredQty</c>, and <c>ConsumedQty</c>) instead of clearing and rebuilding.
    /// 2. Floors each matched row's <c>RequiredQty</c> at <c>Max(scaledRequiredQty, row.TransferredQty)</c>
    ///    so lowering <c>ForQuantity</c> after a transfer keeps <c>RequiredQty &gt;= TransferredQty</c> and keeps the transfer cancellable.
    /// 3. Drops unmatched rows that have neither <c>TransferredQty</c> nor <c>ConsumedQty</c> (e.g., rows from a prior operation
    ///    or manually added untransferred rows the operation does not need), while keeping any row that already has material movement.
    /// </summary>
    public static IReadOnlyList<(Guid RowId, Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty)> RescaleJobCardRequiredItemsInPlace(
        IEnumerable<(Guid RowId, Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty)>? existingRows,
        IEnumerable<(Guid ItemId, Guid? OperationId, Guid? BomOperationId, decimal WorkOrderRequiredQty)>? workOrderRequiredItems,
        decimal jobCardForQuantity,
        decimal operationOrWorkOrderQty,
        Guid? jobCardOperationId = null,
        Guid? jobCardBomOperationId = null,
        bool trackSemiFinishedGoods = false,
        int precision = 4)
    {
        var currentRows = (existingRows ?? Enumerable.Empty<(Guid RowId, Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty)>())
            .ToList();
        var unmatchedRows = currentRows.ToList();

        var woItems = (workOrderRequiredItems ?? Enumerable.Empty<(Guid ItemId, Guid? OperationId, Guid? BomOperationId, decimal WorkOrderRequiredQty)>())
            .ToList();

        var rowIndex = 0;
        foreach (var d in woItems)
        {
            rowIndex++;
            if (!trackSemiFinishedGoods
                && (jobCardOperationId.HasValue || jobCardBomOperationId.HasValue)
                && !d.OperationId.HasValue
                && !d.BomOperationId.HasValue)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Row {rowIndex} : Operation is required against the raw material item {d.ItemId}");
            }

            if (jobCardBomOperationId.HasValue && d.BomOperationId.HasValue)
            {
                if (jobCardBomOperationId.Value != d.BomOperationId.Value)
                {
                    continue;
                }
            }
            else if (jobCardOperationId.HasValue && d.OperationId.HasValue)
            {
                if (jobCardOperationId.Value != d.OperationId.Value)
                {
                    continue;
                }
            }

            var scaledRequiredQty = CalculateJobCardItemRequiredQty(
                d.WorkOrderRequiredQty,
                jobCardForQuantity,
                operationOrWorkOrderQty,
                precision);

            var matchIdx = unmatchedRows.FindIndex(r => r.ItemId == d.ItemId);
            if (matchIdx >= 0)
            {
                var matched = unmatchedRows[matchIdx];
                unmatchedRows.RemoveAt(matchIdx);
                var currentIdx = currentRows.FindIndex(r => r.RowId == matched.RowId);
                if (currentIdx >= 0)
                {
                    currentRows[currentIdx] = (
                        matched.RowId,
                        matched.ItemId,
                        Math.Max(scaledRequiredQty, matched.TransferredQty),
                        matched.TransferredQty,
                        matched.ConsumedQty);
                }
                continue;
            }

            currentRows.Add((Guid.NewGuid(), d.ItemId, scaledRequiredQty, 0m, 0m));
        }

        foreach (var unmatched in unmatchedRows)
        {
            if (unmatched.TransferredQty == 0m && unmatched.ConsumedQty == 0m)
            {
                currentRows.RemoveAll(r => r.RowId == unmatched.RowId);
            }
        }

        return currentRows;
    }

    /// <summary>
    /// Resolves the hour rate for a corrective Job Card.
    /// Per ERPNext PR #67472 (commits 51abb93bdb, 38b5cbe21a / JobCard.set_corrective_hour_rate):
    /// keeps the manual hour rate while the workstation is unchanged (compared against the prior saved
    /// workstation or the amended/source Job Card's workstation), and switches to the new workstation's
    /// hour rate when the workstation changes.
    /// </summary>
    public static decimal ResolveCorrectiveJobCardHourRate(
        decimal currentHourRate,
        Guid? currentWorkstationId,
        Guid? previousWorkstationId,
        decimal? newWorkstationHourRate)
    {
        if (currentWorkstationId.HasValue
            && currentWorkstationId != previousWorkstationId
            && newWorkstationHourRate.HasValue)
        {
            return newWorkstationHourRate.Value;
        }

        return currentHourRate;
    }

    /// <summary>
    /// Determines whether a non-stock BOM item row's cost should be charged on a Job Card's operation output entry:
    /// rows bound to an operation belong only to that operation's Job Card, while unassigned rows go only to the
    /// final Job Card producing the Work Order's main <paramref name="workOrderProductionItemId"/>.
    /// Per ERPNext PR #67462 (<c>_is_job_card_row</c> in <c>operations_cost.py</c>).
    /// </summary>
    public static bool IsNonStockBomRowAttributedToJobCard(
        Guid? rowOperationId,
        Guid? rowOperationRowId,
        Guid jobCardOperationId,
        Guid? jobCardOperationRowId,
        Guid? jobCardFinishedGoodId,
        Guid workOrderProductionItemId)
    {
        if (rowOperationRowId.HasValue)
        {
            return jobCardOperationRowId.HasValue && rowOperationRowId.Value == jobCardOperationRowId.Value;
        }

        if (rowOperationId.HasValue)
        {
            return rowOperationId.Value == jobCardOperationId;
        }

        return jobCardFinishedGoodId.HasValue && jobCardFinishedGoodId.Value == workOrderProductionItemId;
    }

    /// <summary>
    /// Computes the denominator quantity over which corrective operation cost is spread:
    /// when <paramref name="trackSemiFinishedGoods"/> is enabled, filters completed Job Cards to ONLY
    /// those producing <paramref name="workOrderProductionItemId"/> (excluding semi-finished operation cards)
    /// before taking the minimum total completed quantity across operations.
    /// Per ERPNext commit <c>ac5cc09181</c> (<c>_max_operation_quantity</c> in <c>operations_cost.py</c>).
    /// </summary>
    public static decimal CalculateCorrectiveOperationCostBasisQty(
        Guid workOrderProductionItemId,
        bool trackSemiFinishedGoods,
        IEnumerable<(Guid OperationId, Guid? FinishedGoodItemId, decimal CompletedQty)>? jobCardCompletions)
    {
        if (jobCardCompletions == null)
        {
            return 0m;
        }

        var filtered = trackSemiFinishedGoods
            ? jobCardCompletions.Where(c => c.FinishedGoodItemId == workOrderProductionItemId)
            : jobCardCompletions;

        var totalsByOp = filtered
            .GroupBy(c => c.OperationId)
            .Select(g => g.Sum(x => x.CompletedQty))
            .ToList();

        return totalsByOp.Count > 0 ? totalsByOp.Min() : 0m;
    }

    /// <summary>
    /// Determines whether Work-Order-level corrective or additional operating cost should be booked
    /// on a Manufacture Stock Entry: skips Job Card entries that produce a semi-finished good
    /// (<c>jobCardFinishedGoodId != workOrderProductionItemId</c>) so corrective cost is booked on
    /// the finished-good entry only.
    /// Per ERPNext commit <c>a96f709df4</c> (<c>makes_semi_finished_good</c> in <c>operations_cost.py</c>).
    /// </summary>
    public static bool ShouldBookWorkOrderLevelOperatingCostOnEntry(
        Guid workOrderProductionItemId,
        Guid? jobCardFinishedGoodItemId)
        => !jobCardFinishedGoodItemId.HasValue || jobCardFinishedGoodItemId.Value == workOrderProductionItemId;

    /// <summary>
    /// Splits an operation-level operating cost <paramref name="amount"/> across workstation operating
    /// components in table order using cumulative rounding so the component shares sum exactly to
    /// <paramref name="amount"/> (returning empty when total component rate is zero).
    /// Per ERPNext commits <c>1bc6b681ff</c>, <c>21589f031f</c>, and <c>6688a76b2e</c> (<c>_split_by_component_rate</c>).
    /// </summary>
    public static List<(string OperatingComponent, decimal Amount)> SplitOperationCostByWorkstationComponents(
        decimal amount,
        IEnumerable<(string OperatingComponent, decimal OperatingRate)>? workstationComponents,
        int precision = 2)
    {
        var result = new List<(string OperatingComponent, decimal Amount)>();
        if (amount == 0m || workstationComponents == null)
        {
            return result;
        }

        var components = workstationComponents.ToList();
        var totalRate = components.Sum(c => c.OperatingRate);
        if (totalRate <= 0m)
        {
            return result;
        }

        var allocated = 0m;
        var cumulativeRate = 0m;
        foreach (var comp in components)
        {
            cumulativeRate += comp.OperatingRate;
            var cumulativeAmount = Math.Round((amount * cumulativeRate) / totalRate, precision, MidpointRounding.AwayFromZero);
            var share = Math.Round(cumulativeAmount - allocated, precision, MidpointRounding.AwayFromZero);
            allocated = cumulativeAmount;
            result.Add((comp.OperatingComponent, share));
        }

        return result;
    }

    /// <summary>
    /// Clips completed Job Card time logs to <c>[rangeStart, rangeEnd]</c> for Cost of Poor Quality
    /// reporting, skipping open time logs that lack either <c>FromTime</c> or <c>ToTime</c>.
    /// Per ERPNext commit <c>d507648396</c> (<c>clip_to_date_range</c> in <c>cost_of_poor_quality_report.py</c>).
    /// </summary>
    public static decimal CalculateClippedTimeLogMinutesInDateRange(
        IEnumerable<(DateTime? FromTime, DateTime? ToTime)>? timeLogs,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        if (timeLogs == null || rangeEnd <= rangeStart)
        {
            return 0m;
        }

        var totalMinutes = 0m;
        foreach (var log in timeLogs)
        {
            if (!log.FromTime.HasValue || !log.ToTime.HasValue)
                continue;

            var start = log.FromTime.Value > rangeStart ? log.FromTime.Value : rangeStart;
            var end = log.ToTime.Value < rangeEnd ? log.ToTime.Value : rangeEnd;
            if (end > start)
            {
                totalMinutes += (decimal)(end - start).TotalMinutes;
            }
        }

        return totalMinutes;
    }

    /// <summary>
    /// Resolves the hour rate for a corrective Job Card: keeps any manual rate until its workstation changes
    /// from the baseline workstation (<c>previousSavedWorkstationId ?? amendedFromWorkstationId ?? sourceJobCardWorkstationId</c>),
    /// and fetches the new workstation's rate once changed.
    /// Per ERPNext commits <c>51abb93bdb</c> and <c>38b5cbe21a</c> (<c>job_card.py</c>).
    /// </summary>
    public static decimal ResolveCorrectiveJobCardHourRate(
        decimal currentHourRate,
        Guid? currentWorkstationId,
        Guid? previousSavedWorkstationId,
        Guid? amendedFromWorkstationId,
        Guid? sourceJobCardWorkstationId,
        Func<Guid, decimal>? fetchWorkstationHourRate)
    {
        var baselineWorkstation = previousSavedWorkstationId ?? amendedFromWorkstationId ?? sourceJobCardWorkstationId;
        if (currentWorkstationId.HasValue
            && currentWorkstationId != baselineWorkstation
            && fetchWorkstationHourRate != null)
        {
            return fetchWorkstationHourRate(currentWorkstationId.Value);
        }

        return currentHourRate;
    }

    /// <summary>
    /// Reconciles Job Card item rows in place when <c>ForQuantity</c> or <c>Operation</c> changes:
    /// - Matched required items keep <c>RequiredQty = max(newRequiredQty, TransferredQty)</c> so lowering
    ///   <c>ForQuantity</c> after a transfer never drops <c>RequiredQty</c> below already transferred qty.
    /// - Existing rows no longer required by the operation are dropped when neither transferred nor consumed;
    ///   rows with transferred or consumed stock are preserved.
    /// Per ERPNext commits <c>c530d6f106</c> and <c>3b6942f4dd</c> (<c>job_card.py</c>).
    /// </summary>
    public static List<(Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty)> ReconcileJobCardItemsOnRescale(
        IEnumerable<(Guid ItemId, decimal ExistingRequiredQty, decimal TransferredQty, decimal ConsumedQty)>? existingRows,
        IReadOnlyDictionary<Guid, decimal>? newRequiredQtyByItemId)
    {
        var result = new List<(Guid ItemId, decimal RequiredQty, decimal TransferredQty, decimal ConsumedQty)>();
        var unmatched = existingRows != null
            ? existingRows.ToList()
            : new List<(Guid ItemId, decimal ExistingRequiredQty, decimal TransferredQty, decimal ConsumedQty)>();

        if (newRequiredQtyByItemId != null)
        {
            foreach (var (itemId, newReqQty) in newRequiredQtyByItemId)
            {
                var idx = unmatched.FindIndex(r => r.ItemId == itemId);
                if (idx >= 0)
                {
                    var row = unmatched[idx];
                    unmatched.RemoveAt(idx);
                    result.Add((row.ItemId, Math.Max(newReqQty, row.TransferredQty), row.TransferredQty, row.ConsumedQty));
                }
                else
                {
                    result.Add((itemId, newReqQty, 0m, 0m));
                }
            }
        }

        foreach (var leftover in unmatched)
        {
            if (leftover.TransferredQty != 0m || leftover.ConsumedQty != 0m)
            {
                result.Add((leftover.ItemId, leftover.ExistingRequiredQty, leftover.TransferredQty, leftover.ConsumedQty));
            }
        }

        return result;
    }

    /// <summary>
    /// Relinks Job Card secondary item rows to their matching BOM secondary item row by
    /// <c>(ItemId, SecondaryItemType)</c>, preserving an existing valid link when the BOM row still
    /// matches the row's current item and type, and clearing/relinking rows whose item or type changed.
    /// Per ERPNext PR #67394 (<c>JobCard.link_secondary_items_to_bom</c>).
    /// </summary>
    public static List<(Guid RowId, Guid? ResolvedBomSecondaryItemId)> LinkSecondaryItemsToBom(
        IEnumerable<(Guid RowId, Guid ItemId, SecondaryItemType SecondaryItemType, Guid? ExistingBomSecondaryItemId)>? jobCardSecondaryRows,
        IEnumerable<(Guid BomSecondaryItemId, Guid ItemId, SecondaryItemType SecondaryItemType)>? bomSecondaryRows)
    {
        var result = new List<(Guid RowId, Guid? ResolvedBomSecondaryItemId)>();
        if (jobCardSecondaryRows == null)
            return result;

        var bomList = (bomSecondaryRows ?? Enumerable.Empty<(Guid, Guid, SecondaryItemType)>()).ToList();
        var bomById = bomList.ToDictionary(b => b.BomSecondaryItemId, b => (b.ItemId, b.SecondaryItemType));
        var bomByKey = new Dictionary<(Guid ItemId, SecondaryItemType Type), Guid>();
        foreach (var b in bomList)
            bomByKey[(b.ItemId, b.SecondaryItemType)] = b.BomSecondaryItemId;

        foreach (var row in jobCardSecondaryRows)
        {
            if (row.ExistingBomSecondaryItemId.HasValue
                && bomById.TryGetValue(row.ExistingBomSecondaryItemId.Value, out var existingMatch)
                && existingMatch == (row.ItemId, row.SecondaryItemType))
            {
                result.Add((row.RowId, row.ExistingBomSecondaryItemId.Value));
                continue;
            }

            Guid? resolved = bomByKey.TryGetValue((row.ItemId, row.SecondaryItemType), out var matchedId)
                ? matchedId
                : null;
            result.Add((row.RowId, resolved));
        }

        return result;
    }

    /// <summary>
    /// Determines whether a BOM non-stock (service/expense) item should be charged onto a specific
    /// Job Card's output entry per ERPNext PR #67462 (<c>bom/services/operations_cost.py: _is_job_card_row</c>):
    /// - Items assigned to an operation are charged ONLY on that operation's Job Card.
    /// - Unassigned items (no operation row/id) are charged ONLY on the final operation's output
    ///   (where <paramref name="jobCardFinishedGoodId"/> equals <paramref name="workOrderProductionItemId"/>).
    /// </summary>
    public static bool ShouldChargeBomNonStockItemOnJobCardOutput(
        int? bomItemOperationRowIdx,
        Guid? bomItemOperationId,
        int jobCardOperationRowIdx,
        Guid? jobCardOperationId,
        Guid jobCardFinishedGoodId,
        Guid workOrderProductionItemId)
    {
        if (bomItemOperationRowIdx.HasValue)
            return bomItemOperationRowIdx.Value == jobCardOperationRowIdx;

        if (bomItemOperationId.HasValue)
            return jobCardOperationId.HasValue && bomItemOperationId.Value == jobCardOperationId.Value;

        return jobCardFinishedGoodId == workOrderProductionItemId;
    }

    /// <summary>
    /// Rescales Job Card required items in place when <c>ForQuantity</c> changes per ERPNext PR #68098
    /// (<c>JobCard.reset_required_items</c> &amp; <c>set_required_item</c>):
    /// - Updates matched existing rows in place, clamping <c>RequiredQty</c> to at least <c>TransferredQty</c>.
    /// - Appends newly required items not yet on the Job Card.
    /// - Drops unmatched existing rows ONLY when both <c>TransferredQty</c> and <c>ConsumedQty</c> are 0,
    ///   preserving any row against which material has already moved.
    /// </summary>
    public static List<JobCardRescaledItemRow> RescaleJobCardRequiredItemsInPlace(
        IEnumerable<JobCardExistingItemRow> existingRows,
        IEnumerable<(Guid ItemId, decimal ComputedRequiredQty)> targetRequiredItems)
    {
        var unmatched = existingRows.ToList();
        var result = new List<JobCardRescaledItemRow>();

        foreach (var target in targetRequiredItems)
        {
            var matchIdx = unmatched.FindIndex(r => r.ItemId == target.ItemId);
            if (matchIdx >= 0)
            {
                var matched = unmatched[matchIdx];
                unmatched.RemoveAt(matchIdx);
                result.Add(new JobCardRescaledItemRow(
                    matched.RowId,
                    matched.ItemId,
                    Math.Max(target.ComputedRequiredQty, matched.TransferredQty),
                    matched.TransferredQty,
                    matched.ConsumedQty));
            }
            else
            {
                result.Add(new JobCardRescaledItemRow(
                    null,
                    target.ItemId,
                    target.ComputedRequiredQty,
                    0m,
                    0m));
            }
        }

        foreach (var leftover in unmatched)
        {
            if (leftover.TransferredQty > 0m || leftover.ConsumedQty > 0m)
            {
                result.Add(new JobCardRescaledItemRow(
                    leftover.RowId,
                    leftover.ItemId,
                    leftover.RequiredQty,
                    leftover.TransferredQty,
                    leftover.ConsumedQty));
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether a BOM non-stock item row is charged to a given Job Card's manufacture output
    /// per ERPNext PR #67462 (<c>operations_cost.py: _is_job_card_row</c>):
    /// - Rows linked to an operation row or operation are charged only to the matching Job Card.
    /// - Rows with no operation are charged only to the final Job Card producing the Work Order's finished good.
    /// </summary>
    public static bool IsNonStockItemChargedToJobCard(
        Guid? bomItemOperationRowId,
        Guid? bomItemOperationId,
        Guid? jobCardOperationRowId,
        Guid? jobCardOperationId,
        Guid? jobCardFinishedGoodItemId,
        Guid workOrderProductionItemId)
    {
        if (bomItemOperationRowId.HasValue && bomItemOperationRowId.Value != Guid.Empty)
        {
            return jobCardOperationRowId.HasValue && bomItemOperationRowId.Value == jobCardOperationRowId.Value;
        }

        if (bomItemOperationId.HasValue && bomItemOperationId.Value != Guid.Empty)
        {
            return jobCardOperationId.HasValue && bomItemOperationId.Value == jobCardOperationId.Value;
        }

        return jobCardFinishedGoodItemId.HasValue && jobCardFinishedGoodItemId.Value == workOrderProductionItemId;
    }

    /// <summary>
    /// Validates Work Order produced quantity when cancelling a Job Card per ERPNext PR #67468
    /// (<c>job_card.py: validate_produced_quantity</c>):
    /// computes the remaining operation output strictly from <c>(remainingCompletedQty + remainingProcessLossQty)</c>
    /// WITHOUT adding <c>pendingQty</c> (which is handed off to another card, not completed by remaining cards),
    /// and throws when <paramref name="workOrderProducedQty"/> exceeds the remaining operation output.
    /// </summary>
    public static void ValidateProducedQuantityOnJobCardCancel(
        decimal remainingCompletedQty,
        decimal remainingProcessLossQty,
        decimal workOrderQty,
        decimal operationQty,
        decimal workOrderProducedQty,
        int producedQtyPrecision = 4)
    {
        if (operationQty <= 0m)
        {
            return;
        }

        var operationTotal = Math.Round(
            ((remainingCompletedQty + remainingProcessLossQty) * workOrderQty) / operationQty,
            producedQtyPrecision,
            MidpointRounding.AwayFromZero);

        if (Math.Round(workOrderProducedQty, producedQtyPrecision, MidpointRounding.AwayFromZero) > operationTotal)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("workOrderProducedQty", workOrderProducedQty)
                .WithData("operationTotal", operationTotal)
                .WithData(
                    "detail",
                    $"Cannot cancel Job Card because Work Order produced quantity ({workOrderProducedQty}) exceeds remaining operation completed quantity ({operationTotal}).");
        }
    }

    /// <summary>
    /// Computes net transferred quantity for a Job Card (or Job Card item row) across submitted
    /// <c>Material Transfer for Manufacture</c> Stock Entries by subtracting component returns
    /// (<c>IsReturn ? -Qty : Qty</c>), and throws if net transferred quantity falls below <c>0</c>
    /// (at 6-decimal precision) per ERPNext PR #68916 (<c>job_card.py: set_transferred_qty</c> /
    /// <c>get_transferred_qty_from_stock_entry</c>).
    /// </summary>
    public static decimal CalculateNetTransferredQtyWithReturns(
        IEnumerable<(decimal Qty, bool IsReturn)>? transferEntries,
        string? jobCardLabel = null)
    {
        var netQty = 0m;
        if (transferEntries != null)
        {
            foreach (var entry in transferEntries)
            {
                netQty += entry.IsReturn ? -entry.Qty : entry.Qty;
            }
        }

        if (Math.Round(netQty, 6, MidpointRounding.AwayFromZero) < 0m)
        {
            var label = string.IsNullOrWhiteSpace(jobCardLabel) ? "Job Card" : $"Job Card {jobCardLabel}";
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("netTransferredQty", netQty)
                .WithData("detail", $"{label} has more components returned than transferred. Cancel its return entries first.");
        }

        return netQty;
    }

    /// <summary>
    /// Validates a Job Card component return Stock Entry against both the Job Card's transferred
    /// quantity and the Work Order's unconsumed cap per ERPNext PR #68916
    /// (<c>material_transfer.py</c> / commit <c>8893668bc4</c>):
    /// - Rejects <paramref name="returnFgQty"/> exceeding <paramref name="jobCardTransferredQty"/> (6dp).
    /// - Caps per-item returnable qty at <c>min(jobCardReturnable, workOrderUnconsumed)</c> and
    ///   throws when any requested return item exceeds its effective cap.
    /// </summary>
    public static Dictionary<Guid, decimal> ValidateAndCapJobCardComponentReturn(
        decimal returnFgQty,
        decimal jobCardTransferredQty,
        IReadOnlyDictionary<Guid, decimal> jobCardReturnableByItem,
        IReadOnlyDictionary<Guid, decimal> workOrderUnconsumedByItem,
        IReadOnlyDictionary<Guid, decimal>? requestedReturnByItem = null)
    {
        if (Math.Round(returnFgQty, 6, MidpointRounding.AwayFromZero)
            > Math.Round(jobCardTransferredQty, 6, MidpointRounding.AwayFromZero))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("returnFgQty", returnFgQty)
                .WithData("transferredQty", jobCardTransferredQty)
                .WithData("detail", $"Return quantity {returnFgQty} is more than the quantity {jobCardTransferredQty} transferred against Job Card.");
        }

        var effectiveCaps = new Dictionary<Guid, decimal>();
        foreach (var (itemId, jcQty) in jobCardReturnableByItem ?? new Dictionary<Guid, decimal>())
        {
            var woQty = workOrderUnconsumedByItem != null
                ? workOrderUnconsumedByItem.GetValueOrDefault(itemId, 0m)
                : jcQty;
            effectiveCaps[itemId] = Math.Max(0m, Math.Min(jcQty, woQty));
        }

        if (requestedReturnByItem != null)
        {
            foreach (var (itemId, reqQty) in requestedReturnByItem)
            {
                var cap = effectiveCaps.GetValueOrDefault(itemId, 0m);
                if (Math.Round(reqQty, 6, MidpointRounding.AwayFromZero)
                    > Math.Round(cap, 6, MidpointRounding.AwayFromZero))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("itemId", itemId)
                        .WithData("requestedQty", reqQty)
                        .WithData("returnableCap", cap)
                        .WithData("detail", $"Return quantity {reqQty} exceeds unconsumed returnable cap {cap}.");
                }
            }
        }

        return effectiveCaps;
    }

    /// <summary>
    /// Computes elapsed seconds for the live Job Card stopwatch timer per ERPNext PR #67474
    /// (<c>job_card.js: get_current_time</c>):
    /// takes the union of all <c>[FromTime, ToTime ?? nowUtc]</c> intervals sorted by start, merging
    /// overlapping intervals so parallel operators are counted once rather than summed, matching server-side
    /// <c>get_workstation_minutes</c>.
    /// </summary>
    public static double CalculateLiveJobCardTimerSeconds(
        IEnumerable<(DateTime? FromTime, DateTime? ToTime)>? timeLogs,
        DateTime nowUtc)
    {
        if (timeLogs == null)
        {
            return 0d;
        }

        var intervals = timeLogs
            .Where(l => l.FromTime.HasValue)
            .Select(l => (Start: l.FromTime!.Value, End: l.ToTime ?? nowUtc))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        if (intervals.Count == 0)
        {
            return 0d;
        }

        double totalSeconds = 0d;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;

        for (var i = 1; i < intervals.Count; i++)
        {
            var (start, end) = intervals[i];
            if (start <= currentEnd)
            {
                if (end > currentEnd)
                {
                    currentEnd = end;
                }
                continue;
            }

            totalSeconds += (currentEnd - currentStart).TotalSeconds;
            currentStart = start;
            currentEnd = end;
        }

        totalSeconds += (currentEnd - currentStart).TotalSeconds;
        return totalSeconds;
    }

    /// <summary>
    /// Converts a UTC timestamp into the ERP system time zone for Job Card start/pause/resume/end
    /// logging per ERPNext PR #66764 (<c>job_card.js</c>, <c>work_order.js</c>, <c>shop_floor.js</c>:
    /// <c>frappe.datetime.system_datetime()</c>), ensuring logged times never drift to browser-local time.
    /// </summary>
    public static DateTime ConvertUtcToSystemTimeZoneForJobCardLog(
        DateTime utcTimestamp,
        TimeZoneInfo systemTimeZone)
    {
        var normalizedUtc = utcTimestamp.Kind == DateTimeKind.Utc
            ? utcTimestamp
            : DateTime.SpecifyKind(utcTimestamp.ToUniversalTime(), DateTimeKind.Utc);
        if (utcTimestamp.Kind == DateTimeKind.Unspecified)
        {
            normalizedUtc = DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, systemTimeZone);
    }

    /// <summary>
    /// Computes active Shop Floor elapsed seconds for Job Card time logs stored in
    /// <paramref name="systemTimeZone"/> by comparing open logs against the system-time-zone
    /// equivalent of <paramref name="utcNow"/> per ERPNext PR #66764 (<c>shop_floor.js</c>).
    /// </summary>
    public static double CalculateShopFloorElapsedSecondsInSystemTimeZone(
        IEnumerable<(DateTime? FromTimeSystem, DateTime? ToTimeSystem)>? timeLogs,
        DateTime utcNow,
        TimeZoneInfo systemTimeZone)
    {
        if (timeLogs == null)
        {
            return 0d;
        }

        var systemNow = ConvertUtcToSystemTimeZoneForJobCardLog(utcNow, systemTimeZone);
        double totalSeconds = 0d;
        foreach (var (fromTime, toTime) in timeLogs)
        {
            if (!fromTime.HasValue)
            {
                continue;
            }

            var end = toTime ?? systemNow;
            if (end > fromTime.Value)
            {
                totalSeconds += (end - fromTime.Value).TotalSeconds;
            }
        }

        return totalSeconds;
    }
}

public readonly record struct JobCardExistingItemRow(
    Guid RowId,
    Guid ItemId,
    decimal RequiredQty,
    decimal TransferredQty,
    decimal ConsumedQty);

public readonly record struct JobCardRescaledItemRow(
    Guid? RowId,
    Guid ItemId,
    decimal RequiredQty,
    decimal TransferredQty,
    decimal ConsumedQty);

/// <summary>Represents a time range for capacity and overlap validation (ERPNext PR #60650).</summary>
public record JobCardTimeLogRange(DateTime FromTime, DateTime ToTime, Guid? JobCardId = null);

