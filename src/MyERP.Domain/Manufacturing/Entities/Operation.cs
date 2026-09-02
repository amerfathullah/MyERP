using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Operation — a manufacturing step (e.g., "Cutting", "Welding", "Assembly").
/// Used by Routing to define sequence of operations for a BOM.
/// Maps to ERPNext manufacturing/doctype/operation.
/// </summary>
public class Operation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Default workstation for this operation.</summary>
    public Guid? WorkstationId { get; set; }

    /// <summary>Denormalized display name of the linked WorkstationType, if any.</summary>
    public string? WorkstationType { get; set; }

    /// <summary>Default workstation type (for auto-assignment when a specific workstation isn't set).</summary>
    public Guid? WorkstationTypeId { get; set; }

    /// <summary>If true, Job Cards split by batch_size instead of full qty.</summary>
    public bool CreateJobCardBasedOnBatchSize { get; set; }

    /// <summary>Batch size for Job Card splitting.</summary>
    public int BatchSize { get; set; }

    /// <summary>Quality inspection template (auto-requires QI on this op's Job Card).</summary>
    public Guid? QualityInspectionTemplateId { get; set; }

    /// <summary>If true, this is a corrective/rework operation.</summary>
    public bool IsCorrectiveOperation { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Auto-calculated: SUM of all sub-operation TimeInMins.</summary>
    public decimal TotalOperationTime { get; private set; }

    private readonly List<SubOperation> _subOperations = new();
    public IReadOnlyList<SubOperation> SubOperations => _subOperations.AsReadOnly();

    protected Operation() { }

    public Operation(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    /// <summary>Replaces the sub-operation breakdown and recalculates TotalOperationTime.</summary>
    public void SetSubOperations(IEnumerable<(Guid OperationId, decimal TimeInMins, string? Description)> rows)
    {
        _subOperations.Clear();
        foreach (var row in rows)
        {
            Check.NotDefaultOrNull<Guid>(row.OperationId, nameof(row.OperationId));
            _subOperations.Add(new SubOperation(Guid.NewGuid(), Id, row.OperationId, row.TimeInMins, row.Description));
        }
        TotalOperationTime = _subOperations.Sum(s => s.TimeInMins);
    }
}

/// <summary>
/// Sub Operation — a step composed of another Operation, used to build up
/// TotalOperationTime for the parent Operation. Maps to ERPNext manufacturing/doctype/sub_operation.
/// </summary>
public class SubOperation : FullAuditedEntity<Guid>
{
    public Guid ParentOperationId { get; set; }

    /// <summary>The Operation referenced as this step.</summary>
    public Guid OperationId { get; set; }

    public decimal TimeInMins { get; set; }
    public string? Description { get; set; }

    protected SubOperation() { }

    public SubOperation(Guid id, Guid parentOperationId, Guid operationId, decimal timeInMins, string? description) : base(id)
    {
        ParentOperationId = Check.NotDefaultOrNull<Guid>(parentOperationId, nameof(parentOperationId));
        OperationId = Check.NotDefaultOrNull<Guid>(operationId, nameof(operationId));
        TimeInMins = timeInMins;
        Description = description;
    }
}
