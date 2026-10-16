using System;
using System.Linq;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkstationAppServiceTests
{
    [Fact]
    public void WorkstationDto_DefaultValues()
    {
        var dto = new WorkstationDto();
        Assert.Equal(Guid.Empty, dto.Id);
        Assert.Null(dto.Name);
        Assert.Null(dto.WorkstationType);
        Assert.Equal(0, dto.ProductionCapacity);
        Assert.Equal(0m, dto.HourRate);
        Assert.False(dto.IsActive);
        Assert.Empty(dto.Costs);
        Assert.Empty(dto.WorkingHours);
    }

    [Fact]
    public void WorkstationDto_CostAndHour_Mapping()
    {
        var dto = new WorkstationDto
        {
            Name = "CNC Machine A",
            Costs = [new WorkstationCostDto { Name = "Electricity", Amount = 15.50m }],
            WorkingHours = [new WorkstationWorkingHourDto { DayOfWeek = "Monday", StartTime = "08:00", EndTime = "17:00" }],
        };
        Assert.Equal("Electricity", dto.Costs[0].Name);
        Assert.Equal(15.50m, dto.Costs[0].Amount);
        Assert.Equal("Monday", dto.WorkingHours[0].DayOfWeek);
    }

    [Fact]
    public void CreateWorkstationDto_HasRequiredFields()
    {
        var dto = new CreateWorkstationDto
        {
            CompanyId = Guid.NewGuid(),
            Name = "Press 01",
            WorkstationType = "Assembly",
            ProductionCapacity = 3,
        };
        Assert.NotEqual(Guid.Empty, dto.CompanyId);
        Assert.Equal("Press 01", dto.Name);
        Assert.Equal(3, dto.ProductionCapacity);
    }

    [Fact]
    public void Workstation_Entity_HourRate_Calculated_FromCosts()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Test WS");
        ws.AddCost("Labor", 25m);
        ws.AddCost("Electricity", 10m);
        Assert.Equal(35m, ws.HourRate);
    }

    [Fact]
    public void Workstation_Entity_IsActive_DefaultTrue()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS");
        Assert.True(ws.IsActive);
    }

    [Fact]
    public void Workstation_Entity_WorkingHour_Added()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "WS");
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
        Assert.Single(ws.WorkingHours);
        Assert.Equal("Monday", ws.WorkingHours[0].Day);
    }

    [Fact]
    public void WorkstationCostDto_Properties()
    {
        var dto = new WorkstationCostDto { Name = "Gas", Amount = 5.25m };
        Assert.Equal("Gas", dto.Name);
        Assert.Equal(5.25m, dto.Amount);
    }

    [Fact]
    public void WorkstationWorkingHourDto_Properties()
    {
        var dto = new WorkstationWorkingHourDto { DayOfWeek = "Friday", StartTime = "09:00", EndTime = "18:00", IsEnabled = true };
        Assert.Equal("Friday", dto.DayOfWeek);
        Assert.Equal("09:00", dto.StartTime);
        Assert.Equal("18:00", dto.EndTime);
        Assert.True(dto.IsEnabled);
    }

    [Fact]
    public void Workstation_TotalWorkingHours_SumsOnlyEnabledHours()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "CNC-A");
        // Shift 1: 08:00 - 12:00 (4 hours, enabled)
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), isEnabled: true);
        // Shift 2: 13:00 - 17:00 (4 hours, disabled)
        ws.AddWorkingHour("Monday", new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0), isEnabled: false);
        // Shift 3: 08:00 - 16:00 (8 hours, enabled)
        ws.AddWorkingHour("Tuesday", new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), isEnabled: true);

        Assert.Equal(3, ws.WorkingHours.Count);
        Assert.Equal(12.0m, ws.TotalWorkingHours); // 4 + 8 = 12
    }

    [Fact]
    public void Workstation_AddWorkingHour_IgnoresDisabledWorkingHoursForOverlap()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Press-1");
        // Disabled shift 08:00 - 12:00
        ws.AddWorkingHour("Monday", new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), isEnabled: false);

        // Enabled overlapping shift 09:00 - 11:00 does not throw because previous is disabled
        ws.AddWorkingHour("Monday", new TimeSpan(9, 0, 0), new TimeSpan(11, 0, 0), isEnabled: true);
        Assert.Equal(2, ws.WorkingHours.Count);

        // Adding another enabled overlapping shift throws
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            ws.AddWorkingHour("Monday", new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0), isEnabled: true));
    }

    [Fact]
    public void JobCard_StartTimeLog_And_CompleteTimeLogs_Lifecycle()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, 1);
        Assert.Equal(JobCardStatus.Open, jc.Status);

        var startTime = new DateTime(2026, 10, 16, 9, 0, 0, DateTimeKind.Utc);
        jc.StartTimeLog(startTime);

        Assert.Equal(JobCardStatus.WorkInProgress, jc.Status);
        Assert.Equal(startTime, jc.StartedAt);
        Assert.Single(jc.TimeLogs);
        Assert.Equal(default, jc.TimeLogs[0].ToTime);

        var endTime = startTime.AddMinutes(90);
        jc.CompleteTimeLogs(endTime, completedQty: 25m);

        Assert.Equal(endTime, jc.TimeLogs[0].ToTime);
        Assert.Equal(90m, jc.TimeLogs[0].TimeInMins);
        Assert.Equal(25m, jc.TimeLogs[0].CompletedQty);
        Assert.Equal(90m, jc.TotalTimeInMins);
        Assert.Equal(25m, jc.CompletedQty);
    }
}
