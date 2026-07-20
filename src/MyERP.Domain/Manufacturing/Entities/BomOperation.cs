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

    protected BomOperation() { }

    public BomOperation(Guid id, Guid bomId, Guid operationId, int sequenceId,
        decimal timeInMins, Guid? workstationId = null, Guid? tenantId = null)
        : base(id)
    {
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

    /// <summary>Total time for a given production quantity including fixed time.</summary>
    public decimal GetTotalTime(decimal quantity) => FixedTime + (TimeInMins * quantity);

    /// <summary>Number of Job Cards needed for this operation given WO quantity.</summary>
    public int GetJobCardCount(decimal woQty)
    {
        if (BatchSize <= 0) return 1;
        return (int)Math.Ceiling(woQty / BatchSize);
    }
}
