using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Inventory;

public class ItemInspectionTests
{
    private static Item CreateItem()
        => new(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);

    [Fact]
    public void InspectionRequiredBeforePurchase_DefaultsFalse()
    {
        var item = CreateItem();
        item.InspectionRequiredBeforePurchase.ShouldBeFalse();
    }

    [Fact]
    public void InspectionRequiredBeforeDelivery_DefaultsFalse()
    {
        var item = CreateItem();
        item.InspectionRequiredBeforeDelivery.ShouldBeFalse();
    }

    [Fact]
    public void InspectionRequiredBeforePurchase_CanBeEnabled()
    {
        var item = CreateItem();
        item.InspectionRequiredBeforePurchase = true;
        item.InspectionRequiredBeforePurchase.ShouldBeTrue();
    }

    [Fact]
    public void InspectionRequiredBeforeDelivery_CanBeEnabled()
    {
        var item = CreateItem();
        item.InspectionRequiredBeforeDelivery = true;
        item.InspectionRequiredBeforeDelivery.ShouldBeTrue();
    }

    [Fact]
    public void ServiceItem_InspectionNotApplicable()
    {
        // Service items don't maintain stock, QI shouldn't be set
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "SVC-001", "Service", ItemType.Service);
        item.MaintainStock.ShouldBeFalse();
        item.InspectionRequiredBeforePurchase.ShouldBeFalse();
        item.InspectionRequiredBeforeDelivery.ShouldBeFalse();
    }
}
