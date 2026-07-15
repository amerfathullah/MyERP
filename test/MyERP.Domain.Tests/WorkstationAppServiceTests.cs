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
        var dto = new WorkstationWorkingHourDto { DayOfWeek = "Friday", StartTime = "09:00", EndTime = "18:00" };
        Assert.Equal("Friday", dto.DayOfWeek);
        Assert.Equal("09:00", dto.StartTime);
        Assert.Equal("18:00", dto.EndTime);
    }
}
