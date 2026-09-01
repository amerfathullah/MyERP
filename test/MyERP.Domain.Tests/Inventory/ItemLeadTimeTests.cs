using System;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class ItemLeadTimeTests
{
    [Fact]
    public void ItemLeadTime_Creation_CalculatesCapacityCorrectly()
    {
        var id = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // 8 hours * 2 workstations * 1 shift = 16 hours total workstation time = 960 mins
        // 960 mins / 30 mins per unit = 32 units produced
        // 90% yield * 32 units = 28.8 => 29 capacity per day
        var leadTime = new ItemLeadTime(
            id,
            itemId,
            shiftTimeInHours: 8,
            noOfWorkstations: 2,
            noOfShifts: 1,
            manufacturingTimeInMins: 30,
            dailyYield: 90.0m,
            purchaseTimeDays: 7,
            bufferTimeDays: 2);

        Assert.Equal(id, leadTime.Id);
        Assert.Equal(itemId, leadTime.ItemId);
        Assert.Equal(16, leadTime.TotalWorkstationTime);
        Assert.Equal(32, leadTime.NoOfUnitsProduced);
        Assert.Equal(29, leadTime.CapacityPerDay);
        Assert.Equal(7, leadTime.PurchaseTimeDays);
        Assert.Equal(2, leadTime.BufferTimeDays);
    }

    [Fact]
    public void ItemLeadTime_AddSupplier_ManagesDefaultCorrectly()
    {
        var leadTime = new ItemLeadTime(Guid.NewGuid(), Guid.NewGuid());
        var sup1 = Guid.NewGuid();
        var sup2 = Guid.NewGuid();

        leadTime.AddSupplier(sup1, 10, 2, isDefault: true);
        leadTime.AddSupplier(sup2, 5, 1, isDefault: true);

        Assert.Equal(2, leadTime.Suppliers.Count);
        Assert.False(leadTime.Suppliers[0].IsDefault);
        Assert.True(leadTime.Suppliers[1].IsDefault);
    }

    [Fact]
    public void ItemLeadTime_AddSupplier_DuplicateThrowsException()
    {
        var leadTime = new ItemLeadTime(Guid.NewGuid(), Guid.NewGuid());
        var sup1 = Guid.NewGuid();

        leadTime.AddSupplier(sup1, 10, 2);
        var ex = Assert.Throws<Volo.Abp.BusinessException>(() => leadTime.AddSupplier(sup1, 5, 1));
        Assert.Equal(MyERPDomainErrorCodes.DuplicateRecord, ex.Code);
    }
}
