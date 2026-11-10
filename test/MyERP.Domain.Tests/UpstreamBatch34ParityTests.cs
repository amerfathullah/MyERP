using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP;

public class UpstreamBatch34ParityTests
{
    // === 1) PR #65124: Production Plan Download Raw Materials & Alignment ===

    [Fact]
    public void BuildDownloadRows_PreservesPlannedQty()
    {
        var itemId = Guid.NewGuid();
        var rows = new List<RawMaterialPlanRow>
        {
            new()
            {
                ItemId = itemId,
                ItemCode = "RM-001",
                ItemName = "Raw Material 1",
                StockUom = "Nos",
                Warehouse = "Stores - TC",
                RequiredBomQty = 50m,
                ProjectedQty = 10m,
                ActualQty = 20m,
                OrderedQty = 5m,
                PlannedQty = 35m,
                ReservedQtyForProduction = 2m,
                SafetyStock = 0m,
                Quantity = 35m,
                Uom = "Nos"
            }
        };

        var result = ProductionPlanMaterialPlanningService.BuildDownloadRows(rows);

        result.Count.ShouldBe(1);
        result[0].ItemCode.ShouldBe("RM-001");
        result[0].Warehouse.ShouldBe("Stores - TC");
        result[0].ActualQty.ShouldBe(20m);
        result[0].ProjectedQty.ShouldBe(10m);
        result[0].OrderedQty.ShouldBe(5m);
        result[0].PlannedQty.ShouldBe(35m);
        result[0].ReservedQtyForProduction.ShouldBe(2m);
        result[0].RequiredQty.ShouldBe(35m);
    }

    [Fact]
    public void BuildDownloadRows_PreservesOtherWarehouseQuantities()
    {
        var itemId = Guid.NewGuid();
        var rows = new List<RawMaterialPlanRow>
        {
            // Main required row
            new()
            {
                ItemId = itemId,
                ItemCode = "RM-001",
                ItemName = "Raw Material 1",
                StockUom = "Nos",
                Warehouse = "Work In Progress - TC",
                RequiredBomQty = 50m,
                ActualQty = 0m,
                PlannedQty = 50m,
                Quantity = 50m,
                Uom = "Nos"
            },
            // Other-warehouse row (per PR #65124 / commit 38d560b782)
            new()
            {
                ItemId = itemId,
                ItemCode = string.Empty,
                ItemName = "Raw Material 1",
                StockUom = "Nos",
                Warehouse = "Finished Goods - TC",
                ActualQty = 7m,
                PlannedQty = 4m,
                OrderedQty = 2m,
                ProjectedQty = 9m,
                ReservedQtyForProduction = 1m,
                RequiredBomQty = 0m,
                Quantity = 0m,
                Uom = "Nos"
            }
        };

        var result = ProductionPlanMaterialPlanningService.BuildDownloadRows(rows);

        result.Count.ShouldBe(2);

        var mainRow = result[0];
        mainRow.ItemCode.ShouldBe("RM-001");
        mainRow.Warehouse.ShouldBe("Work In Progress - TC");
        mainRow.PlannedQty.ShouldBe(50m);
        mainRow.RequiredQty.ShouldBe(50m);

        var otherRow = result[1];
        otherRow.Warehouse.ShouldBe("Finished Goods - TC");
        otherRow.ActualQty.ShouldBe(7m);
        otherRow.PlannedQty.ShouldBe(4m);
        otherRow.OrderedQty.ShouldBe(2m);
        otherRow.ProjectedQty.ShouldBe(9m);
        otherRow.ReservedQtyForProduction.ShouldBe(1m);
        otherRow.RequiredQty.ShouldBe(0m);
    }

    [Fact]
    public void BuildDownloadRows_MultipleItemsInSameOtherWarehouse_DoNotCollide()
    {
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var rows = new List<RawMaterialPlanRow>
        {
            new()
            {
                ItemId = item1Id,
                ItemCode = string.Empty,
                ItemName = "Item 1",
                Warehouse = "Shared Stores - TC",
                ActualQty = 15m,
                PlannedQty = 3m,
                Uom = "Nos"
            },
            new()
            {
                ItemId = item2Id,
                ItemCode = string.Empty,
                ItemName = "Item 2",
                Warehouse = "Shared Stores - TC",
                ActualQty = 25m,
                PlannedQty = 6m,
                Uom = "Nos"
            }
        };

        var result = ProductionPlanMaterialPlanningService.BuildDownloadRows(rows);

        result.Count.ShouldBe(2);
        result[0].ItemName.ShouldBe("Item 1");
        result[0].ActualQty.ShouldBe(15m);
        result[0].PlannedQty.ShouldBe(3m);

        result[1].ItemName.ShouldBe("Item 2");
        result[1].ActualQty.ShouldBe(25m);
        result[1].PlannedQty.ShouldBe(6m);
    }
}
