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

    /// <summary>Workstation type for grouping/auto-assignment.</summary>
    public string? WorkstationType { get; set; }

    /// <summary>Concurrent jobs allowed (default 1).</summary>
    public int ProductionCapacity { get; set; } = 1;

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

    protected WorkstationWorkingHour() { }
    public WorkstationWorkingHour(Guid id, Guid workstationId, string day, TimeSpan startTime, TimeSpan endTime) : base(id)
    {
        WorkstationId = workstationId;
        Day = day;
        StartTime = startTime;
        EndTime = endTime;
    }
}
