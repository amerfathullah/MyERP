using System;
using MyERP.Projects.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Projects;

public class ActivityTypeAndCostTests
{
    [Fact]
    public void ActivityType_DefaultState()
    {
        var at = new ActivityType(Guid.NewGuid(), "Development");
        Assert.Equal("Development", at.Name);
        Assert.Equal(0m, at.DefaultBillingRate);
        Assert.Equal(0m, at.DefaultCostingRate);
        Assert.True(at.IsEnabled);
    }

    [Fact]
    public void ActivityType_WithRates()
    {
        var at = new ActivityType(Guid.NewGuid(), "Consulting", 250m, 150m);
        Assert.Equal(250m, at.DefaultBillingRate);
        Assert.Equal(150m, at.DefaultCostingRate);
    }

    [Fact]
    public void ActivityType_NameRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new ActivityType(Guid.NewGuid(), ""));
    }

    [Fact]
    public void ActivityCost_EmployeeSpecific()
    {
        var empId = Guid.NewGuid();
        var atId = Guid.NewGuid();
        var cost = new ActivityCost(Guid.NewGuid(), empId, atId, 300m, 180m);

        Assert.Equal(empId, cost.EmployeeId);
        Assert.Equal(atId, cost.ActivityTypeId);
        Assert.Equal(300m, cost.BillingRate);
        Assert.Equal(180m, cost.CostingRate);
    }

    [Fact]
    public void ActivityCost_ZeroRates_Valid()
    {
        var cost = new ActivityCost(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, 0m);
        Assert.Equal(0m, cost.BillingRate);
        Assert.Equal(0m, cost.CostingRate);
    }

    [Fact]
    public void ActivityType_Disable()
    {
        var at = new ActivityType(Guid.NewGuid(), "Design");
        at.IsEnabled = false;
        Assert.False(at.IsEnabled);
    }
}
