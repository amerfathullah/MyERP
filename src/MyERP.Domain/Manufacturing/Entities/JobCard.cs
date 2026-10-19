using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Job Card — tracks execution of a single operation within a Work Order.
/// Each WO operation gets one or more Job Cards (split by batch_size).
/// Maps to ERPNext manufacturing/doctype/job_card.
/// </summary>
public class JobCard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid WorkOrderId { get; set; }
    public Guid OperationId { get; set; }

    /// <summary>
    /// BOM Operation row ID — disambiguates when the same Operation appears
    /// multiple times in a Work Order routing (e.g., "Assembly" at sequence 10 and 30).
    /// Per ERPNext PR #57387: resolved server-side via set_operation_id().
    /// </summary>
    public Guid? BomOperationId { get; set; }

    public Guid? WorkstationId { get; set; }
    public string? WorkstationType { get; set; }

    /// <summary>
    /// Semi-finished good item ID. When set, the JC produces this intermediate item
    /// instead of the WO's final production_item. Per gotcha #497/#813.
    /// </summary>
    public Guid? FinishedGoodItemId { get; set; }

    /// <summary>
    /// Semi-FG BOM override. When set, uses this BOM instead of the WO's main BOM.
    /// Per gotcha #813: `semi_fg_bom ?? bom_no` for correct sub-assembly resolution.
    /// </summary>
    public Guid? SemiFgBomId { get; set; }

    /// <summary>
    /// Whether this is a corrective Job Card (rework/repair operation).
    /// Corrective JCs skip material transfer validation.
    /// </summary>
    public bool IsCorrective { get; set; }

    /// <summary>
    /// On completion of the Job Card, split the consumed batch into one child batch per finished piece.
    /// Maps to ERPNext manufacturing/doctype/job_card/job_card.json (batch_split).
    /// </summary>
    public bool BatchSplit { get; set; }

    /// <summary>
    /// Weight per piece when batch splitting is enabled. Non-negative.
    /// Maps to ERPNext manufacturing/doctype/job_card/job_card.json (weight_per_piece).
    /// </summary>
    public decimal? WeightPerPiece { get; set; }

    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal PendingQty { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal TotalTimeInMins { get; set; }

    public Guid? WipWarehouseId { get; set; }
    public int SequenceId { get; set; }

    public JobCardStatus Status { get; private set; } = JobCardStatus.Open;

    /// <summary>Planned operation time (from Routing).</summary>
    public decimal PlannedTimeInMins { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    private readonly List<JobCardTimeLog> _timeLogs = new();
    public IReadOnlyList<JobCardTimeLog> TimeLogs => _timeLogs.AsReadOnly();

    private readonly List<JobCardSecondaryItem> _secondaryItems = new();
    public IReadOnlyList<JobCardSecondaryItem> SecondaryItems => _secondaryItems.AsReadOnly();

    protected JobCard() { }

    public JobCard(Guid id, Guid companyId, Guid workOrderId, Guid operationId,
        decimal forQuantity, int sequenceId, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        WorkOrderId = workOrderId;
        OperationId = operationId;
        ForQuantity = forQuantity;
        SequenceId = sequenceId;
        TenantId = tenantId;
    }

    public void Start()
    {
        if (Status is not (JobCardStatus.Open or JobCardStatus.PartiallyTransferred or JobCardStatus.MaterialTransferred))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = JobCardStatus.WorkInProgress;
        StartedAt ??= DateTime.UtcNow;
    }

    public void AddTimeLog(DateTime fromTime, DateTime toTime, decimal completedQty)
    {
        if (Status is JobCardStatus.Completed or JobCardStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (toTime <= fromTime)
            throw new ArgumentException("To time must be after from time.");

        // Prevent negative completed quantity (ERPNext commit c30665fda7 / PR #48946)
        if (completedQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(completedQty));

        var timeInMins = (decimal)(toTime - fromTime).TotalMinutes;
        _timeLogs.Add(new JobCardTimeLog(Guid.NewGuid(), Id, fromTime, toTime, timeInMins, completedQty));
        TotalTimeInMins = GetWorkstationMinutes(_timeLogs);
        CompletedQty = _timeLogs.Sum(t => t.CompletedQty);

        if (Status is JobCardStatus.Open or JobCardStatus.PartiallyTransferred or JobCardStatus.MaterialTransferred)
            Status = JobCardStatus.WorkInProgress;
    }

    /// <summary>
    /// Starts a time log at the workstation (ERPNext PR #60595 / commit da8343617a).
    /// </summary>
    public JobCardTimeLog StartTimeLog(DateTime fromTime)
    {
        if (Status is JobCardStatus.Completed or JobCardStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (Status is JobCardStatus.Open or JobCardStatus.PartiallyTransferred or JobCardStatus.MaterialTransferred)
        {
            Status = JobCardStatus.WorkInProgress;
            StartedAt ??= fromTime;
        }

        var log = new JobCardTimeLog(Guid.NewGuid(), Id, fromTime, default, 0m, 0m);
        _timeLogs.Add(log);
        return log;
    }

    /// <summary>
    /// Completes open time logs and allocates completed quantity (ERPNext PR #60595 / commit da8343617a).
    /// </summary>
    public void CompleteTimeLogs(DateTime toTime, decimal completedQty)
    {
        if (Status is JobCardStatus.Completed or JobCardStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var pendingQty = completedQty;
        var openLogs = _timeLogs.Where(tl => tl.ToTime == default).ToList();
        if (openLogs.Count > 0)
        {
            foreach (var row in openLogs)
            {
                row.ToTime = toTime;
                row.CompletedQty = pendingQty;
                row.TimeInMins = Math.Max(0m, (decimal)(toTime - row.FromTime).TotalMinutes);
                pendingQty = 0m;
            }
        }
        else
        {
            var fromTime = StartedAt ?? toTime.AddMinutes(-30);
            if (fromTime >= toTime) fromTime = toTime.AddMinutes(-1);
            var timeInMins = Math.Max(0m, (decimal)(toTime - fromTime).TotalMinutes);
            _timeLogs.Add(new JobCardTimeLog(Guid.NewGuid(), Id, fromTime, toTime, timeInMins, completedQty));
        }

        TotalTimeInMins = GetWorkstationMinutes(_timeLogs);
        CompletedQty = _timeLogs.Sum(t => t.CompletedQty);
    }

    /// <summary>
    /// Computes total workstation minutes by merging overlapping intervals.
    /// Per ERPNext PR #60650 (commit f5ae5b18fa):
    /// When multiple operators work simultaneously on the same Job Card, the workstation
    /// time is counted once across the overlapping intervals rather than sum(time_logs).
    /// </summary>
    public static decimal GetWorkstationMinutes(IEnumerable<JobCardTimeLog> timeLogs)
    {
        var intervals = new List<(DateTime start, DateTime end)>();
        decimal extraMinutes = 0m;

        foreach (var row in timeLogs)
        {
            if (row.FromTime != default && row.ToTime != default && row.ToTime > row.FromTime)
            {
                intervals.Add((row.FromTime, row.ToTime));
            }
            else if (row.TimeInMins > 0)
            {
                extraMinutes += row.TimeInMins;
            }
        }

        if (intervals.Count == 0) return extraMinutes;

        intervals.Sort((a, b) => a.start.CompareTo(b.start));

        decimal mergedMinutes = 0m;
        DateTime? currentStart = null;
        DateTime? currentEnd = null;

        foreach (var (start, end) in intervals)
        {
            if (currentEnd.HasValue && start <= currentEnd.Value)
            {
                if (end > currentEnd.Value)
                {
                    currentEnd = end;
                }
                continue;
            }

            if (currentEnd.HasValue && currentStart.HasValue)
            {
                mergedMinutes += (decimal)(currentEnd.Value - currentStart.Value).TotalMinutes;
            }

            currentStart = start;
            currentEnd = end;
        }

        if (currentEnd.HasValue && currentStart.HasValue)
        {
            mergedMinutes += (decimal)(currentEnd.Value - currentStart.Value).TotalMinutes;
        }

        return Math.Round(mergedMinutes + extraMinutes, 2);
    }

    public void SetPendingQty(decimal pendingQty)
    {
        if (Status is JobCardStatus.Completed or JobCardStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (pendingQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(pendingQty));

        var remainingQty = Math.Max(0m, ForQuantity - CompletedQty);
        if (pendingQty > 0 && pendingQty > remainingQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Pending Qty {pendingQty} cannot be more than the {remainingQty} left after the Total Completed Qty.");
        }

        PendingQty = pendingQty;
    }

    public void SetProcessLossQty(decimal processLossQty)
    {
        if (processLossQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(processLossQty));
        ProcessLossQty = processLossQty;
    }

    public JobCardSecondaryItem AddSecondaryItem(
        Guid itemId,
        string itemName,
        decimal stockQty,
        string stockUom,
        SecondaryItemType secondaryItemType,
        string? description = null,
        Guid? bomSecondaryItemId = null,
        int? idx = null)
    {
        if (stockQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(stockQty));

        var nextIdx = idx ?? (_secondaryItems.Count > 0 ? _secondaryItems.Max(i => i.Idx) + 1 : 1);
        var item = new JobCardSecondaryItem(
            Guid.NewGuid(),
            Id,
            itemId,
            itemName,
            stockQty,
            stockUom,
            secondaryItemType,
            description,
            bomSecondaryItemId,
            nextIdx,
            TenantId);

        _secondaryItems.Add(item);
        return item;
    }

    public void ClearSecondaryItems()
    {
        _secondaryItems.Clear();
    }

    /// <summary>
    /// Computes process loss quantity when completed quantity is less than for_quantity.
    /// Per ERPNext PR #59104 & #58466: applies when completed_qty > 0 or process_loss_qty is already recorded,
    /// deducting both completed_qty and pending_qty from for_quantity.
    /// </summary>
    public void SetProcessLoss()
    {
        var shouldSet = CompletedQty > 0 || ProcessLossQty > 0;
        ProcessLossQty = 0m;
        if (shouldSet && ForQuantity > CompletedQty)
        {
            ProcessLossQty = Math.Max(0m, Math.Round(ForQuantity - CompletedQty - PendingQty, 4));
        }
    }

    public void Complete()
    {
        if (Status is not (JobCardStatus.WorkInProgress or JobCardStatus.MaterialTransferred or JobCardStatus.PartiallyTransferred))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Validate From Time and To Time are present on all time logs (ERPNext PR #47325 / commit 7499c25a3c)
        foreach (var row in _timeLogs)
        {
            if (row.FromTime == default || row.ToTime == default || row.ToTime <= row.FromTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "From Time and To Time fields are required and To Time must be after From Time.");
            }
        }

        SetProcessLoss();

        // Per ERPNext PR #59104: Completed, Process Loss or Pending Qty is required before submission
        if (CompletedQty <= 0 && ProcessLossQty <= 0 && PendingQty <= 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Completed, Process Loss or Pending Qty is required for Job Card {Id}, please start and complete the job card before submission.");
        }

        Status = JobCardStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates status based on raw material transfer progress per ERPNext PR #56187.
    /// </summary>
    public void UpdateTransferStatus(bool allTransferred, bool anyTransferred)
    {
        if (Status is JobCardStatus.Completed or JobCardStatus.Cancelled or JobCardStatus.WorkInProgress or JobCardStatus.OnHold)
            return;

        if (allTransferred)
            Status = JobCardStatus.MaterialTransferred;
        else if (anyTransferred)
            Status = JobCardStatus.PartiallyTransferred;
        else
            Status = JobCardStatus.Open;
    }

    public void Hold()
    {
        if (Status != JobCardStatus.WorkInProgress)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = JobCardStatus.OnHold;
    }

    public void Resume()
    {
        if (Status != JobCardStatus.OnHold)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = JobCardStatus.WorkInProgress;
    }

    public void Cancel()
    {
        if (Status == JobCardStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = JobCardStatus.Cancelled;
    }
}

public class JobCardTimeLog : FullAuditedEntity<Guid>
{
    public Guid JobCardId { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal TimeInMins { get; set; }
    public decimal CompletedQty { get; set; }

    protected JobCardTimeLog() { }

    public JobCardTimeLog(Guid id, Guid jobCardId, DateTime fromTime, DateTime toTime,
        decimal timeInMins, decimal completedQty) : base(id)
    {
        // Prevent negative values (ERPNext PR #48946 / #48948)
        if (timeInMins < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(timeInMins));

        if (completedQty < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(completedQty));

        JobCardId = jobCardId;
        FromTime = fromTime;
        ToTime = toTime;
        TimeInMins = timeInMins;
        CompletedQty = completedQty;
    }
}
