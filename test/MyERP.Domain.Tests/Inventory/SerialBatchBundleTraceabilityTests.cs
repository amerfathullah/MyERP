using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using NSubstitute;
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

    [Fact]
    public async Task GetFinishedGoodTargetsAsync_ExtractsSerialAndBatchTargets()
    {
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        var fgItemId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();
        entry.AddItem(fgItemId, 2m, null, targetWhId, 100m, isFinishedItem: true);
        var fgItemRow = entry.Items[0];

        var fgBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, fgItemId, targetWhId,
            BundleTransactionType.Inward, "StockEntry", entry.Id, DateTime.UtcNow)
        {
            VoucherDetailId = fgItemRow.Id
        };
        fgBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), fgBundle.Id, 1m, 100m, serialNo: "FG-SN-001"));
        fgBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), fgBundle.Id, 1m, 100m, serialNo: "FG-SN-002"));

        var bundleRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SerialAndBatchBundle, Guid>>();
        bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { fgBundle }.AsQueryable()));

        var targets = await MyERP.Inventory.DomainServices.StockEntryManager.GetFinishedGoodTargetsAsync(entry, bundleRepo);

        targets.Count.ShouldBe(2);
        targets.ShouldContain(t => t.FgSerialNo == "FG-SN-001" && t.FgBatchNo == null);
        targets.ShouldContain(t => t.FgSerialNo == "FG-SN-002" && t.FgBatchNo == null);
    }

    [Fact]
    public async Task ValidateFinishedGoodMappingsAsync_WithInvalidTarget_Throws()
    {
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();

        entry.AddItem(fgItemId, 1m, null, targetWhId, 100m, isFinishedItem: true);
        entry.AddItem(rmItemId, 1m, sourceWhId, null, 50m, isFinishedItem: false);
        var fgRow = entry.Items[0];
        var rmRow = entry.Items[1];

        var fgBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, fgItemId, targetWhId,
            BundleTransactionType.Inward, "StockEntry", entry.Id, DateTime.UtcNow)
        {
            VoucherDetailId = fgRow.Id
        };
        fgBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), fgBundle.Id, 1m, 100m, serialNo: "FG-VALID-01"));

        var rmBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, rmItemId, sourceWhId,
            BundleTransactionType.Outward, "StockEntry", entry.Id, DateTime.UtcNow)
        {
            VoucherDetailId = rmRow.Id
        };
        // Mapped to invalid target
        rmBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), rmBundle.Id, 1m, 50m, serialNo: "RM-01", fgSerialNo: "NON-EXISTENT-FG"));

        var bundleRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SerialAndBatchBundle, Guid>>();
        bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { fgBundle, rmBundle }.AsQueryable()));

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            MyERP.Inventory.DomainServices.StockEntryManager.ValidateFinishedGoodMappingsAsync(entry, bundleRepo));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidFinishedGoodMapping);
    }

    [Fact]
    public async Task AutoMapRawMaterialsToFinishedGoodsAsync_MapsUnmappedEntriesInOrder()
    {
        var companyId = Guid.NewGuid();
        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow);
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var sourceWhId = Guid.NewGuid();
        var targetWhId = Guid.NewGuid();

        entry.AddItem(fgItemId, 2m, null, targetWhId, 100m, isFinishedItem: true);
        entry.AddItem(rmItemId, 2m, sourceWhId, null, 50m, isFinishedItem: false);
        var fgRow = entry.Items[0];
        var rmRow = entry.Items[1];

        var fgBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, fgItemId, targetWhId,
            BundleTransactionType.Inward, "StockEntry", entry.Id, DateTime.UtcNow)
        {
            VoucherDetailId = fgRow.Id
        };
        fgBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), fgBundle.Id, 1m, 100m, serialNo: "FG-A"));
        fgBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), fgBundle.Id, 1m, 100m, serialNo: "FG-B"));

        var rmBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, rmItemId, sourceWhId,
            BundleTransactionType.Outward, "StockEntry", entry.Id, DateTime.UtcNow)
        {
            VoucherDetailId = rmRow.Id
        };
        var rmEntry1 = new SerialAndBatchEntry(Guid.NewGuid(), rmBundle.Id, 1m, 50m, serialNo: "RM-01");
        var rmEntry2 = new SerialAndBatchEntry(Guid.NewGuid(), rmBundle.Id, 1m, 50m, serialNo: "RM-02");
        rmBundle.AddEntry(rmEntry1);
        rmBundle.AddEntry(rmEntry2);

        var bundleRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<SerialAndBatchBundle, Guid>>();
        bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SerialAndBatchBundle, object>>[]>())
            .Returns(Task.FromResult(new List<SerialAndBatchBundle> { fgBundle, rmBundle }.AsQueryable()));

        await MyERP.Inventory.DomainServices.StockEntryManager.AutoMapRawMaterialsToFinishedGoodsAsync(entry, bundleRepo);

        rmEntry1.FgSerialNo.ShouldBe("FG-A");
        rmEntry2.FgSerialNo.ShouldBe("FG-B");
        await bundleRepo.Received(1).UpdateManyAsync(Arg.Is<IEnumerable<SerialAndBatchBundle>>(list => list.Contains(rmBundle)));
    }
}
