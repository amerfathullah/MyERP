using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Workstation Type — a reusable operating-cost template shared by multiple Workstations
/// (e.g. "CNC Machine", "Assembly Line"). Assigning a Workstation Type to a Workstation
/// copies its cost breakdown down onto that Workstation (per ERPNext workstation.py
/// _set_data_based_on_workstation_type — a one-time copy, not a live binding).
/// Maps to ERPNext manufacturing/doctype/workstation_type.
/// </summary>
public class WorkstationType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Auto-calculated: SUM of all cost component operating_cost.</summary>
    public decimal HourRate { get; private set; }

    private readonly List<WorkstationTypeCost> _costs = new();
    public IReadOnlyList<WorkstationTypeCost> Costs => _costs.AsReadOnly();

    protected WorkstationType() { }

    public WorkstationType(Guid id, string name, Guid? tenantId = null) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    /// <summary>Replaces the full cost-component breakdown and recalculates HourRate.</summary>
    public void ReplaceCosts(IEnumerable<(string Component, decimal OperatingCost)> rows)
    {
        var seen = new HashSet<string>();
        var list = rows.ToList();
        foreach (var row in list)
        {
            if (!seen.Add(row.Component))
                throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("detail", $"Duplicate cost component: {row.Component}");
        }

        _costs.Clear();
        foreach (var row in list)
            _costs.Add(new WorkstationTypeCost(Guid.NewGuid(), Id, row.Component, row.OperatingCost));
        RecalculateHourRate();
    }

    private void RecalculateHourRate()
    {
        HourRate = _costs.Sum(c => c.OperatingCost);
    }
}

public class WorkstationTypeCost : FullAuditedEntity<Guid>
{
    public Guid WorkstationTypeId { get; set; }
    public string OperatingComponent { get; set; } = null!;
    public decimal OperatingCost { get; set; }

    protected WorkstationTypeCost() { }
    public WorkstationTypeCost(Guid id, Guid workstationTypeId, string component, decimal cost) : base(id)
    {
        WorkstationTypeId = workstationTypeId;
        OperatingComponent = component;
        OperatingCost = cost;
    }
}
