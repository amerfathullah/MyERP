using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for ERPNext PR #58613 / commit 7ecfa6b356:
/// - Stock Reservation Entry delivery restoration on document cancellation
/// - Pick list location filtering across rows and serial number sequence preservation
/// </summary>
public class StockReservationRestoreOnCancelTests
{
    [Fact]
    public void StockReservationEntry_RevertDelivery_RestoresDeliveredAndAvailableQty()
    {
        var sre = new StockReservationEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SalesOrder", Guid.NewGuid(), reservedQty: 20m);
        sre.Submit();

        sre.RecordDelivery(15m);
        sre.DeliveredQty.ShouldBe(15m);
        sre.AvailableQty.ShouldBe(5m);

        // Revert 10 of the delivered qty
        sre.RevertDelivery(10m);
        sre.DeliveredQty.ShouldBe(5m);
        sre.AvailableQty.ShouldBe(15m);

        // Revert remaining (and more than available) caps at 0
        sre.RevertDelivery(10m);
        sre.DeliveredQty.ShouldBe(0m);
        sre.AvailableQty.ShouldBe(20m);
    }

    [Fact]
    public void StockReservationEntry_RevertDelivery_NonPositiveQty_Throws()
    {
        var sre = new StockReservationEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SalesOrder", Guid.NewGuid(), reservedQty: 20m);
        sre.Submit();
        sre.RecordDelivery(10m);

        Should.Throw<ArgumentException>(() => sre.RevertDelivery(0));
        Should.Throw<ArgumentException>(() => sre.RevertDelivery(-5));
    }

    [Fact]
    public async Task StockReservationManager_RestoreOnCancelDeliveryAsync_RestoresInLifoOrder()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var salesOrderId = Guid.NewGuid();

        // First SRE created earlier
        var sre1 = new StockReservationEntry(
            Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId,
            "SalesOrder", salesOrderId, reservedQty: 10m);
        sre1.Submit();
        sre1.RecordDelivery(8m); // 8 delivered

        // Second SRE created later
        var sre2 = new StockReservationEntry(
            Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId,
            "SalesOrder", salesOrderId, reservedQty: 15m);
        sre2.Submit();
        sre2.RecordDelivery(12m); // 12 delivered

        // Set creation times to verify LIFO ordering
        sre1.GetType().GetProperty("CreationTime")!.SetValue(sre1, DateTime.UtcNow.AddMinutes(-10));
        sre2.GetType().GetProperty("CreationTime")!.SetValue(sre2, DateTime.UtcNow.AddMinutes(-5));

        var sreList = new List<StockReservationEntry> { sre1, sre2 };
        var sreRepo = Substitute.For<IRepository<StockReservationEntry, Guid>>();
        sreRepo.GetQueryableAsync().Returns(Task.FromResult(sreList.AsQueryable()));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var manager = new StockReservationManager(sreRepo, binRepo, sleRepo);

        // Cancel delivery note that delivered 15 units
        // LIFO: should restore 12 from sre2, and 3 from sre1
        var restored = await manager.RestoreOnCancelDeliveryAsync(itemId, warehouseId, deliveredQty: 15m, salesOrderId);

        restored.Length.ShouldBe(2);
        restored[0].StockReservationEntryId.ShouldBe(sre2.Id);
        restored[0].ConsumedQty.ShouldBe(12m);
        sre2.DeliveredQty.ShouldBe(0m);
        sre2.AvailableQty.ShouldBe(15m);

        restored[1].StockReservationEntryId.ShouldBe(sre1.Id);
        restored[1].ConsumedQty.ShouldBe(3m);
        sre1.DeliveredQty.ShouldBe(5m); // 8 - 3 = 5
        sre1.AvailableQty.ShouldBe(5m);  // 10 - 5 = 5
    }

    [Fact]
    public void FilterLocationsByPickedMaterials_ConsumesPickedQtyAcrossRows_WithoutEarlyZeroingBug()
    {
        // Per ERPNext PR #58613: test_filter_locations_consumes_picked_qty_across_rows
        var warehouseId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var key = (warehouseId, (Guid?)batchId);

        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation { WarehouseId = warehouseId, BatchId = batchId, Qty = 5m },
            new PickLocationAllocation { WarehouseId = warehouseId, BatchId = batchId, Qty = 5m },
        };

        var pickedQtyMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>
        {
            [key] = 7m
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, pickedQtyMap);

        // First row of 5 is fully consumed (picked_qty becomes 7 - 5 = 2, row.qty becomes 0, filtered out)
        // Second row of 5 has 2 consumed -> 3 remaining
        filtered.Count.ShouldBe(1);
        filtered[0].Qty.ShouldBe(3m);
        pickedQtyMap[key].ShouldBe(0m);
    }

    [Fact]
    public void FilterLocationsByPickedMaterials_PreservesSerialOrder()
    {
        // Per ERPNext PR #58613: test_filter_locations_preserves_serial_order
        var warehouseId = Guid.NewGuid();

        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation
            {
                WarehouseId = warehouseId,
                BatchId = null,
                Qty = 4m,
                SerialNumbers = new List<string> { "SN-1", "SN-2", "SN-3", "SN-4" }
            }
        };

        var pickedQtyMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>
        {
            [(warehouseId, null)] = 2m
        };

        var pickedSerialsMap = new Dictionary<Guid, HashSet<string>>
        {
            [warehouseId] = new HashSet<string> { "SN-2", "SN-4" }
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(
            locations, pickedQtyMap, pickedSerialsMap);

        filtered.Count.ShouldBe(1);
        filtered[0].Qty.ShouldBe(2m);
        filtered[0].SerialNumbers.ShouldBe(new List<string> { "SN-1", "SN-3" });
    }
}
