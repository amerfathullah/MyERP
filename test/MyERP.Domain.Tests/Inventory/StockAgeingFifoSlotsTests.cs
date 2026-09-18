using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

public class StockAgeingFifoSlotsTests
{
    [Fact]
    public void FIFO_Consumes_Oldest_Slots_First()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 1, 1),
                CreationTime = new DateTime(2026, 1, 1, 10, 0, 0),
                ActualQty = 100m,
                StockValueDifference = 1000m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 9, 1),
                CreationTime = new DateTime(2026, 9, 1, 10, 0, 0),
                ActualQty = 50m,
                StockValueDifference = 600m,
                ValuationRate = 12m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 9, 10),
                CreationTime = new DateTime(2026, 9, 10, 10, 0, 0),
                ActualQty = -60m,
                StockValueDifference = -600m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            }
        };

        var result = FifoSlotsSimulator.Simulate(entries, new DateTime(2026, 9, 15)).Single();

        result.TotalQty.ShouldBe(90m);
        // 40 remaining from 2026-01-01, 50 from 2026-09-01
        // As of 2026-09-15:
        // Jan 1: age = 257 days
        // Sep 1: age = 14 days
        result.OldestDays.ShouldBe(257);
        result.NewestDays.ShouldBe(14);
        var expectedAvgAge = (40m * 257 + 50m * 14) / 90m;
        result.AverageAgeDays.ShouldBe(Math.Round(expectedAvgAge, 2));
    }

    [Fact]
    public void LIFO_Consumes_Newest_Slots_First()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 1, 1),
                CreationTime = new DateTime(2026, 1, 1, 10, 0, 0),
                ActualQty = 100m,
                StockValueDifference = 1000m,
                ValuationRate = 10m,
                ValuationMethod = "LIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 9, 1),
                CreationTime = new DateTime(2026, 9, 1, 10, 0, 0),
                ActualQty = 50m,
                StockValueDifference = 600m,
                ValuationRate = 12m,
                ValuationMethod = "LIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 9, 10),
                CreationTime = new DateTime(2026, 9, 10, 10, 0, 0),
                ActualQty = -60m,
                StockValueDifference = -700m,
                ValuationRate = 12m,
                ValuationMethod = "LIFO"
            }
        };

        var result = FifoSlotsSimulator.Simulate(entries, new DateTime(2026, 9, 15)).Single();

        result.TotalQty.ShouldBe(90m);
        // LIFO consumed all 50 from 2026-09-01, and 10 from 2026-01-01
        // Remaining 90 are all from 2026-01-01 (age 257 days)
        result.OldestDays.ShouldBe(257);
        result.NewestDays.ShouldBe(257);
        result.AverageAgeDays.ShouldBe(257m);
    }

    [Fact]
    public void LIFO_Batch_Consumes_Newest_Batch_Slot_First_PR59062()
    {
        // Per ERPNext PR #59062 (commit 5a63b36c3b):
        // Ledger: +100 of old batch in Jan, +50 of newer in Sep, then -20 against newer.
        // LIFO consumes newest batch slot first.
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var olderBatch = Guid.NewGuid();
        var newerBatch = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = olderBatch,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 1, 1),
                CreationTime = new DateTime(2021, 1, 1, 10, 0, 0),
                ActualQty = 100m,
                StockValueDifference = 1000m,
                ValuationRate = 10m,
                ValuationMethod = "LIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = newerBatch,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 9, 1),
                CreationTime = new DateTime(2021, 9, 1, 10, 0, 0),
                ActualQty = 50m,
                StockValueDifference = 500m,
                ValuationRate = 10m,
                ValuationMethod = "LIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = newerBatch,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 9, 10),
                CreationTime = new DateTime(2021, 9, 10, 10, 0, 0),
                ActualQty = -20m,
                StockValueDifference = -200m,
                ValuationRate = 10m,
                ValuationMethod = "LIFO"
            }
        };

        var result = FifoSlotsSimulator.Simulate(entries, new DateTime(2021, 9, 15)).Single();

        result.TotalQty.ShouldBe(130m);
        // 100 remaining from olderBatch (Jan 1) and 30 remaining from newerBatch (Sep 1)
        result.OldestDays.ShouldBe((int)(new DateTime(2021, 9, 15) - new DateTime(2021, 1, 1)).TotalDays);
        result.NewestDays.ShouldBe((int)(new DateTime(2021, 9, 15) - new DateTime(2021, 9, 1)).TotalDays);
    }

    [Fact]
    public void Batch_Age_In_Warehouse_Scopes_To_Warehouse_PR59058()
    {
        // Per ERPNext PR #59058 (commit 990a43ed16):
        // +10 into WH1 on 2021-12-01, transferred to WH2 on 2021-12-05.
        // WH2 holds the batch since 2021-12-05, so age in WH2 is measured from 2021-12-05.
        var itemId = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = wh1,
                BatchId = batchId,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 12, 1),
                CreationTime = new DateTime(2021, 12, 1, 10, 0, 0),
                ActualQty = 10m,
                StockValueDifference = 100m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = wh1,
                BatchId = batchId,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 12, 5),
                CreationTime = new DateTime(2021, 12, 5, 10, 0, 0),
                ActualQty = -10m,
                StockValueDifference = -100m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = wh2,
                BatchId = batchId,
                HasBatchNo = true,
                PostingDate = new DateTime(2021, 12, 5),
                CreationTime = new DateTime(2021, 12, 5, 10, 0, 1),
                ActualQty = 10m,
                StockValueDifference = 100m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            }
        };

        var results = FifoSlotsSimulator.Simulate(entries, new DateTime(2021, 12, 10), showWarehouseWise: true);

        // WH1 should have 0 qty (not returned)
        results.Any(r => r.WarehouseId == wh1).ShouldBeFalse();

        // WH2 should have 10 qty aging from 2021-12-05 (5 days old on 2021-12-10, NOT 9 days from 2021-12-01)
        var wh2Result = results.Single(r => r.WarehouseId == wh2);
        wh2Result.TotalQty.ShouldBe(10m);
        wh2Result.AverageAgeDays.ShouldBe(5m);
        wh2Result.OldestDays.ShouldBe(5);
        wh2Result.NewestDays.ShouldBe(5);
    }

    [Fact]
    public void Serial_Age_In_Warehouse_Scopes_To_Warehouse_PR59058()
    {
        // Per ERPNext PR #59058 (commit 990a43ed16):
        // Serial SN1 received into WH1 on 2021-12-01, transferred to WH2 on 2021-12-05.
        // WH2 holds the serial since the transfer.
        var itemId = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        var serialNo = "SN-001";

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = wh1,
                SerialNo = serialNo,
                HasSerialNo = true,
                PostingDate = new DateTime(2021, 12, 1),
                CreationTime = new DateTime(2021, 12, 1, 10, 0, 0),
                ActualQty = 1m,
                StockValueDifference = 100m,
                ValuationRate = 100m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = wh1,
                SerialNo = serialNo,
                HasSerialNo = true,
                PostingDate = new DateTime(2021, 12, 5),
                CreationTime = new DateTime(2021, 12, 5, 10, 0, 0),
                ActualQty = -1m,
                StockValueDifference = -100m,
                ValuationRate = 100m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = wh2,
                SerialNo = serialNo,
                HasSerialNo = true,
                PostingDate = new DateTime(2021, 12, 5),
                CreationTime = new DateTime(2021, 12, 5, 10, 0, 1),
                ActualQty = 1m,
                StockValueDifference = 100m,
                ValuationRate = 100m,
                ValuationMethod = "FIFO"
            }
        };

        var results = FifoSlotsSimulator.Simulate(entries, new DateTime(2021, 12, 10), showWarehouseWise: true);

        var wh2Result = results.Single(r => r.WarehouseId == wh2);
        wh2Result.TotalQty.ShouldBe(1m);
        wh2Result.AverageAgeDays.ShouldBe(5m);
    }

    [Fact]
    public void Negative_Stock_Absorbed_By_Subsequent_Inward()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 1, 1),
                CreationTime = new DateTime(2026, 1, 1, 10, 0, 0),
                ActualQty = -20m,
                StockValueDifference = 0m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                PostingDate = new DateTime(2026, 1, 10),
                CreationTime = new DateTime(2026, 1, 10, 10, 0, 0),
                ActualQty = 50m,
                StockValueDifference = 500m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            }
        };

        var result = FifoSlotsSimulator.Simulate(entries, new DateTime(2026, 1, 20)).Single();

        result.TotalQty.ShouldBe(30m);
        // Age is calculated from 2026-01-10 when the stock was actually received
        result.OldestDays.ShouldBe(10);
        result.NewestDays.ShouldBe(10);
        result.AverageAgeDays.ShouldBe(10m);
    }

    [Fact]
    public void Buckets_Calculated_Correctly_With_Custom_Ranges()
    {
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // As of 2026-05-01:
        // Slot 1: 2026-04-20 (11 days old -> 0-30d)
        // Slot 2: 2026-03-20 (42 days old -> 31-60d)
        // Slot 3: 2026-01-10 (111 days old -> 91-120d)
        // Slot 4: 2025-10-01 (212 days old -> >120d)
        var asOf = new DateTime(2026, 5, 1);

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new() { ItemId = itemId, WarehouseId = warehouseId, PostingDate = new DateTime(2025, 10, 1), ActualQty = 10m, StockValueDifference = 100m, ValuationRate = 10m },
            new() { ItemId = itemId, WarehouseId = warehouseId, PostingDate = new DateTime(2026, 1, 10), ActualQty = 20m, StockValueDifference = 200m, ValuationRate = 10m },
            new() { ItemId = itemId, WarehouseId = warehouseId, PostingDate = new DateTime(2026, 3, 20), ActualQty = 30m, StockValueDifference = 300m, ValuationRate = 10m },
            new() { ItemId = itemId, WarehouseId = warehouseId, PostingDate = new DateTime(2026, 4, 20), ActualQty = 40m, StockValueDifference = 400m, ValuationRate = 10m },
        };

        var result = FifoSlotsSimulator.Simulate(entries, asOf, rangeString: "30, 60, 90, 120").Single();

        result.TotalQty.ShouldBe(100m);
        result.Buckets.Count.ShouldBe(5);
        result.Buckets[0].Label.ShouldBe("0-30d");
        result.Buckets[0].Qty.ShouldBe(40m);

        result.Buckets[1].Label.ShouldBe("31-60d");
        result.Buckets[1].Qty.ShouldBe(30m);

        result.Buckets[2].Label.ShouldBe("61-90d");
        result.Buckets[2].Qty.ShouldBe(0m);

        result.Buckets[3].Label.ShouldBe("91-120d");
        result.Buckets[3].Qty.ShouldBe(20m);

        result.Buckets[4].Label.ShouldBe(">120d");
        result.Buckets[4].Qty.ShouldBe(10m);
    }

    [Fact]
    public void Second_Negative_Batch_Slot_Survives_A_Partial_Refill_PR59061()
    {
        // Per ERPNext PR #59061 (commit 0e31182dca):
        // Two issues against no stock, then two receipts.
        // The first receipt clears only the first negative slot, so the second receipt
        // must still find and clear the one left behind.
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var entries = new List<FifoSlotsSimulator.SimulationEntry>
        {
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = batchId,
                HasBatchNo = true,
                UseBatchwiseValuation = true,
                PostingDate = new DateTime(2021, 12, 1),
                CreationTime = new DateTime(2021, 12, 1, 10, 0, 0),
                ActualQty = -10m,
                StockValueDifference = -100m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = batchId,
                HasBatchNo = true,
                UseBatchwiseValuation = true,
                PostingDate = new DateTime(2021, 12, 2),
                CreationTime = new DateTime(2021, 12, 2, 10, 0, 0),
                ActualQty = -5m,
                StockValueDifference = -50m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = batchId,
                HasBatchNo = true,
                UseBatchwiseValuation = true,
                PostingDate = new DateTime(2021, 12, 3),
                CreationTime = new DateTime(2021, 12, 3, 10, 0, 0),
                ActualQty = 10m,
                StockValueDifference = 100m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            },
            new()
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                BatchId = batchId,
                HasBatchNo = true,
                UseBatchwiseValuation = true,
                PostingDate = new DateTime(2021, 12, 4),
                CreationTime = new DateTime(2021, 12, 4, 10, 0, 0),
                ActualQty = 5m,
                StockValueDifference = 50m,
                ValuationRate = 10m,
                ValuationMethod = "FIFO"
            }
        };

        var results = FifoSlotsSimulator.Simulate(entries, new DateTime(2021, 12, 10));

        // Queue completely cleared, no remaining slots or stock
        results.Count.ShouldBe(0);
    }
}
