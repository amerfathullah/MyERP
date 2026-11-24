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
    public Guid? ProductionPlanId { get; set; }
    public Guid? ProductionPlanItemId { get; set; }
    public Guid? ProductionPlanSubAssemblyItemId { get; set; }
    public Guid? MaterialRequestId { get; set; }
    public Guid? MaterialRequestItemId { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public Guid? WipWarehouseId { get; set; }
    public Guid? FgWarehouseId { get; set; }
    public Guid? ScrapWarehouseId { get; set; }

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    /// <summary>
    /// Lead time in minutes calculated from actual start and end dates.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (lead_time).
    /// </summary>
    public decimal? LeadTime { get; set; }

    public string? Notes { get; set; }
    public bool TrackSemiFinishedGoods { get; set; }
    public bool SkipTransfer { get; set; }
    /// <summary>Backflush from WIP warehouse even when skip_transfer is enabled (ERPNext PR #49280 / commit fe0722c4f1).</summary>
    public bool FromWipWarehouse { get; set; }
    /// <summary>
    /// Whether to reserve raw material stock when Work Order is submitted.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (reserve_stock).
    /// </summary>
    public bool ReserveStock { get; set; }

    /// <summary>
    /// Allow substituting required items with registered Item Alternatives during material transfer/consumption.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (allow_alternative_item).
    /// </summary>
    public bool AllowAlternativeItem { get; set; }

    /// <summary>
    /// Batch size for manufacturing.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (batch_size).
    /// </summary>
    public decimal BatchSize { get; set; } = 0m;

    /// <summary>
    /// Planned operating cost calculated from operations.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (planned_operating_cost).
    /// </summary>
    public decimal PlannedOperatingCost { get; set; } = 0m;

    /// <summary>
    /// Actual operating cost recorded from Job Cards.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (actual_operating_cost).
    /// </summary>
    public decimal ActualOperatingCost { get; set; } = 0m;

    /// <summary>
    /// Additional operating cost for the work order, distributed to finished goods stock entry.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (additional_operating_cost).
    /// </summary>
    public decimal AdditionalOperatingCost { get; set; } = 0m;

    /// <summary>
    /// Corrective operation cost from corrective Job Cards.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (corrective_operation_cost).
    /// </summary>
    public decimal CorrectiveOperationCost { get; set; } = 0m;

    /// <summary>
    /// Total operating cost = AdditionalOperatingCost + (ActualOperatingCost != 0 ? ActualOperatingCost : PlannedOperatingCost) + CorrectiveOperationCost.
    /// Maps to ERPNext manufacturing/doctype/work_order/work_order.json (total_operating_cost).
    /// </summary>
    public decimal TotalOperatingCost { get; set; } = 0m;

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

    public void ClearRequiredItems()
    {
        if (Status != WorkOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        RequiredItems.Clear();
    }

    /// <summary>
    /// Recalculates planned and total operating costs.
    /// Maps to ERPNext OperationsService.calculate_operating_cost.
    /// Resets operating costs when draft Work Order changes to a BOM without operations (PR #64987 / commit 00a1268b74).
    /// </summary>
    public void CalculateOperatingCost(decimal plannedOperatingCostFromOperations = 0m)
    {
        PlannedOperatingCost = Math.Max(0m, Math.Round(plannedOperatingCostFromOperations, 4));
        var variableCost = ActualOperatingCost != 0m ? ActualOperatingCost : PlannedOperatingCost;
        TotalOperatingCost = Math.Round(AdditionalOperatingCost + variableCost + CorrectiveOperationCost, 4);
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

    /// <summary>
    /// Validates batch size cannot be negative.
    /// Per ERPNext PR #60647 / commit 3d70a00f40.
    /// </summary>
    public void ValidateBatchSize()
    {
        if (BatchSize < 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.BatchSizeCannotBeNegative)
                .WithData("batchSize", BatchSize);
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
        ValidateBatchSize();
        ValidateDates();
        Status = WorkOrderStatus.Submitted;
        if (ReserveStock)
        {
            UpdateReservationStatus();
        }
    }

    /// <summary>
    /// Calculates reservation status when Work Order is submitted and ReserveStock is enabled.
    /// Per ERPNext PR #60161 / commit 1334acafe2 and PR #69243 (<c>status.py: _reservation_status</c>):
    /// - Counts both <c>StockReservedQty</c> and any <c>SubcontractedQty</c> taken over by a Subcontracting Order.
    /// - If no items have reservations or subcontracted qty, status remains unchanged.
    /// - If any required item has unreserved qty (RequiredQuantity - subcontractedQty - StockReservedQty) &gt; 0 at stock precision, status is StockPartiallyReserved.
    /// - Otherwise, status is StockReserved.
    /// </summary>
    public WorkOrderStatus GetReservationStatus(WorkOrderStatus fallbackStatus = WorkOrderStatus.NotStarted)
        => GetReservationStatus(fallbackStatus, subcontractedQtyByRowId: null, precision: 4);

    public WorkOrderStatus GetReservationStatus(
        WorkOrderStatus fallbackStatus,
        IReadOnlyDictionary<Guid, decimal>? subcontractedQtyByRowId,
        int precision = 4)
    {
        if (!ReserveStock || !RequiredItems.Any())
            return fallbackStatus;

        decimal GetSubcontracted(WorkOrderItem row) =>
            subcontractedQtyByRowId != null && subcontractedQtyByRowId.TryGetValue(row.Id, out var qty) ? qty : 0m;

        if (!RequiredItems.Any(r => r.StockReservedQty > 0m || GetSubcontracted(r) > 0m))
            return fallbackStatus;

        // Per ERPNext PR #66965 & PR #69243: round unreserved qty (required - subcontracted - reserved) at stock precision.
        if (RequiredItems.Any(r =>
            Math.Round(r.RequiredQuantity - GetSubcontracted(r) - r.StockReservedQty, precision, MidpointRounding.AwayFromZero) > 0m))
            return WorkOrderStatus.StockPartiallyReserved;

        return WorkOrderStatus.StockReserved;
    }

    public void UpdateReservationStatus()
    {
        if (Status is WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted or WorkOrderStatus.StockReserved or WorkOrderStatus.StockPartiallyReserved)
        {
            Status = GetReservationStatus(WorkOrderStatus.NotStarted);
        }
    }

    /// <summary>
    /// Refreshes <see cref="Status"/> to <paramref name="computedStatus"/> only when it changed,
    /// <paramref name="savedStatus"/> still equals the current <see cref="Status"/> (no concurrent
    /// transaction changed it), and <paramref name="savedStatus"/> is neither <see cref="WorkOrderStatus.Stopped"/>
    /// nor <see cref="WorkOrderStatus.Closed"/>.
    /// Per ERPNext PR #69312 (<c>StatusService.refresh_status</c> in <c>work_order/services/status.py</c>).
    /// </summary>
    public bool TryRefreshStatus(WorkOrderStatus savedStatus, WorkOrderStatus computedStatus)
    {
        if (computedStatus == Status)
        {
            return false;
        }

        if (savedStatus != Status || savedStatus is WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
        {
            return false;
        }

        Status = computedStatus;
        return true;
    }

    public void Start()
    {
        if (Status is not (WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted or WorkOrderStatus.StockReserved or WorkOrderStatus.StockPartiallyReserved))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.InProcess;
        ActualStartDate ??= DateTime.UtcNow;
    }

    public void RecordProduction(decimal quantity, decimal overproductionPercentage = 0, decimal processLoss = 0)
    {
        if (Status != WorkOrderStatus.InProcess)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Overproduction check: cannot exceed qty × (1 + overproduction_pct/100)
        // Per ERPNext PR #58847: cumulative manufactured quantity including process loss
        var maxAllowed = Quantity * (1 + overproductionPercentage / 100m);
        var totalManufactured = ProducedQuantity + ProcessLossQty + quantity + processLoss;
        if (totalManufactured > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", maxAllowed)
                .WithData("produced", ProducedQuantity + ProcessLossQty)
                .WithData("attempted", quantity + processLoss);
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
            CalculateLeadTime();
        }
    }

    /// <summary>
    /// Sets actual start and end dates and recalculates lead time.
    /// Per ERPNext PR #60196 / commit 1e3b9e39be.
    /// </summary>
    public void SetActualDates(DateTime? actualStartDate, DateTime? actualEndDate)
    {
        ActualStartDate = actualStartDate;
        ActualEndDate = actualEndDate;
        CalculateLeadTime();
    }

    /// <summary>
    /// Calculates lead time in minutes from actual start and end dates.
    /// Maps to ERPNext manufacturing/doctype/work_order/services/operations.py (set_lead_time).
    /// </summary>
    public void CalculateLeadTime()
    {
        if (ActualStartDate.HasValue && ActualEndDate.HasValue && ActualEndDate.Value >= ActualStartDate.Value)
        {
            var diffInMinutes = (decimal)(ActualEndDate.Value - ActualStartDate.Value).TotalMinutes;
            LeadTime = Math.Max(0m, Math.Round(diffInMinutes, 2));
        }
        else
        {
            LeadTime = null;
        }
    }

    /// <summary>
    /// Reverses recorded production and process loss (e.g. on Stock Entry cancellation).
    /// Reopens the Work Order to InProcess if below ordered Quantity.
    /// Per ERPNext PR #60421 (commit 00ca106bf5): manufactured qty cannot be less than disassembled qty.
    /// </summary>
    public void ReverseProduction(decimal quantity, decimal processLoss = 0)
    {
        var newProducedQuantity = Math.Max(0, ProducedQuantity - quantity);
        if (newProducedQuantity < DisassembledQuantity)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ManufactureCannotBeLessThanDisassembled)
                .WithData("produced", newProducedQuantity)
                .WithData("disassembled", DisassembledQuantity)
                .WithData("workOrder", WorkOrderNumber ?? Id.ToString());
        }

        ProducedQuantity = newProducedQuantity;
        if (processLoss > 0)
        {
            ProcessLossQty = Math.Max(0, ProcessLossQty - processLoss);
        }

        if (Status == WorkOrderStatus.Completed && (ProducedQuantity + ProcessLossQty) < Quantity)
        {
            Status = WorkOrderStatus.InProcess;
            ActualEndDate = null;
            LeadTime = null;
        }
    }

    /// <summary>
    /// Refreshes process loss quantity and checks completion for semi-finished goods tracking.
    /// Per ERPNext commit 0eb61c9fac / PR #57895.
    /// </summary>
    public void SetProcessLossQty(decimal totalProcessLoss, decimal overproductionPercentage = 0)
    {
        var maxAllowed = Quantity * (1 + overproductionPercentage / 100m);
        var totalManufactured = ProducedQuantity + totalProcessLoss;
        if (totalManufactured > maxAllowed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderOverproduction)
                .WithData("maxAllowed", maxAllowed)
                .WithData("produced", ProducedQuantity + ProcessLossQty)
                .WithData("attempted", totalProcessLoss);
        }

        ProcessLossQty = totalProcessLoss;
        if (ProducedQuantity + ProcessLossQty >= Quantity
            && Status != WorkOrderStatus.Cancelled
            && Status != WorkOrderStatus.Draft
            && Status != WorkOrderStatus.Closed
            && Status != WorkOrderStatus.Stopped)
        {
            Status = WorkOrderStatus.Completed;
            ActualEndDate ??= DateTime.UtcNow;
            CalculateLeadTime();
        }
    }

    public void RecordMaterialTransfer(decimal quantity)
    {
        MaterialTransferred += quantity;
        if ((Status == WorkOrderStatus.Submitted || Status == WorkOrderStatus.StockReserved || Status == WorkOrderStatus.StockPartiallyReserved) && quantity > 0)
            Status = WorkOrderStatus.NotStarted;
    }

    /// <summary>
    /// Marks a submitted/not-started/reserved Work Order as InProcess once material is consumed
    /// (e.g. a skip-transfer Work Order after a posted Material Consumption for Manufacture entry).
    /// Per ERPNext PR #65858 (commit bdb3a4e16c).
    /// </summary>
    public void RecordMaterialConsumption()
    {
        if (Status is WorkOrderStatus.Submitted or WorkOrderStatus.NotStarted
            or WorkOrderStatus.StockReserved or WorkOrderStatus.StockPartiallyReserved
            && RequiredItems.Any(r => r.ConsumedQuantity > 0))
        {
            Status = WorkOrderStatus.InProcess;
            ActualStartDate ??= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Reverts an InProcess Work Order back to NotStarted (or reservation status) when all
    /// consumed, transferred, and produced quantities have been reversed.
    /// </summary>
    public void ReverseMaterialConsumptionStatus()
    {
        if (Status == WorkOrderStatus.InProcess
            && MaterialTransferred <= 0
            && ProducedQuantity <= 0
            && ProcessLossQty <= 0
            && !RequiredItems.Any(r => r.ConsumedQuantity > 0 || r.TransferredQuantity > 0))
        {
            Status = GetReservationStatus(WorkOrderStatus.NotStarted);
        }
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
        if (Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("status", Status)
                .WithData("detail", "Only a submitted Work Order can be closed, stopped or resumed");

        if (Status == WorkOrderStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("detail", "Closed Work Order can not be stopped or Re-opened");

        Status = WorkOrderStatus.Stopped;
    }

    public void Unstop()
    {
        if (Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("status", Status)
                .WithData("detail", "Only a submitted Work Order can be closed, stopped or resumed");

        if (Status == WorkOrderStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("detail", "Closed Work Order can not be stopped or Re-opened");

        if (Status != WorkOrderStatus.Stopped)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("status", Status)
                .WithData("detail", "Work Order is not stopped");

        // Per ERPNext PR #60647 & PR #65858 / services/status.py: resume to InProcess if started, material transferred/consumed, or items produced, else NotStarted
        Status = (ActualStartDate.HasValue || MaterialTransferred > 0 || ProducedQuantity > 0 || RequiredItems.Any(r => r.ConsumedQuantity > 0))
            ? WorkOrderStatus.InProcess
            : WorkOrderStatus.NotStarted;
    }

    public void Close()
    {
        if (Status is WorkOrderStatus.Draft or WorkOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("status", Status)
                .WithData("detail", "Only a submitted Work Order can be closed, stopped or resumed");

        if (Status == WorkOrderStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted)
                .WithData("workOrder", WorkOrderNumber ?? Id.ToString())
                .WithData("detail", $"Work Order {WorkOrderNumber ?? Id.ToString()} is already Closed");

        Status = WorkOrderStatus.Closed;
    }

    public void Cancel()
    {
        // Per DO-NOT: "Cancel Stopped Work Order directly (must Unstop first, then cancel)"
        if (Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled or WorkOrderStatus.Stopped or WorkOrderStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = WorkOrderStatus.Cancelled;
    }

    /// <summary>
    /// Determines whether this Completed or Closed Work Order has unconsumed transferred components
    /// remaining in WIP that can be returned.
    /// Per ERPNext PR #67351 (commit db34b763e9):
    /// Aggregates <c>TransferredQuantity - ConsumedQuantity</c> per item across <see cref="RequiredItems"/>
    /// because rows of one item split transfers by source warehouse while consumption and returns are distributed
    /// by required qty.
    /// </summary>
    public bool HasReturnableComponents(int precision = 4)
    {
        if (Status is not (WorkOrderStatus.Completed or WorkOrderStatus.Closed))
            return false;

        var pendingByItem = new Dictionary<Guid, decimal>();
        foreach (var row in RequiredItems)
        {
            pendingByItem[row.ItemId] = pendingByItem.GetValueOrDefault(row.ItemId, 0m)
                + row.TransferredQuantity
                - row.ConsumedQuantity;
        }

        return pendingByItem.Values.Any(qty => Math.Round(qty, precision) > 0m);
    }

    /// <summary>
    /// Computes the Work Order's <c>CorrectiveOperationCost</c> (rounded after summing all submitted corrective Job Cards)
    /// and <c>TotalOperatingCost</c> (rounded to field precision) in company currency
    /// per ERPNext commits 027c7a7a4e and ebf97959c8 (<c>job_card.py</c>, <c>work_order/services/operations.py</c>).
    /// </summary>
    public static (decimal CorrectiveOperationCost, decimal TotalOperatingCost) CalculateCorrectiveAndTotalOperatingCost(
        IEnumerable<(decimal TotalTimeInMins, decimal HourRate)>? submittedCorrectiveJobCards,
        decimal actualOperatingCost,
        decimal plannedOperatingCost,
        decimal additionalOperatingCost,
        int precision = 2)
    {
        var rawCorrective = submittedCorrectiveJobCards != null
            ? submittedCorrectiveJobCards.Sum(jc => (jc.TotalTimeInMins / 60m) * jc.HourRate)
            : 0m;
        var correctiveCost = Math.Round(rawCorrective, precision, MidpointRounding.AwayFromZero);

        var variableCost = actualOperatingCost != 0m ? actualOperatingCost : plannedOperatingCost;
        var totalOperatingCost = Math.Round(
            additionalOperatingCost + variableCost + correctiveCost,
            precision,
            MidpointRounding.AwayFromZero);

        return (correctiveCost, totalOperatingCost);
    }

    /// <summary>
    /// Resolves the Work Order's <c>PlannedEndDate</c> from the latest <c>PlannedEndTime</c> across all operations
    /// (since parallel operations with the same Sequence ID may end after the last operation row)
    /// per ERPNext commit 2ce29e04ba (<c>work_order/services/operations.py</c>).
    /// </summary>
    public static DateTime? ResolvePlannedEndDateFromOperations(IEnumerable<DateTime?>? operationPlannedEndTimes)
    {
        if (operationPlannedEndTimes == null)
        {
            return null;
        }

        var valid = operationPlannedEndTimes
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();

        return valid.Count > 0 ? valid.Max() : null;
    }

    /// <summary>
    /// Sums the Work Order quantity against a specific Sales Order row (<paramref name="salesOrderItemId"/>),
    /// always filtering by <paramref name="salesOrderItemId"/> even when <paramref name="productBundleItemId"/> is set
    /// so a Product Bundle line counts only its own Work Orders per ERPNext commit c6482b7a6c (<c>work_order/services/status.py</c>).
    /// </summary>
    public static decimal CalculateSalesOrderItemWorkOrderQty(
        Guid salesOrderId,
        Guid salesOrderItemId,
        Guid productionItemId,
        Guid? productBundleItemId,
        IEnumerable<(Guid SalesOrderId, Guid? SalesOrderItemId, Guid ProductionItemId, Guid? ProductBundleItemId, bool IsSubmitted, decimal QtyAgainstSalesOrder)>? workOrders)
    {
        if (workOrders == null)
        {
            return 0m;
        }

        return workOrders
            .Where(wo => wo.IsSubmitted
                && wo.SalesOrderId == salesOrderId
                && wo.SalesOrderItemId == salesOrderItemId
                && (productBundleItemId.HasValue && productBundleItemId.Value != Guid.Empty
                    ? wo.ProductBundleItemId == productBundleItemId.Value
                    : wo.ProductionItemId == productionItemId))
            .Sum(wo => wo.QtyAgainstSalesOrder);
    }

    /// <summary>
    /// Computes the remaining finished good quantity available to disassemble against a Manufacture Stock Entry,
    /// subtracting <paramref name="sourceProcessLossQty"/> from <paramref name="sourceFgCompletedQty"/>
    /// per ERPNext commit 3043a78686 (<c>work_order.py</c>, <c>stock_entry/services/disassemble.py</c>).
    /// </summary>
    public static decimal CalculateDisassemblyAvailableQtyFromManufactureEntry(
        decimal sourceFgCompletedQty,
        decimal sourceProcessLossQty,
        decimal alreadyDisassembledQty,
        int precision = 4)
    {
        return Math.Round(
            sourceFgCompletedQty - sourceProcessLossQty - alreadyDisassembledQty,
            precision,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes available finished-good quantity for disassembly or alternative-item conversion,
    /// subtracting BOTH <paramref name="disassembledQty"/> and <paramref name="convertedFgQty"/>
    /// from <paramref name="producedQty"/> so disassembled and converted units mutually reduce available qty.
    /// Per ERPNext PR #67345 (<c>get_available_fg_qty</c>).
    /// </summary>
    public static decimal CalculateAvailableFgQtyForDisassemblyOrConversion(
        decimal producedQty,
        decimal disassembledQty,
        decimal convertedFgQty,
        int precision = 4)
    {
        return Math.Max(
            Math.Round(producedQty - disassembledQty - convertedFgQty, precision, MidpointRounding.AwayFromZero),
            0m);
    }

    /// <summary>
    /// Computes Work Order <c>material_transferred_for_manufacturing</c> when Pick List transfers
    /// (with <c>fg_completed_qty == 0</c>) coexist with direct Stock Entry transfers (<c>fg_completed_qty &gt; 0</c>):
    /// takes <c>max(min(minFraction, 1) * woQty, sumFgCompletedQty)</c> when Pick List transfers exist.
    /// Per ERPNext PR #67344 (<c>required_items.py</c>).
    /// </summary>
    public static decimal CalculateMaterialTransferredQtyWithPickList(
        decimal woQty,
        decimal sumFgCompletedQty,
        bool hasTransferWithoutForQuantity,
        IEnumerable<(Guid ItemId, decimal RequiredQty, decimal TransferredQty)>? requiredItems,
        int precision = 4)
    {
        if (sumFgCompletedQty > 0m && !hasTransferWithoutForQuantity)
        {
            return Math.Round(sumFgCompletedQty, precision, MidpointRounding.AwayFromZero);
        }

        var requiredByItem = new Dictionary<Guid, decimal>();
        var transferredByItem = new Dictionary<Guid, decimal>();
        if (requiredItems != null)
        {
            foreach (var row in requiredItems)
            {
                if (row.RequiredQty <= 0m) continue;
                requiredByItem[row.ItemId] = requiredByItem.GetValueOrDefault(row.ItemId, 0m) + row.RequiredQty;
                transferredByItem[row.ItemId] = transferredByItem.GetValueOrDefault(row.ItemId, 0m) + row.TransferredQty;
            }
        }

        if (requiredByItem.Count == 0)
        {
            return Math.Round(sumFgCompletedQty, precision, MidpointRounding.AwayFromZero);
        }

        var minFraction = requiredByItem.Min(kv => transferredByItem.GetValueOrDefault(kv.Key, 0m) / kv.Value);
        minFraction = Math.Min(Math.Max(minFraction, 0m), 1m);
        var transferred = Math.Max(minFraction * woQty, sumFgCompletedQty);
        return Math.Round(transferred, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes the remaining unreserved quantity of a Work Order required item, excluding stock already
    /// taken over by a Subcontracting Order (<paramref name="subcontractedQty"/>) and stock already
    /// reserved, consumed, or transferred to WIP.
    /// Per ERPNext PR #68805 (<c>get_work_order_unreserved_qty</c>).
    /// </summary>
    public static decimal CalculateWorkOrderUnreservedQty(
        decimal requiredQty,
        decimal subcontractedQty,
        decimal stockReservedQty,
        decimal consumedQty,
        decimal transferredQty,
        int precision = 4)
    {
        var coveredQty = Math.Max(stockReservedQty + consumedQty, transferredQty);
        return Math.Max(
            Math.Round(requiredQty - subcontractedQty - coveredQty, precision, MidpointRounding.AwayFromZero),
            0m);
    }
}
