using System;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

public class BomCycleAndPhantomTests
{
    [Fact]
    public void BomItem_SubBomId_DefaultsNull()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 10, 5);
        item.SubBomId.ShouldBeNull();
    }

    [Fact]
    public void BomItem_IsPhantom_DefaultsFalse()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 10, 5);
        item.IsPhantom.ShouldBeFalse();
    }

    [Fact]
    public void BomItem_CanSetSubBomId()
    {
        var subBomId = Guid.NewGuid();
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SubAssembly", 2, 100);
        item.SubBomId = subBomId;
        item.SubBomId.ShouldBe(subBomId);
    }

    [Fact]
    public void BomItem_CanSetPhantom()
    {
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Phantom", 1, 50);
        item.IsPhantom = true;
        item.IsPhantom.ShouldBeTrue();
    }

    [Fact]
    public void BomItem_PhantomWithSubBom_IsValidConfiguration()
    {
        var subBomId = Guid.NewGuid();
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Phantom Sub", 1, 0);
        item.IsPhantom = true;
        item.SubBomId = subBomId;

        item.IsPhantom.ShouldBeTrue();
        item.SubBomId.ShouldBe(subBomId);
    }

    [Fact]
    public void BomItem_SubAssemblyWithSubBom_NotPhantom()
    {
        var subBomId = Guid.NewGuid();
        var item = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Sub-Assembly", 3, 200);
        item.SubBomId = subBomId;
        item.IsPhantom = false;

        item.SubBomId.ShouldBe(subBomId);
        item.IsPhantom.ShouldBeFalse();
    }

    [Fact]
    public void ExplodedBomItem_RecordHoldsCorrectData()
    {
        var itemId = Guid.NewGuid();
        var subBomId = Guid.NewGuid();
        var record = new DomainServices.ExplodedBomItem(itemId, "Wire", 10m, 5m, "Kg", subBomId);

        record.ItemId.ShouldBe(itemId);
        record.ItemName.ShouldBe("Wire");
        record.Quantity.ShouldBe(10m);
        record.Rate.ShouldBe(5m);
        record.Uom.ShouldBe("Kg");
        record.SubBomId.ShouldBe(subBomId);
    }

    [Fact]
    public void ExplodedBomItem_RawMaterial_HasNoSubBom()
    {
        var record = new DomainServices.ExplodedBomItem(Guid.NewGuid(), "Steel Bar", 20m, 25m, "Unit", null);
        record.SubBomId.ShouldBeNull();
    }

    [Fact]
    public void BomCycleDetected_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.BomCycleDetected.ShouldBe("MyERP:10007");
    }
}
