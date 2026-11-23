using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// BOM Operation — links a Bill of Materials to a specific manufacturing operation.
/// Enables per-operation cost tracking, Job Card splitting by batch size, and
/// sequence-based scheduling. Operations on a BOM can differ from the Routing template.
/// Maps to ERPNext manufacturing/doctype/bom_operation (child of BOM).
/// </summary>
public class BomOperation : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid BomId { get; set; }

    /// <summary>Reference to the Operation master.</summary>
    public Guid OperationId { get; set; }

    /// <summary>Workstation where this operation is performed.</summary>
    public Guid? WorkstationId { get; set; }

    /// <summary>Workstation Type for operations performed on a class of workstations.</summary>
    public Guid? WorkstationTypeId { get; set; }

    /// <summary>Sequence in manufacturing process (must be monotonically increasing within BOM).</summary>
    public int SequenceId { get; set; }

    /// <summary>Standard time per unit (minutes).</summary>
    public decimal TimeInMins { get; set; }

    /// <summary>Operating cost per unit = TimeInMins × (workstation hour_rate / 60).</summary>
    public decimal OperatingCost { get; set; }

    /// <summary>Number of units processed per Job Card (for batch splitting).</summary>
    public int BatchSize { get; set; } = 0; // 0 = full WO qty in single JC

    /// <summary>Fixed cycle time regardless of quantity (setup time).</summary>
    public decimal FixedTime { get; set; }

    /// <summary>Description/notes for this operation step.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this is a subcontracted operation (done externally).</summary>
    public bool IsSubcontracted { get; set; }

    /// <summary>FG or Semi-FG item produced by this operation (per PR #57885).</summary>
    public Guid? FinishedGoodItemId { get; set; }

    /// <summary>Whether this is the final finished good operation of the BOM.</summary>
    public bool IsFinalFinishedGood { get; set; }

    /// <summary>Whether quality inspection is required for this specific operation (per ERPNext PR #53859).</summary>
    public bool QualityInspectionRequired { get; set; }

    /// <summary>
    /// On completion of the Job Card, split the consumed batch into one child batch per finished piece.
    /// Maps to ERPNext manufacturing/doctype/bom_operation/bom_operation.json (batch_split).
    /// </summary>
    public bool BatchSplit { get; set; }

    /// <summary>
    /// Weight per piece when batch splitting is enabled. Non-negative.
    /// Maps to ERPNext manufacturing/doctype/bom_operation/bom_operation.json (weight_per_piece).
    /// </summary>
    public decimal? WeightPerPiece { get; set; }

    /// <summary>
    /// When true, operation cost on the BOM is scaled by BOM Quantity / BatchSize
    /// (ERPNext set_cost_based_on_bom_qty, commits 22d504f5b6, e7be233789, f406f6e2b8).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool SetCostBasedOnBomQty { get; set; }

    /// <summary>
    /// Target finished-good warehouse for this operation (ERPNext fg_warehouse, commits aba50eb01d, 46c37b5eef).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid? TargetWarehouseId { get; set; }

    /// <summary>
    /// Unrounded operating cost per unit = OperatingCost / (BatchSize &gt; 0 ? BatchSize : 1)
    /// (ERPNext commit e7be233789).
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal CostPerUnit => OperatingCost / (BatchSize > 0 ? BatchSize : 1);

    protected BomOperation() { }

    public BomOperation(Guid id, Guid bomId, Guid operationId, int sequenceId,
        decimal timeInMins, Guid? workstationId = null, Guid? tenantId = null)
        : base(id)
    {
        // Prevent negative operation time (ERPNext commit 3a80e116e8 / PR #48696)
        if (timeInMins < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(timeInMins));

        BomId = bomId;
        OperationId = operationId;
        SequenceId = sequenceId;
        TimeInMins = timeInMins;
        WorkstationId = workstationId;
        TenantId = tenantId;
    }

    /// <summary>Calculate operating cost from workstation hour rate.</summary>
    public void CalculateCost(decimal workstationHourRate)
    {
        OperatingCost = (TimeInMins / 60m) * workstationHourRate;
    }

    /// <summary>
    /// Effective unrounded operating cost contributed to BOM total operating cost.
    /// When SetCostBasedOnBomQty is enabled, scales unrounded CostPerUnit by bomQuantity
    /// (ERPNext commits 22d504f5b6, e7be233789, f406f6e2b8).
    /// </summary>
    public decimal GetEffectiveOperatingCost(decimal bomQuantity)
    {
        if (SetCostBasedOnBomQty)
        {
            return CostPerUnit * (bomQuantity > 0 ? bomQuantity : 1m);
        }

        return OperatingCost;
    }

    /// <summary>Total time for a given production quantity including fixed time.</summary>
    public decimal GetTotalTime(decimal quantity) => FixedTime + (TimeInMins * quantity);

    /// <summary>Number of Job Cards needed for this operation given WO quantity.</summary>
    public int GetJobCardCount(decimal woQty)
    {
        if (BatchSize <= 0) return 1;
        return (int)Math.Ceiling(woQty / BatchSize);
    }

    /// <summary>
    /// Computes scheduled operation duration in minutes for a Production Plan quantity,
    /// scaling per batch when <paramref name="createJobCardBasedOnBatchSize"/> is enabled and
    /// <paramref name="batchSize"/> is positive (instead of dividing by <paramref name="bomQty"/>).
    /// Per ERPNext PR #68876 (<c>get_operation_duration</c> / <c>get_operation_time_per_unit</c>).
    /// </summary>
    public static decimal CalculateScheduledDurationMins(
        decimal timeInMins,
        decimal qty,
        decimal bomQty,
        bool isFixedTime,
        bool createJobCardBasedOnBatchSize,
        decimal batchSize,
        int precision = 4)
    {
        if (isFixedTime)
        {
            return Math.Round(timeInMins, precision, MidpointRounding.AwayFromZero);
        }

        decimal timePerUnit;
        if (createJobCardBasedOnBatchSize && batchSize > 0m)
        {
            timePerUnit = timeInMins / batchSize;
        }
        else
        {
            var divisor = bomQty > 0m ? bomQty : 1m;
            timePerUnit = timeInMins / divisor;
        }

        return Math.Round(timePerUnit * qty, precision, MidpointRounding.AwayFromZero);
    }
}
