using System;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class QualityInspectionParameterGroupTests
{
    [Fact]
    public void QualityInspectionParameterGroup_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var group = new QualityInspectionParameterGroup(id, "Dimensional Inspection", "Measurements, tolerances and physical geometry");

        Assert.Equal(id, group.Id);
        Assert.Equal("Dimensional Inspection", group.GroupName);
        Assert.Equal("Measurements, tolerances and physical geometry", group.Description);
        Assert.True(group.IsActive);
    }
}
