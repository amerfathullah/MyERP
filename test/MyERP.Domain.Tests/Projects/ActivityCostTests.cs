using System;
using MyERP.Projects.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Projects;

public class ActivityCostTests
{
    [Fact]
    public void ActivityCost_Creation_SetsPropertiesCorrectly()
    {
        var empId = Guid.NewGuid();
        var actId = Guid.NewGuid();

        var cost = new ActivityCost(Guid.NewGuid(), empId, actId, 150m, 80m);

        Assert.Equal(empId, cost.EmployeeId);
        Assert.Equal(actId, cost.ActivityTypeId);
        Assert.Equal(150m, cost.BillingRate);
        Assert.Equal(80m, cost.CostingRate);
    }
}
