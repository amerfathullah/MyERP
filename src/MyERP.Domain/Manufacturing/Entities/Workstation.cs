using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Manufacturing.Entities;

/// <summary>
/// Workstation — physical production station with capacity, costs, and working hours.
/// Maps to ERPNext manufacturing/doctype/workstation.
/// </summary>
public class Workstation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Denormalized display name of the linked WorkstationType, if any.</summary>
    public string? WorkstationType { get; set; }

    /// <summary>Optional link to a WorkstationType cost template. Assigning/changing this
    /// copies the type's operating cost breakdown onto this Workstation (AppService-side,
    /// mirroring ERPNext workstation.py _set_data_based_on_workstation_type).</summary>
    public Guid? WorkstationTypeId { get; set; }

    private int _productionCapacity = 1;

    /// <summary>Concurrent jobs allowed (default 1). Non-negative (PR #48557 / commit 92a12d7fea).</summary>
    public int ProductionCapacity
    {
        get => _productionCapacity;
        set
        {
            if (value < 0)
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", nameof(ProductionCapacity));
            _productionCapacity = value;
        }
    }

    /// <summary>Auto-calculated: SUM of all cost component operating_cost.</summary>
    public decimal HourRate { get; private set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Holiday list for blocking production scheduling.</summary>
    public Guid? HolidayListId { get; set; }

    private readonly List<WorkstationCost> _costs = new();
    public IReadOnlyList<WorkstationCost> Costs => _costs.AsReadOnly();

    private readonly List<WorkstationWorkingHour> _workingHours = new();
    public IReadOnlyList<WorkstationWorkingHour> WorkingHours => _workingHours.AsReadOnly();

    protected Workstation() { }

    public Workstation(Guid id, Guid companyId, string name, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), 200);
        TenantId = tenantId;
    }

    public void AddCost(string component, decimal operatingCost)
    {
        if (_costs.Any(c => c.OperatingComponent == component))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", $"Duplicate cost component: {component}");
        _costs.Add(new WorkstationCost(Guid.NewGuid(), Id, component, operatingCost));
        RecalculateHourRate();
    }

    /// <summary>Replaces the full cost-component breakdown and recalculates HourRate.</summary>
    public void ReplaceCosts(IEnumerable<(string Component, decimal OperatingCost)> rows)
    {
        _costs.Clear();
        foreach (var row in rows)
            _costs.Add(new WorkstationCost(Guid.NewGuid(), Id, row.Component, row.OperatingCost));
        RecalculateHourRate();
    }

    public void AddWorkingHour(string day, TimeSpan startTime, TimeSpan endTime)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Start time must be before end time.");
        _workingHours.Add(new WorkstationWorkingHour(Guid.NewGuid(), Id, day, startTime, endTime));
    }

    private void RecalculateHourRate()
    {
        HourRate = _costs.Sum(c => c.OperatingCost);
    }
}

public class WorkstationCost : FullAuditedEntity<Guid>
{
    public Guid WorkstationId { get; set; }
    public string OperatingComponent { get; set; } = null!;
    public decimal OperatingCost { get; set; }

    protected WorkstationCost() { }
    public WorkstationCost(Guid id, Guid workstationId, string component, decimal cost) : base(id)
    {
        if (cost < 0)
            throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", nameof(cost));

        WorkstationId = workstationId;
        OperatingComponent = component;
        OperatingCost = cost;
    }
}

public class WorkstationWorkingHour : FullAuditedEntity<Guid>
{
    public Guid WorkstationId { get; set; }
    public string Day { get; set; } = null!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Auto-calculated shift duration in hours (gotcha #1834).</summary>
    public decimal Hours => (decimal)(EndTime - StartTime).TotalHours;

    protected WorkstationWorkingHour() { }
    public WorkstationWorkingHour(Guid id, Guid workstationId, string day, TimeSpan startTime, TimeSpan endTime) : base(id)
    {
        WorkstationId = workstationId;
        Day = day;
        StartTime = startTime;
        EndTime = endTime;
    }
}
