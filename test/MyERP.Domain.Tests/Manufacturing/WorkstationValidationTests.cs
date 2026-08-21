using System;
using MyERP.Manufacturing.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for Workstation domain validations:
/// - Working hours validation (StartTime < EndTime, auto-calculated shift hours) (Gotcha #1834)
/// - Duplicate cost component prevention (Gotcha #1830)
/// - HourRate auto-calculation from cost components
/// </summary>
public class WorkstationValidationTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Workstation_AddWorkingHour_ValidShift_CalculatesShiftHours()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "CNC Machine 1");
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(16, 30, 0));

        Assert.Single(ws.WorkingHours);
        Assert.Equal(8.5m, ws.WorkingHours[0].Hours);
    }

    [Fact]
    public void Workstation_AddWorkingHour_StartTimeAfterEndTime_ThrowsArgumentException()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "CNC Machine 2");
        
        Assert.Throws<ArgumentException>(() =>
            ws.AddWorkingHour("Monday", new TimeSpan(17, 0, 0), new TimeSpan(8, 0, 0)));
    }

    [Fact]
    public void Workstation_AddCost_DuplicateComponent_ThrowsBusinessException()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "Assembly Bench 1");
        ws.AddCost("Electricity", 15m);

        var ex = Assert.Throws<BusinessException>(() => ws.AddCost("Electricity", 20m));
        Assert.Contains("Duplicate cost component", ex.Data["detail"]?.ToString());
    }

    [Fact]
    public void Workstation_HourRate_SumsAllOperatingCosts()
    {
        var ws = new Workstation(Guid.NewGuid(), _companyId, "Assembly Bench 2");
        ws.AddCost("Electricity", 15m);
        ws.AddCost("Labour", 45m);
        ws.AddCost("Rent", 20m);

        Assert.Equal(80m, ws.HourRate);
    }
}
