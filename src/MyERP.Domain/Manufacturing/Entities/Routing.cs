using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Routing — a sequence of operations applied to a BOM.
/// Sequence IDs must be monotonically increasing.
/// Maps to ERPNext manufacturing/doctype/routing.
/// </summary>
public class Routing : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public bool IsDisabled { get; set; }

    private readonly List<RoutingOperation> _operations = new();
    public IReadOnlyList<RoutingOperation> Operations => _operations.AsReadOnly();

    protected Routing() { }

    public Routing(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    public void AddOperation(Guid operationId, int sequenceId, decimal timeInMins,
        Guid? workstationId = null, string? description = null,
        bool batchSplit = false, decimal? weightPerPiece = null)
    {
        // Sequence ID must be monotonically increasing
        if (_operations.Any() && sequenceId < _operations.Max(o => o.SequenceId))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Routing sequence_id must be monotonically increasing");

        _operations.Add(new RoutingOperation(Guid.NewGuid(), Id, operationId,
            sequenceId, timeInMins, workstationId, description, batchSplit, weightPerPiece));
    }

    public decimal GetTotalOperatingCost()
    {
        return _operations.Sum(o => o.OperatingCost);
    }

    /// <summary>Replaces the full operations list (used by Update).</summary>
    public void ReplaceOperations(IEnumerable<(Guid OperationId, int SequenceId, decimal TimeInMins, Guid? WorkstationId, string? Description, bool BatchSplit, decimal? WeightPerPiece)> rows)
    {
        _operations.Clear();
        var lastSequence = -1;
        foreach (var row in rows)
        {
            if (row.SequenceId < lastSequence)
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("detail", "Routing sequence_id must be monotonically increasing");
            _operations.Add(new RoutingOperation(Guid.NewGuid(), Id, row.OperationId,
                row.SequenceId, row.TimeInMins, row.WorkstationId, row.Description, row.BatchSplit, row.WeightPerPiece));
            lastSequence = row.SequenceId;
        }
    }

    public void ReplaceOperations(IEnumerable<(Guid OperationId, int SequenceId, decimal TimeInMins, Guid? WorkstationId, string? Description)> rows)
    {
        ReplaceOperations(rows.Select(r => (r.OperationId, r.SequenceId, r.TimeInMins, r.WorkstationId, r.Description, false, (decimal?)null)));
    }
}

public class RoutingOperation : FullAuditedEntity<Guid>
{
    public Guid RoutingId { get; set; }
    public Guid OperationId { get; set; }
    public int SequenceId { get; set; }
    public decimal TimeInMins { get; set; }
    public Guid? WorkstationId { get; set; }
    public string? Description { get; set; }

    /// <summary>Auto-calculated: hour_rate × (time_in_mins / 60).</summary>
    public decimal OperatingCost { get; set; }

    /// <summary>Workstation hour rate (from Workstation or WorkstationType).</summary>
    public decimal HourRate { get; set; }

    public bool IsFixedTime { get; set; }

    /// <summary>
    /// Batch size for splitting Work Order into multiple Job Cards.
    /// 0 = no splitting (one JC per operation for full WO qty).
    /// </summary>
    public decimal BatchSize { get; set; }

    /// <summary>
    /// On completion of the Job Card, split the consumed batch into one child batch per finished piece.
    /// Maps to ERPNext manufacturing/doctype/routing (batch_split).
    /// </summary>
    public bool BatchSplit { get; set; }

    /// <summary>
    /// Weight per piece when batch splitting is enabled. Non-negative.
    /// Maps to ERPNext manufacturing/doctype/routing (weight_per_piece).
    /// </summary>
    public decimal? WeightPerPiece { get; set; }

    protected RoutingOperation() { }

    public RoutingOperation(Guid id, Guid routingId, Guid operationId,
        int sequenceId, decimal timeInMins, Guid? workstationId, string? description,
        bool batchSplit = false, decimal? weightPerPiece = null) : base(id)
    {
        RoutingId = routingId;
        OperationId = operationId;
        SequenceId = sequenceId;
        TimeInMins = timeInMins;
        WorkstationId = workstationId;
        Description = description;
        BatchSplit = batchSplit;
        WeightPerPiece = weightPerPiece;
        OperatingCost = 0; // Calculated when hour_rate is resolved
    }

    public void CalculateCost(decimal hourRate)
    {
        HourRate = hourRate;
        OperatingCost = hourRate * TimeInMins / 60m;
    }
}
