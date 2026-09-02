using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Work Order — production execution referencing a BOM.
/// </summary>
public class WorkOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string WorkOrderNumber { get; set; } = null!;
    public WorkOrderStatus Status { get; private set; }

    public Guid ItemId { get; set; }
    public Guid BomId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal DisassembledQuantity { get; set; }
    public decimal MaterialTransferred { get; set; }
    public decimal ProcessLossQty { get; set; }
    public decimal ProcessLossPercentage { get; set; }

    /// <summary>
    /// Effective FG quantity after process loss deduction.
    /// Per ERPNext: fg_completed_qty = quantity - process_loss_qty
    /// </summary>
    public decimal EffectiveFgQuantity =>
        ProcessLossQty > 0
            ? Quantity - ProcessLossQty
            : (ProcessLossPercentage > 0
                ? Quantity * (1 - ProcessLossPercentage / 100m)
                : Quantity);

    /// <summary>
    /// Percentage of production completed (based on total WO quantity, not FG quantity).
    /// </summary>
    public decimal PercentComplete =>
        Quantity > 0 ? Math.Min(100, Math.Round(ProducedQuantity / Quantity * 100, 2)) : 0;

    public Guid CompanyId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? SalesOrderItemId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? WipWarehouseId { get; set; }
    public Guid? FgWarehouseId { get; set; }
    public Guid? ScrapWarehouseId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public string? Notes { get; set; }
    public bool TrackSemiFinishedGoods { get; set; }
    public bool SkipTransfer { get; set; }
    /// <summary>Backflush from WIP warehouse even when skip_transfer is enabled (ERPNext PR #49280 / commit fe0722c4f1).</summary>
    public bool FromWipWarehouse { get; set; }

    public List<WorkOrderItem> RequiredItems { get; private set; } = new();

    protected WorkOrder() { }

    public WorkOrder(Guid id, Guid companyId, string workOrderNumber, Guid itemId, Guid bomId, decimal quantity, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        WorkOrderNumber = workOrderNumber;
        ItemId = Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));
        BomId = Check.NotDefaultOrNull<Guid>(bomId, nameof(bomId));
        Quantity = quantity;
        Status = WorkOrderStatus.Draft;
        TenantId = tenantId;
    }

    public void SetPlannedDates(DateTime? startDate, DateTime? endDate)
    {
        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate);
        PlannedStartDate = startDate;
        PlannedEndDate = endDate;
    }

    /// <summary>
    /// Validates whole number quantity when item stock UOM has MustBeWholeNumber set (gotcha #497).
    /// </summary>
    public void ValidateWholeNumberQuantity(bool mustBeWholeNumber)
    {
        if (mustBeWholeNumber)
        {
            var rounded = Math.Round(Quantity, 4);
            if (Math.Abs(Math.Round(rounded, 0) - rounded) > 0.0000001m)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Quantity {Quantity} must be a whole number for this item's UOM.");
            }
        }
    }

    public void ValidateDates()
    {
        if (PlannedStartDate.HasValue && PlannedEndDate.HasValue && PlannedEndDate.Value < PlannedStartDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.PlannedEndDateBeforeStartDate);
        if (ActualStartDate.HasValue && ActualEndDate.HasValue && ActualEndDate.Value < ActualStartDate.Value)
            throw new BusinessException(MyERPDomainErrorCodes.ActualEndDateBeforeStartDate);
    }

    public void Submit()
    {
        if (Status != WorkOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        ValidateDates();
        Status = WorkOrderStatus.Submitted;
    }

    public void Start()
    {
        if (Status is not (WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.InProcess;
        ActualStartDate ??= DateTime.UtcNow;
    }

    public void RecordProduction(decimal quantity, decimal overproductionPercentage = 0, decimal processLoss = 0)
    {
        if (Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Overproduction check: cannot exceed qty × (1 + overproduction_pct/100)
        var maxAllowed = Quantity * (1 + overproductionPercentage / 100m);
        if (ProducedQuantity + quantity > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", maxAllowed)
                .WithData("produced", ProducedQuantity)
                .WithData("attempted", quantity);
        }

        ProducedQuantity += quantity;
        if (processLoss > 0)
        {
            ProcessLossQty += processLoss;
        }

        // Per ERPNext PR #57895 / #57903 / commit 0eb61c9fac:
        // Completion is reached when ProducedQuantity + ProcessLossQty covers ordered Quantity
        if (ProducedQuantity + ProcessLossQty >= Quantity)
        {
            Status = WorkOrderStatus.Completed;
            ActualEndDate = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Refreshes process loss quantity and checks completion for semi-finished goods tracking.
    /// Per ERPNext commit 0eb61c9fac / PR #57895.
    /// </summary>
    public void SetProcessLossQty(decimal totalProcessLoss)
    {
        ProcessLossQty = totalProcessLoss;
        if (ProducedQuantity + ProcessLossQty >= Quantity && Status != WorkOrderStatus.Cancelled && Status != WorkOrderStatus.Draft)
        {
            Status = WorkOrderStatus.Completed;
            ActualEndDate ??= DateTime.UtcNow;
        }
    }

    public void RecordMaterialTransfer(decimal quantity)
    {
        MaterialTransferred += quantity;
        if (Status == WorkOrderStatus.Submitted && quantity > 0)
            Status = WorkOrderStatus.NotStarted;
    }

    /// <summary>Records disassembly qty against produced goods. Per ERPNext: disassembled_qty tracked on WO.</summary>
    public void RecordDisassembly(decimal quantity)
    {
        if (quantity <= 0) return;
        var availableForDisassembly = ProducedQuantity - DisassembledQuantity;
        if (quantity > availableForDisassembly)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", availableForDisassembly)
                .WithData("attempted", quantity);
        DisassembledQuantity += quantity;
    }

    /// <summary>Reverses disassembly qty on cancellation. Per ERPNext PR #48184 / commit 3e4d160626.</summary>
    public void ReverseDisassembly(decimal quantity)
    {
        if (quantity <= 0) return;
        DisassembledQuantity = Math.Max(0, DisassembledQuantity - quantity);
    }

    public void Stop()
    {
        if (Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Stopped;
    }

    public void Unstop()
    {
        if (Status != WorkOrderStatus.Stopped)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.InProcess;
    }

    public void Close()
    {
        if (Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Closed;
    }

    public void Cancel()
    {
        // Per DO-NOT: "Cancel Stopped Work Order directly (must Unstop first, then cancel)"
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled or WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Cancelled;
    }
}
