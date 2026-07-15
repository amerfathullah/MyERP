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
        Guid? workstationId = null, string? description = null)
    {
        // Sequence ID must be monotonically increasing
        if (_operations.Any() && sequenceId < _operations.Max(o => o.SequenceId))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Routing sequence_id must be monotonically increasing");

        _operations.Add(new RoutingOperation(Guid.NewGuid(), Id, operationId,
            sequenceId, timeInMins, workstationId, description));
    }

    public decimal GetTotalOperatingCost()
    {
        return _operations.Sum(o => o.OperatingCost);
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

    protected RoutingOperation() { }

    public RoutingOperation(Guid id, Guid routingId, Guid operationId,
        int sequenceId, decimal timeInMins, Guid? workstationId, string? description) : base(id)
    {
        RoutingId = routingId;
        OperationId = operationId;
        SequenceId = sequenceId;
        TimeInMins = timeInMins;
        WorkstationId = workstationId;
        Description = description;
        OperatingCost = 0; // Calculated when hour_rate is resolved
    }

    public void CalculateCost(decimal hourRate)
    {
        HourRate = hourRate;
        OperatingCost = hourRate * TimeInMins / 60m;
    }
}
