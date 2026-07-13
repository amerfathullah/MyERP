using System;
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

    /// <summary>Workstation type (for auto-assignment when specific workstation not set).</summary>
    public string? WorkstationType { get; set; }

    /// <summary>If true, Job Cards split by batch_size instead of full qty.</summary>
    public bool CreateJobCardBasedOnBatchSize { get; set; }

    /// <summary>Batch size for Job Card splitting.</summary>
    public int BatchSize { get; set; }

    /// <summary>Quality inspection template (auto-requires QI on this op's Job Card).</summary>
    public Guid? QualityInspectionTemplateId { get; set; }

    /// <summary>If true, this is a corrective/rework operation.</summary>
    public bool IsCorrectiveOperation { get; set; }

    public bool IsActive { get; set; } = true;

    protected Operation() { }

    public Operation(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }
}
