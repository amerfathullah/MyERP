using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.DomainServices;

/// <summary>
/// Tests for Bin entity — projected qty formula and concurrency.
/// </summary>
public class BinEntityExtendedTests
{
    [Fact]
    public void Bin_ProjectedQty_Formula()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.OrderedQty = 50;
        bin.IndentedQty = 20;
        bin.PlannedQty = 30;
        bin.ReservedQty = 10;
        bin.ReservedQtyForProduction = 5;
        bin.ReservedQtyForSubContract = 3;
        bin.ReservedQtyForProductionPlan = 2;

        // Formula: Actual + Ordered + Indented + Planned - Reserved - Production - SubContract - ProductionPlan
        bin.ProjectedQty.ShouldBe(100 + 50 + 20 + 30 - 10 - 5 - 3 - 2);
        bin.ProjectedQty.ShouldBe(180m);
    }

    [Fact]
    public void Bin_ProjectedQty_CanBeNegative()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 5;
        bin.ReservedQty = 20;
        bin.ProjectedQty.ShouldBe(-15m);
    }

    [Fact]
    public void Bin_Default_AllZeros()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty.ShouldBe(0);
        bin.OrderedQty.ShouldBe(0);
        bin.PlannedQty.ShouldBe(0);
        bin.ReservedQty.ShouldBe(0);
        bin.IndentedQty.ShouldBe(0);
        bin.ReservedQtyForProduction.ShouldBe(0);
        bin.ReservedQtyForSubContract.ShouldBe(0);
        bin.ReservedQtyForProductionPlan.ShouldBe(0);
        bin.ProjectedQty.ShouldBe(0);
    }

    [Fact]
    public void Bin_ConcurrencyStamp_HasValue()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ConcurrencyStamp.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Bin_ConcurrencyStamp_ChangesOnUpdate()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var original = bin.ConcurrencyStamp;
        bin.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        bin.ConcurrencyStamp.ShouldNotBe(original);
    }

    [Fact]
    public void Bin_OrderedQty_IncreasesProjectedQty()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 10;
        bin.ProjectedQty.ShouldBe(10m);
        bin.OrderedQty = 25;
        bin.ProjectedQty.ShouldBe(35m);
    }

    [Fact]
    public void Bin_ReservedQty_DecreasesProjectedQty()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bin.ActualQty = 100;
        bin.ProjectedQty.ShouldBe(100m);
        bin.ReservedQty = 30;
        bin.ProjectedQty.ShouldBe(70m);
    }

    [Fact]
    public void Bin_HasItemAndWarehouse()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bin = new Bin(Guid.NewGuid(), itemId, warehouseId);
        bin.ItemId.ShouldBe(itemId);
        bin.WarehouseId.ShouldBe(warehouseId);
    }
}

/// <summary>
/// Tests for TaxWithholdingService — static distribution method.
/// </summary>
public class TaxWithholdingDistributionTests
{
    [Fact]
    public void DistributeTds_SingleItem_GetsFullAmount()
    {
        var items = new List<(Guid ItemId, decimal NetAmount)>
        {
            (Guid.NewGuid(), 10000m)
        };

        var result = TaxWithholdingService.DistributeTdsAcrossItems(500m, items);
        result.Count.ShouldBe(1);
        result[items[0].ItemId].ShouldBe(500m);
    }

    [Fact]
    public void DistributeTds_TwoEqualItems_SplitEvenly()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var items = new List<(Guid ItemId, decimal NetAmount)>
        {
            (id1, 5000m),
            (id2, 5000m)
        };

        var result = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        result[id1].ShouldBe(50m);
        result[id2].ShouldBe(50m);
    }

    [Fact]
    public void DistributeTds_ProportionalSplit()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var items = new List<(Guid ItemId, decimal NetAmount)>
        {
            (id1, 7500m),  // 75%
            (id2, 2500m)   // 25%
        };

        var result = TaxWithholdingService.DistributeTdsAcrossItems(1000m, items);
        result.Values.Sum().ShouldBe(1000m); // Must total exactly
    }

    [Fact]
    public void DistributeTds_LastItemAbsorbsRounding()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var items = new List<(Guid ItemId, decimal NetAmount)>
        {
            (id1, 3333m),
            (id2, 3333m),
            (id3, 3334m)
        };

        var result = TaxWithholdingService.DistributeTdsAcrossItems(100m, items);
        result.Values.Sum().ShouldBe(100m); // Last item absorbs rounding
    }

    [Fact]
    public void DistributeTds_ZeroNetTotal_EmptyResult()
    {
        var items = new List<(Guid ItemId, decimal NetAmount)>
        {
            (Guid.NewGuid(), 0m),
            (Guid.NewGuid(), 0m)
        };

        var result = TaxWithholdingService.DistributeTdsAcrossItems(500m, items);
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void DistributeTds_EmptyItems_EmptyResult()
    {
        var items = new List<(Guid ItemId, decimal NetAmount)>();
        var result = TaxWithholdingService.DistributeTdsAcrossItems(500m, items);
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void TaxWithholdingResult_Properties()
    {
        var result = new TaxWithholdingResult
        {
            TaxableAmount = 10000m,
            WithheldAmount = 200m,
            EffectiveRate = 2m
        };
        result.TaxableAmount.ShouldBe(10000m);
        result.WithheldAmount.ShouldBe(200m);
        result.EffectiveRate.ShouldBe(2m);
    }
}

/// <summary>
/// Tests for DropShipService — static helper methods.
/// </summary>
public class DropShipServiceStaticTests
{
    [Fact]
    public void HasDropShipItems_EmptyOrder_False()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        DropShipService.HasDropShipItems(so).ShouldBeFalse();
    }

    [Fact]
    public void HasDropShipItems_NormalItems_False()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item 1", 10, 100m, 0m);
        DropShipService.HasDropShipItems(so).ShouldBeFalse();
    }

    [Fact]
    public void HasDropShipItems_WithDropShipItem_True()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Normal", 5, 50m, 0m);
        so.AddItem(Guid.NewGuid(), "Drop Ship", 3, 200m, 0m);
        so.Items.Last().DeliveredBySupplier = true;
        DropShipService.HasDropShipItems(so).ShouldBeTrue();
    }

    [Fact]
    public void GetDropShipItemIds_ReturnsOnlyDropShipItems()
    {
        var normalItemId = Guid.NewGuid();
        var dropItemId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(normalItemId, "Normal", 5, 50m, 0m);
        so.AddItem(dropItemId, "Drop Ship", 3, 200m, 0m);
        so.Items.Last().DeliveredBySupplier = true;

        var ids = DropShipService.GetDropShipItemIds(so);
        ids.ShouldContain(dropItemId);
        ids.ShouldNotContain(normalItemId);
    }

    [Fact]
    public void GetDropShipItemIds_NoDropShip_EmptySet()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Normal", 5, 50m, 0m);
        var ids = DropShipService.GetDropShipItemIds(so);
        ids.ShouldBeEmpty();
    }
}

/// <summary>
/// Tests for BomValidation concepts — ExplodedBomItem record.
/// </summary>
public class BomValidationConceptTests
{
    [Fact]
    public void ExplodedBomItem_RecordCreation()
    {
        var item = new ExplodedBomItem(
            Guid.NewGuid(), "RAW-001", 10m, 50m, "Kg", null);
        item.ItemName.ShouldBe("RAW-001");
        item.Quantity.ShouldBe(10m);
        item.Rate.ShouldBe(50m);
        item.SubBomId.ShouldBeNull();
    }

    [Fact]
    public void ExplodedBomItem_WithSubBom()
    {
        var subBomId = Guid.NewGuid();
        var item = new ExplodedBomItem(
            Guid.NewGuid(), "SUB-ASSY", 2m, 100m, "Unit", subBomId);
        item.SubBomId.ShouldBe(subBomId);
    }

    [Fact]
    public void ExplodedBomItem_Amount_Calculated()
    {
        var item = new ExplodedBomItem(
            Guid.NewGuid(), "ITEM", 5m, 20m, "Kg", null);
        (item.Quantity * item.Rate).ShouldBe(100m);
    }

    [Fact]
    public void ExplodedBomItem_HasUom()
    {
        var item = new ExplodedBomItem(
            Guid.NewGuid(), "ITEM", 1m, 10m, "Litre", null);
        item.Uom.ShouldBe("Litre");
    }
}

/// <summary>
/// Tests for SalesOrder with drop-ship items.
/// </summary>
public class SalesOrderDropShipTests
{
    [Fact]
    public void SalesOrderItem_DeliveredBySupplier_Default()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item", 10, 100m, 0m);
        so.Items.First().DeliveredBySupplier.ShouldBeFalse();
    }

    [Fact]
    public void SalesOrderItem_SupplierId_Default()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Item", 10, 100m, 0m);
        so.Items.First().SupplierId.ShouldBeNull();
    }

    [Fact]
    public void SalesOrderItem_DropShip_WithSupplier()
    {
        var supplierId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Drop Ship Item", 3, 500m, 0m);
        var item = so.Items.First();
        item.DeliveredBySupplier = true;
        item.SupplierId = supplierId;
        item.DeliveredBySupplier.ShouldBeTrue();
        item.SupplierId.ShouldBe(supplierId);
    }

    [Fact]
    public void SalesOrderItem_DropShip_NoWarehouse()
    {
        var so = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.Today);
        so.AddItem(Guid.NewGuid(), "Drop Ship", 1, 100m, 0m);
        so.Items.First().DeliveredBySupplier = true;
        so.Items.First().WarehouseId.ShouldBeNull(); // Drop-ship = no warehouse
    }
}
