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
}
