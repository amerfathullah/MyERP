using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkstationTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "CNC Machine 1");
        ws.Name.ShouldBe("CNC Machine 1");
        ws.ProductionCapacity.ShouldBe(1);
        ws.HourRate.ShouldBe(0);
        ws.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void AddCost_CalculatesHourRate()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Lathe");
        ws.AddCost("Labor", 50m);
        ws.AddCost("Electricity", 10m);
        ws.HourRate.ShouldBe(60m);
    }

    [Fact]
    public void AddCost_DuplicateComponent_Throws()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Drill");
        ws.AddCost("Labor", 50m);
        Should.Throw<BusinessException>(() => ws.AddCost("Labor", 30m));
    }

    [Fact]
    public void AddWorkingHour_Valid()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Press");
        ws.AddWorkingHour("Monday", TimeSpan.FromHours(8), TimeSpan.FromHours(17));
        ws.WorkingHours.Count.ShouldBe(1);
    }

    [Fact]
    public void AddWorkingHour_InvalidRange_Throws()
    {
        var ws = new Workstation(Guid.NewGuid(), Guid.NewGuid(), "Grinder");
        Should.Throw<ArgumentException>(() =>
            ws.AddWorkingHour("Monday", TimeSpan.FromHours(17), TimeSpan.FromHours(8)));
    }
}

public class RoutingTests
{
    [Fact]
    public void AddOperation_InSequence()
    {
        var routing = new Routing(Guid.NewGuid(), "Standard Routing");
        routing.AddOperation(Guid.NewGuid(), 10, 30m);
        routing.AddOperation(Guid.NewGuid(), 20, 45m);
        routing.Operations.Count.ShouldBe(2);
    }

    [Fact]
    public void AddOperation_DecreasingSequence_Throws()
    {
        var routing = new Routing(Guid.NewGuid(), "Test Routing");
        routing.AddOperation(Guid.NewGuid(), 20, 30m);
        Should.Throw<BusinessException>(() =>
            routing.AddOperation(Guid.NewGuid(), 10, 45m)); // 10 < 20 = violation
    }

    [Fact]
    public void RoutingOperation_CalculateCost()
    {
        var routing = new Routing(Guid.NewGuid(), "Cost Routing");
        routing.AddOperation(Guid.NewGuid(), 10, 60m); // 60 mins
        routing.Operations[0].CalculateCost(120m); // RM120/hr
        routing.Operations[0].OperatingCost.ShouldBe(120m); // 120 × 60/60
    }
}
