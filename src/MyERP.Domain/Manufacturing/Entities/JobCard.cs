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

    public decimal ForQuantity { get; set; }
    public decimal CompletedQty { get; set; }
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
        if (Status != JobCardStatus.Open)
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

        var timeInMins = (decimal)(toTime - fromTime).TotalMinutes;
        _timeLogs.Add(new JobCardTimeLog(Guid.NewGuid(), Id, fromTime, toTime, timeInMins, completedQty));
        TotalTimeInMins = _timeLogs.Sum(t => t.TimeInMins);
        CompletedQty = _timeLogs.Sum(t => t.CompletedQty);

        if (Status == JobCardStatus.Open)
            Status = JobCardStatus.WorkInProgress;
    }

    public void Complete()
    {
        if (Status is not (JobCardStatus.WorkInProgress or JobCardStatus.MaterialTransferred))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = JobCardStatus.Completed;
        CompletedAt = DateTime.UtcNow;
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
        JobCardId = jobCardId;
        FromTime = fromTime;
        ToTime = toTime;
        TimeInMins = timeInMins;
        CompletedQty = completedQty;
    }
}
