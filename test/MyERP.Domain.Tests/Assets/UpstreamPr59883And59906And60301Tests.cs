using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Assets;

public class UpstreamPr59883And59906And60301Tests
{
    // =========================================================================
    // ERPNext PR #59883 / commit bf7c7b7da9: WDV requires rate or salvage value
    // =========================================================================

    [Fact]
    public void AssetDepreciationDetail_Validate_Throws_WhenWDVMissingRateAndSalvageValue()
    {
        var ex = Should.Throw<BusinessException>(() =>
        {
            var detail = new AssetDepreciationDetail(
                Guid.NewGuid(),
                Guid.NewGuid(),
                DepreciationMethod.WrittenDownValue,
                totalDepreciations: 5,
                frequencyMonths: 12,
                netPurchaseAmount: 1000m)
            {
                Rate = 0,
                ExpectedValueAfterUsefulLife = 0
            };
            detail.Validate();
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Written Down Value");
    }

    [Fact]
    public void AssetDepreciationDetail_Validate_Succeeds_WhenWDVHasRateOrSalvageValue()
    {
        // Has Salvage value
        var detailWithSalvage = new AssetDepreciationDetail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DepreciationMethod.WrittenDownValue,
            totalDepreciations: 5,
            frequencyMonths: 12,
            netPurchaseAmount: 1000m,
            rate: 0,
            expectedValueAfterUsefulLife: 100m);
        detailWithSalvage.Validate();

        // Has Rate
        var detailWithRate = new AssetDepreciationDetail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DepreciationMethod.WrittenDownValue,
            totalDepreciations: 5,
            frequencyMonths: 12,
            netPurchaseAmount: 1000m,
            rate: 20m,
            expectedValueAfterUsefulLife: 0);
        detailWithRate.Validate();
    }

    // =========================================================================
    // ERPNext PR #59883 / commit 5e523af409 & a093e4ac36: Manual schedule validation
    // =========================================================================

    [Fact]
    public void Asset_ValidateManualSchedule_Throws_WhenRowAmountZeroOrNegative()
    {
        var asset = new Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-001", "MacBook Pro",
            new DateTime(2026, 1, 1), 1200m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.Manual,
            AvailableForUseDate = new DateTime(2026, 1, 1),
            ExpectedValueAfterUsefulLife = 200m,
            ValueAfterDepreciation = 1200m
        };

        asset.DepreciationSchedule.Add(new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 6, 30), 0m, 0m));

        var ex = Should.Throw<BusinessException>(() => asset.ValidateManualSchedule());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("greater than zero");
    }

    [Fact]
    public void Asset_ValidateManualSchedule_Throws_WhenScheduleDateBeforeAvailableDate()
    {
        var asset = new Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-002", "Server Rack",
            new DateTime(2026, 1, 1), 1000m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.Manual,
            AvailableForUseDate = new DateTime(2026, 3, 1),
            ExpectedValueAfterUsefulLife = 0m,
            ValueAfterDepreciation = 1000m
        };

        asset.DepreciationSchedule.Add(new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 2, 1), 1000m, 1000m));

        var ex = Should.Throw<BusinessException>(() => asset.ValidateManualSchedule());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("cannot be before the Available-for-use Date");
    }

    [Fact]
    public void Asset_ValidateManualSchedule_Throws_WhenTotalMismatch()
    {
        var asset = new Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-003", "Office Furniture",
            new DateTime(2026, 1, 1), 1000m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.Manual,
            AvailableForUseDate = new DateTime(2026, 1, 1),
            ExpectedValueAfterUsefulLife = 100m, // depreciable = 900
            ValueAfterDepreciation = 1000m
        };

        // Total 800 != 900
        asset.DepreciationSchedule.Add(new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 6, 30), 400m, 400m));
        asset.DepreciationSchedule.Add(new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 12, 31), 400m, 800m));

        var ex = Should.Throw<BusinessException>(() => asset.ValidateManualSchedule());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("must be equal to the depreciable value");
    }

    [Fact]
    public void Asset_ValidateManualSchedule_RecomputesAccumulatedDepreciation()
    {
        var asset = new Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-004", "Delivery Van",
            new DateTime(2026, 1, 1), 1000m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.Manual,
            AvailableForUseDate = new DateTime(2026, 1, 1),
            ExpectedValueAfterUsefulLife = 200m, // depreciable = 800
            ValueAfterDepreciation = 1000m,
            OpeningAccumulatedDepreciation = 0m
        };

        var entry1 = new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 6, 30), 500m, 9999m); // incorrect initial accumulated
        var entry2 = new DepreciationScheduleEntry(
            Guid.NewGuid(), asset.Id, new DateTime(2026, 12, 31), 300m, 9999m);
        asset.DepreciationSchedule.Add(entry1);
        asset.DepreciationSchedule.Add(entry2);

        asset.ValidateManualSchedule();

        entry1.AccumulatedDepreciation.ShouldBe(500m);
        entry2.AccumulatedDepreciation.ShouldBe(800m);
    }

    // =========================================================================
    // ERPNext PR #59883 / commit a1ff01724d: Sync value with default finance book
    // =========================================================================

    [Fact]
    public void Asset_SyncValueAfterDepreciation_FollowsDefaultFinanceBook()
    {
        var asset = new Asset(
            Guid.NewGuid(), Guid.NewGuid(), "AST-005", "CNC Machine",
            new DateTime(2026, 1, 1), 1200m)
        {
            CalculateDepreciation = true,
            ValueAfterDepreciation = 1200m
        };

        var defaultBook = new AssetDepreciationDetail(
            Guid.NewGuid(), asset.Id, DepreciationMethod.StraightLine, 12, 1, 1200m)
        {
            FinanceBookId = null, // Default finance book
            ValueAfterDepreciation = 900m
        };

        var taxBook = new AssetDepreciationDetail(
            Guid.NewGuid(), asset.Id, DepreciationMethod.StraightLine, 6, 2, 1200m)
        {
            FinanceBookId = Guid.NewGuid(), // Secondary tax book
            ValueAfterDepreciation = 600m
        };

        asset.DepreciationDetails.Add(defaultBook);
        asset.DepreciationDetails.Add(taxBook);

        asset.Submit();
        asset.SyncValueAfterDepreciation();

        // Header value should match default book (900m), NOT sum or tax book
        asset.ValueAfterDepreciation.ShouldBe(900m);
        asset.Status.ShouldBe(AssetStatus.PartiallyDepreciated);
    }

    // =========================================================================
    // ERPNext PR #59906 / commit 2a691858b3: Pricing rule matches campaign or utm_campaign
    // =========================================================================

    [Fact]
    public void PricingRule_Matches_Campaign_MatchesBothCampaignAndUtmCampaign()
    {
        var itemId = Guid.NewGuid();
        var rule = new PricingRule(
            Guid.NewGuid(), "Festive Discount", PricingRuleApplyOn.ItemCode,
            PricingRuleType.Discount)
        {
            ApplyOnId = itemId,
            Campaign = "Festive-Sale-2026"
        };

        var now = DateTime.UtcNow;

        // Matches when campaign is passed
        rule.Matches(itemId, null, 1, 100, now, campaign: "Festive-Sale-2026", utmCampaign: null).ShouldBeTrue();

        // Matches when utm_campaign is passed
        rule.Matches(itemId, null, 1, 100, now, campaign: null, utmCampaign: "Festive-Sale-2026").ShouldBeTrue();

        // Rejects different campaign
        rule.Matches(itemId, null, 1, 100, now, campaign: "Summer-Sale-2026", utmCampaign: null).ShouldBeFalse();

        // Rejects null campaign when rule requires campaign
        rule.Matches(itemId, null, 1, 100, now, campaign: null, utmCampaign: null).ShouldBeFalse();
    }

    // =========================================================================
    // ERPNext PR #60301 / commit ead0229375b: Pick List bundle multi-batch aggregation
    // =========================================================================

    [Fact]
    public void PickListManager_AggregateBundlePickedBatches_AggregatesMultipleBatchesWithoutLoss()
    {
        var whId = Guid.NewGuid();
        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var entries = new List<SerialAndBatchEntry>
        {
            new SerialAndBatchEntry(Guid.NewGuid(), bundleId, -10m, 50m, batchId: batch1),
            new SerialAndBatchEntry(Guid.NewGuid(), bundleId, -5m, 50m, batchId: batch2)
        };

        var pickedQtyMap = new Dictionary<(Guid WarehouseId, Guid? BatchId), decimal>();

        PickListManager.AggregateBundlePickedBatches(pickedQtyMap, whId, entries);

        pickedQtyMap[(whId, batch1)].ShouldBe(10m);
        pickedQtyMap[(whId, batch2)].ShouldBe(5m);

        // Verify FilterLocationsByPickedMaterials consumes correctly
        var locations = new List<PickLocationAllocation>
        {
            new PickLocationAllocation { WarehouseId = whId, BatchId = batch1, Qty = 30m },
            new PickLocationAllocation { WarehouseId = whId, BatchId = batch2, Qty = 20m }
        };

        var filtered = PickListManager.FilterLocationsByPickedMaterials(locations, pickedQtyMap);

        filtered.Count.ShouldBe(2);
        filtered.First(l => l.BatchId == batch1).Qty.ShouldBe(20m);
        filtered.First(l => l.BatchId == batch2).Qty.ShouldBe(15m);
    }
}
