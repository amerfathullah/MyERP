using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class PickListLocationFilterTests
{
    [Fact]
    public void FilterLocations_ConsumesPickedQty_AcrossMultipleRows()
    {
        var whId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation { WarehouseId = whId, BatchId = batchId, Qty = 5 },
            new PickLocationAllocation { WarehouseId = whId, BatchId = batchId, Qty = 5 }
        };

        var pickedQtyMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>
        {
            [(whId, batchId)] = 7
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, pickedQtyMap);

        filtered.Count.ShouldBe(1);
        filtered[0].Qty.ShouldBe(3);
        pickedQtyMap[(whId, batchId)].ShouldBe(0);
    }

    [Fact]
    public void FilterLocations_PreservesSerialOrder_WhenExcludingPickedSerials()
    {
        var whId = Guid.NewGuid();

        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation
            {
                WarehouseId = whId,
                Qty = 4,
                SerialNumbers = new List<string> { "SN-1", "SN-2", "SN-3", "SN-4" }
            }
        };

        var pickedQtyMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>
        {
            [(whId, null)] = 2
        };

        var pickedSerialNosMap = new Dictionary<Guid, HashSet<string>>
        {
            [whId] = new HashSet<string> { "SN-2", "SN-4" }
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, pickedQtyMap, pickedSerialNosMap);

        filtered.Count.ShouldBe(1);
        filtered[0].Qty.ShouldBe(2);
        filtered[0].SerialNumbers.ShouldBe(new[] { "SN-1", "SN-3" });
    }
}
