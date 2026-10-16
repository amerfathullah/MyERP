using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class BatchBundleValuationTests
{
    [Fact]
    public void Recalculate_CalculatesUnitRateCorrectly_ForMultiUnitBundle()
    {
        // Per ERPNext PR #58994 / commit 825d24f406:
        // Bundle with 5 units at rate 10 has total amount 50, unit rate must be 10 (not 50).
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 5m, 10m));

        Assert.Equal(5m, bundle.TotalQty);
        Assert.Equal(50m, bundle.TotalAmount);
        Assert.Equal(10m, bundle.AvgRate);
        Assert.Equal(10m, bundle.CalculateUnitValuationRate());
    }

    [Fact]
    public void Recalculate_CalculatesUnitRateCorrectly_ForWeightedMultiEntries()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 3m, 10m)); // 30
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 7m, 20m)); // 140

        Assert.Equal(10m, bundle.TotalQty);
        Assert.Equal(170m, bundle.TotalAmount);
        Assert.Equal(17m, bundle.AvgRate);
        Assert.Equal(17m, bundle.CalculateUnitValuationRate());
    }

    [Fact]
    public void Recalculate_ReturnsPositiveUnitRate_ForOutwardNegativeQuantities()
    {
        var bundle = new SerialAndBatchBundle(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(), DateTime.UtcNow);

        // Outward entries may be recorded as negative qty or positive qty with Outward type
        bundle.Entries.Add(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, -5m, 12m));
        bundle.Recalculate();

        Assert.Equal(-5m, bundle.TotalQty);
        Assert.Equal(-60m, bundle.TotalAmount);
        Assert.Equal(12m, bundle.AvgRate);
        Assert.Equal(12m, bundle.CalculateUnitValuationRate());
    }

    [Fact]
    public async Task GetValuationRateAsync_ComputesBatchwiseValuation_WhenBatchHasFlag()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var batchRepo = Substitute.For<IRepository<Batch, Guid>>();

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var batch = new Batch(batchId, itemId, "B-001") { UseBatchwiseValuation = true };
        batchRepo.FindAsync(batchId).Returns(batch);

        var entries = new List<StockLedgerEntry>
        {
            new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId, DateTime.UtcNow.AddDays(-2), 10m, 15m, 10m, 150m)
            {
                BatchId = batchId,
                StockValueDifference = 150m,
            },
            new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), itemId, warehouseId, DateTime.UtcNow.AddDays(-1), 10m, 25m, 20m, 400m)
            {
                BatchId = batchId,
                StockValueDifference = 250m,
            }
        };

        var queryable = entries.AsQueryable();
        sleRepo.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider, batchRepo);

        var rate = await valuationService.GetValuationRateAsync(itemId, warehouseId, batchId: batchId);

        // Total qty = 20, Total value diff = 400 => rate = 20
        Assert.Equal(20m, rate);
    }

    [Fact]
    public async Task GetValuationRateAsync_ComputesBundleValuationPerUnit()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var bundle = new SerialAndBatchBundle(
            bundleId, Guid.NewGuid(), itemId, warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow);

        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 4m, 10m)); // 40
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundleId, 6m, 20m)); // 120
        // Total Qty = 10, Total Amount = 160 => Unit Rate = 16
        bundleRepo.FindAsync(bundleId).Returns(bundle);

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider, null, bundleRepo);

        var rate = await valuationService.GetValuationRateAsync(itemId, warehouseId, serialAndBatchBundleId: bundleId);

        Assert.Equal(16m, rate);
    }

    [Fact]
    public void CalculateTransferRates_AddsProportionalAdditionalCost_ToEachOutwardBatch()
    {
        // Per ERPNext PR #59657 / commit 1cf560fbc7:
        // Batch 1 @ 100, Batch 2 @ 200, row additional cost 20 across 2 units.
        // Batch 1 gets 100 + 10 = 110, Batch 2 gets 200 + 10 = 210.
        var outwardBundleId = Guid.NewGuid();
        var outwardBundle = new SerialAndBatchBundle(
            outwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var batch1Id = Guid.NewGuid();
        var batch2Id = Guid.NewGuid();

        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 100m, batchId: batch1Id));
        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 200m, batchId: batch2Id));

        var rates = SerialAndBatchBundle.CalculateTransferRates(outwardBundle, additionalCost: 20m, transferQty: 2m);

        Assert.Equal(110m, rates[(null, batch1Id)]);
        Assert.Equal(210m, rates[(null, batch2Id)]);
    }

    [Fact]
    public void ApplyTransferRates_UpdatesInwardBundleEntries_PreservingBatchWiseValuation()
    {
        // Per ERPNext PR #59657 / commit 1cf560fbc7:
        // Inward bundle receives each batch with outward rate + additional cost, avoiding value shifting.
        var outwardBundleId = Guid.NewGuid();
        var outwardBundle = new SerialAndBatchBundle(
            outwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var batch1Id = Guid.NewGuid();
        var batch2Id = Guid.NewGuid();

        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 100m, batchId: batch1Id));
        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 200m, batchId: batch2Id));

        var inwardBundleId = Guid.NewGuid();
        var inwardBundle = new SerialAndBatchBundle(
            inwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var inEntry1 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, batchId: batch1Id);
        var inEntry2 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, batchId: batch2Id);
        inwardBundle.AddEntry(inEntry1);
        inwardBundle.AddEntry(inEntry2);

        inwardBundle.ApplyTransferRates(outwardBundle, additionalCost: 20m, transferQty: 2m);

        Assert.Equal(110m, inEntry1.IncomingRate);
        Assert.Equal(210m, inEntry2.IncomingRate);
        Assert.Equal(320m, inwardBundle.TotalAmount);
        Assert.Equal(160m, inwardBundle.AvgRate);
    }

    [Fact]
    public void ApplyTransferRates_SupportsSerializedItems_WithAdditionalCost()
    {
        var outwardBundleId = Guid.NewGuid();
        var outwardBundle = new SerialAndBatchBundle(
            outwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 500m, serialNo: "SN-001"));
        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 700m, serialNo: "SN-002"));

        var inwardBundleId = Guid.NewGuid();
        var inwardBundle = new SerialAndBatchBundle(
            inwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var inEntry1 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, serialNo: "SN-001");
        var inEntry2 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, serialNo: "SN-002");
        inwardBundle.AddEntry(inEntry1);
        inwardBundle.AddEntry(inEntry2);

        inwardBundle.ApplyTransferRates(outwardBundle, additionalCost: 50m, transferQty: 2m);

        Assert.Equal(525m, inEntry1.IncomingRate);
        Assert.Equal(725m, inEntry2.IncomingRate);
        Assert.Equal(1250m, inwardBundle.TotalAmount);
        Assert.Equal(625m, inwardBundle.AvgRate);
    }

    [Fact]
    public void ApplyTransferRates_UnmatchedEntry_TakesRowRate_NotPreviousEntryRate_PerPR60629()
    {
        // Per ERPNext PR #60629 / commit 6e580e3f18:
        // Each entry starts from the row rate, so an entry without an outward match
        // takes the default row rate (+ additional cost) rather than inheriting an earlier entry's rate.
        var outwardBundleId = Guid.NewGuid();
        var outwardBundle = new SerialAndBatchBundle(
            outwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var batch1Id = Guid.NewGuid();
        var batch2Id = Guid.NewGuid(); // not present in outward bundle

        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 150m, batchId: batch1Id));

        var inwardBundleId = Guid.NewGuid();
        var inwardBundle = new SerialAndBatchBundle(
            inwardBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var inEntry1 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, batchId: batch1Id);
        var inEntry2 = new SerialAndBatchEntry(Guid.NewGuid(), inwardBundleId, 1m, 0m, batchId: batch2Id);
        inwardBundle.AddEntry(inEntry1);
        inwardBundle.AddEntry(inEntry2);

        inwardBundle.ApplyTransferRates(outwardBundle, additionalCost: 20m, transferQty: 2m, defaultRate: 80m);

        // inEntry1 matches batch1: 150 + (20/2) = 160
        Assert.Equal(160m, inEntry1.IncomingRate);
        // inEntry2 does not match: starts from defaultRate 80 + (20/2) = 90 (not 160!)
        Assert.Equal(90m, inEntry2.IncomingRate);
        Assert.Equal(250m, inwardBundle.TotalAmount);
        Assert.Equal(125m, inwardBundle.AvgRate);
    }

    [Fact]
    public void Repack_FinishedGoodValuation_ValuesAtConsumedCostOfAutoPickedBundles()
    {
        // Per ERPNext PR #60214 / commit ef827e263b & fc069b5516:
        // Raw material consumed with 2 units: 1 unit @ 100, 1 unit @ 200.
        // Finished good qty = 1. Finished good incoming rate must be 300.
        var rmBundleId = Guid.NewGuid();
        var rmBundle = new SerialAndBatchBundle(
            rmBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        var batch1Id = Guid.NewGuid();
        var batch2Id = Guid.NewGuid();

        rmBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), rmBundleId, 1m, 100m, batchId: batch1Id));
        rmBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), rmBundleId, 1m, 200m, batchId: batch2Id));

        var totalOutwardValue = rmBundle.Entries.Sum(e => e.Qty * e.IncomingRate);
        var fgQty = 1m;
        var additionalCost = 0m;

        var fgRate = (totalOutwardValue + additionalCost) / fgQty;

        Assert.Equal(300m, totalOutwardValue);
        Assert.Equal(300m, fgRate);
    }

    [Fact]
    public void Repack_FinishedGoodValuation_IncludesAdditionalCost()
    {
        // Consumed RM value = 300 across 2 units. FG qty = 1, additional cost = 50.
        // FG valuation rate = (300 + 50) / 1 = 350.
        var rmBundleId = Guid.NewGuid();
        var rmBundle = new SerialAndBatchBundle(
            rmBundleId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BundleTransactionType.Outward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow);

        rmBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), rmBundleId, 1m, 100m));
        rmBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), rmBundleId, 1m, 200m));

        var totalOutwardValue = rmBundle.Entries.Sum(e => e.Qty * e.IncomingRate);
        var fgQty = 1m;
        var additionalCost = 50m;

        var fgRate = (totalOutwardValue + additionalCost) / fgQty;

        Assert.Equal(350m, fgRate);
    }

    [Fact]
    public async Task GetSerialNoWiseIncomingRateAsync_FetchesLatestInwardRate_InWarehouse_PerPR60771()
    {
        // Per ERPNext PR #60771 / commit 8233482921:
        // Receipt 1 @ 100 on day -10. Receipt 2 @ 150 on day -6.
        // Serial numbers queried on day -2 should receive the latest rate 150.
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var bundle1 = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow.AddDays(-10));
        bundle1.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle1.Id, 1m, 100m, serialNo: "SN-001"));
        bundle1.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle1.Id, 1m, 100m, serialNo: "SN-002"));
        bundle1.Submit();

        var bundle2 = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow.AddDays(-6));
        bundle2.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle2.Id, 1m, 150m, serialNo: "SN-001"));
        bundle2.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle2.Id, 1m, 150m, serialNo: "SN-002"));
        bundle2.Submit();

        var bundles = new List<SerialAndBatchBundle> { bundle1, bundle2 }.AsQueryable();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(bundles));

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider, null, bundleRepo);

        var rates = await valuationService.GetSerialNoWiseIncomingRateAsync(
            itemId, warehouseId, new[] { "SN-001", "SN-002" }, asOfDate: DateTime.UtcNow.AddDays(-2));

        Assert.Equal(150m, rates["SN-001"]);
        Assert.Equal(150m, rates["SN-002"]);
    }

    [Fact]
    public async Task GetSerialNoWiseIncomingRateAsync_ExcludesCurrentVoucher()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var voucherToExclude = Guid.NewGuid();

        var bundle1 = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            BundleTransactionType.Inward, "StockEntry", Guid.NewGuid(), DateTime.UtcNow.AddDays(-5));
        bundle1.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle1.Id, 1m, 80m, serialNo: "SN-001"));
        bundle1.Submit();

        var bundle2 = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            BundleTransactionType.Inward, "StockEntry", voucherToExclude, DateTime.UtcNow.AddDays(-2));
        bundle2.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle2.Id, 1m, 120m, serialNo: "SN-001"));
        bundle2.Submit();

        var bundles = new List<SerialAndBatchBundle> { bundle1, bundle2 }.AsQueryable();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(bundles));

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider, null, bundleRepo);

        var rate = await valuationService.GetSerialNoIncomingRateAsync(
            itemId, warehouseId, "SN-001", excludeVoucherId: voucherToExclude);

        // Excluded bundle2 (120m), so should return bundle1 (80m)
        Assert.Equal(80m, rate);
    }

    [Fact]
    public async Task GetValuationRateAsync_ResolvesSerialRatesForOutwardBundle_WhenRatesZero()
    {
        var sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var settingProvider = Substitute.For<ISettingProvider>();
        var bundleRepo = Substitute.For<IRepository<SerialAndBatchBundle, Guid>>();

        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var inwardBundle = new SerialAndBatchBundle(
            Guid.NewGuid(), companyId, itemId, warehouseId,
            BundleTransactionType.Inward, "PurchaseReceipt", Guid.NewGuid(), DateTime.UtcNow.AddDays(-5));
        inwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), inwardBundle.Id, 1m, 250m, serialNo: "SN-A"));
        inwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), inwardBundle.Id, 1m, 350m, serialNo: "SN-B"));
        inwardBundle.Submit();

        var outwardBundleId = Guid.NewGuid();
        var outwardBundle = new SerialAndBatchBundle(
            outwardBundleId, companyId, itemId, warehouseId,
            BundleTransactionType.Outward, "DeliveryNote", Guid.NewGuid(), DateTime.UtcNow.AddDays(-1));
        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 0m, serialNo: "SN-A"));
        outwardBundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), outwardBundleId, 1m, 0m, serialNo: "SN-B"));

        var bundles = new List<SerialAndBatchBundle> { inwardBundle, outwardBundle }.AsQueryable();
        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(bundles));
        bundleRepo.FindAsync(outwardBundleId).Returns(outwardBundle);

        var valuationService = new StockValuationService(sleRepo, itemRepo, settingProvider, null, bundleRepo);

        var rate = await valuationService.GetValuationRateAsync(
            itemId, warehouseId, serialAndBatchBundleId: outwardBundleId);

        // (250 + 350) / 2 = 300
        Assert.Equal(300m, rate);
    }
}
