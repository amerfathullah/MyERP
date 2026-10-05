using System;
using System.Collections.Generic;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class SerialBatchBundleTraceabilityTests
{
    [Fact]
    public void SetFinishedGoodMapping_SetsFgSerialAndBatch()
    {
        var entry = new SerialAndBatchEntry(
            Guid.NewGuid(), Guid.NewGuid(), 1m, 25m, serialNo: "RM-SN-001");

        entry.SetFinishedGoodMapping("FG-SN-100", "FG-BATCH-A");

        entry.FgSerialNo.ShouldBe("FG-SN-100");
        entry.FgBatchNo.ShouldBe("FG-BATCH-A");
    }

    [Fact]
    public void MapEntryToFinishedGood_UpdatesTargetEntry()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var entryId = Guid.NewGuid();
        var entry = new SerialAndBatchEntry(entryId, bundle.Id, 1m, 15m, serialNo: "RM-01");
        bundle.AddEntry(entry);

        bundle.MapEntryToFinishedGood(entryId, "FG-SN-999", "FG-BATCH-01");

        entry.FgSerialNo.ShouldBe("FG-SN-999");
        entry.FgBatchNo.ShouldBe("FG-BATCH-01");
    }

    [Fact]
    public void MapEntryToFinishedGood_CancelledBundle_Throws()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var entryId = Guid.NewGuid();
        bundle.AddEntry(new SerialAndBatchEntry(entryId, bundle.Id, 1m, 15m, serialNo: "RM-01"));
        bundle.Cancel();

        var ex = Should.Throw<BusinessException>(() =>
            bundle.MapEntryToFinishedGood(entryId, "FG-SN-999", "FG-BATCH-01"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    [Fact]
    public void AutoMapToFinishedGoods_SingleFg_MapsAllRawMaterials()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var e1 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-01");
        var e2 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-02");
        var e3 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 5m, 10m, batchId: Guid.NewGuid());
        bundle.AddEntry(e1);
        bundle.AddEntry(e2);
        bundle.AddEntry(e3);

        bundle.AutoMapToFinishedGoods(new (string? FgSerialNo, string? FgBatchNo)[] { ("FG-SERIAL-1", "FG-BATCH-1") });

        e1.FgSerialNo.ShouldBe("FG-SERIAL-1");
        e1.FgBatchNo.ShouldBe("FG-BATCH-1");
        e2.FgSerialNo.ShouldBe("FG-SERIAL-1");
        e2.FgBatchNo.ShouldBe("FG-BATCH-1");
        e3.FgSerialNo.ShouldBe("FG-SERIAL-1");
        e3.FgBatchNo.ShouldBe("FG-BATCH-1");
    }

    [Fact]
    public void AutoMapToFinishedGoods_MultipleFgs_SpreadsSerializedEntries()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var e1 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-01");
        var e2 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-02");
        var e3 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-03");
        var e4 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-04");
        bundle.AddEntry(e1);
        bundle.AddEntry(e2);
        bundle.AddEntry(e3);
        bundle.AddEntry(e4);

        var fgs = new (string? FgSerialNo, string? FgBatchNo)[]
        {
            ("FG-01", null),
            ("FG-02", null),
        };

        bundle.AutoMapToFinishedGoods(fgs);

        e1.FgSerialNo.ShouldBe("FG-01");
        e2.FgSerialNo.ShouldBe("FG-02");
        e3.FgSerialNo.ShouldBe("FG-01");
        e4.FgSerialNo.ShouldBe("FG-02");
    }

    [Fact]
    public void AutoMapToFinishedGoods_PreservesExistingMapping()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var e1 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-01", fgSerialNo: "EXISTING-FG");
        var e2 = new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 1m, 10m, serialNo: "RM-02");
        bundle.AddEntry(e1);
        bundle.AddEntry(e2);

        bundle.AutoMapToFinishedGoods(new (string? FgSerialNo, string? FgBatchNo)[] { ("NEW-FG", null) });

        e1.FgSerialNo.ShouldBe("EXISTING-FG");
        e2.FgSerialNo.ShouldBe("NEW-FG");
    }
}
