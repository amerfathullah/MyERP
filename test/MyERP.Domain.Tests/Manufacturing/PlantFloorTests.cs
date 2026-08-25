using System;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class PlantFloorTests
{
    [Fact]
    public void PlantFloor_Creation_SetsPropertiesCorrectly()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var floor = new PlantFloor(id, companyId, "Floor 1 - Assembly", whId, "Main Assembly Zone");

        Assert.Equal(id, floor.Id);
        Assert.Equal(companyId, floor.CompanyId);
        Assert.Equal("Floor 1 - Assembly", floor.FloorName);
        Assert.Equal(whId, floor.WarehouseId);
        Assert.Equal("Main Assembly Zone", floor.Description);
        Assert.True(floor.IsActive);
    }
}
