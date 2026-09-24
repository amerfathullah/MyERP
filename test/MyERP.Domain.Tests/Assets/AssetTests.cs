using System;
using System.Linq;
using MyERP.Assets.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Assets;

public class AssetTests
{
    [Fact]
    public void Create_ShouldSetDraftStatus()
    {
        var asset = CreateAsset();

        asset.Status.ShouldBe(AssetStatus.Draft);
        asset.ValueAfterDepreciation.ShouldBe(50000m);
    }

    [Fact]
    public void Submit_FromDraft_ShouldSucceed()
    {
        var asset = CreateAsset();

        asset.Submit();

        asset.Status.ShouldBe(AssetStatus.Submitted);
    }

    [Fact]
    public void Submit_FromSubmitted_ShouldThrow()
    {
        var asset = CreateAsset();
        asset.Submit();

        Assert.Throws<BusinessException>(() => asset.Submit());
    }

    [Fact]
    public void Sell_FromSubmitted_ShouldSetDisposal()
    {
        var asset = CreateAsset();
        asset.Submit();
        var disposalDate = new DateTime(2026, 6, 30);

        asset.Sell(disposalDate, 30000m);

        asset.Status.ShouldBe(AssetStatus.Sold);
        asset.DisposalDate.ShouldBe(disposalDate);
        asset.DisposalAmount.ShouldBe(30000m);
    }

    [Fact]
    public void Sell_FromDraft_ShouldThrow()
    {
        var asset = CreateAsset();

        Assert.Throws<BusinessException>(() => asset.Sell(DateTime.UtcNow, 10000));
    }

    [Fact]
    public void Scrap_FromSubmitted_ShouldSucceed()
    {
        var asset = CreateAsset();
        asset.Submit();

        asset.Scrap(new DateTime(2026, 7, 1));

        asset.Status.ShouldBe(AssetStatus.Scrapped);
        asset.DisposalAmount.ShouldBe(0m);
    }

    [Fact]
    public void Cancel_FromDraft_Succeeds()
    {
        var asset = CreateAsset();

        asset.Cancel();

        asset.Status.ShouldBe(AssetStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Submitted_NoDepreciationPostedYet_Succeeds()
    {
        var asset = CreateAsset();
        asset.Submit();

        // No GL impact yet — cancelling is a pure status change, no reversal needed.
        asset.Cancel();

        asset.Status.ShouldBe(AssetStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Submitted_WithBookedDepreciation_Throws()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());

        // Real GL history exists — would need those Journal Entries reversed first.
        Assert.Throws<BusinessException>(() => asset.Cancel());
    }

    [Fact]
    public void ReverseAllBookedDepreciation_ClearsBookedEntriesAndRestoresValue()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());
        asset.ValueAfterDepreciation -= asset.DepreciationSchedule[0].DepreciationAmount;
        asset.MarkPartiallyDepreciated();

        asset.ReverseAllBookedDepreciation();

        asset.DepreciationSchedule.ShouldNotBeEmpty();
        asset.DepreciationSchedule.ShouldAllBe(e => !e.IsBooked && e.JournalEntryId == null);
        asset.ValueAfterDepreciation.ShouldBe(asset.TotalAssetCost);
        asset.IsFullyDepreciated.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_PartiallyDepreciated_AfterReversal_Succeeds()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());
        asset.MarkPartiallyDepreciated();

        // Caller reverses GL first (not modeled here — pure domain test), then resets state.
        asset.ReverseAllBookedDepreciation();
        asset.Cancel();

        asset.Status.ShouldBe(AssetStatus.Cancelled);
    }

    [Fact]
    public void Cancel_PartiallyDepreciated_WithStillBookedDepreciation_Throws()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());
        asset.MarkPartiallyDepreciated();

        // Widening Cancel()'s allowed-status set must NOT bypass the booked-depreciation guard.
        Assert.Throws<BusinessException>(() => asset.Cancel());
    }

    [Fact]
    public void Cancel_Scrapped_Throws()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.Scrap(new DateTime(2026, 7, 1));

        // Must be restored first, per Asset.Restore().
        Assert.Throws<BusinessException>(() => asset.Cancel());
    }

    [Fact]
    public void TotalAssetCost_IncludesAdditional()
    {
        var asset = CreateAsset();
        asset.AdditionalCost = 5000;

        asset.TotalAssetCost.ShouldBe(55000m);
    }

    [Fact]
    public void GenerateDepreciationSchedule_StraightLine()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2025, 1, 1);

        asset.GenerateDepreciationSchedule();

        asset.DepreciationSchedule.Count.ShouldBe(5); // 60/12 = 5 periods
        asset.DepreciationSchedule[0].DepreciationAmount.ShouldBe(10000m); // 50000/5
        asset.DepreciationSchedule[4].AccumulatedDepreciation.ShouldBe(50000m);
    }

    [Fact]
    public void GenerateDepreciationSchedule_CompleteOpeningPeriods_PR59304()
    {
        // Per ERPNext PR #59304 / commit 56f24a6adf / test_complete_opening_periods:
        // Purchase 13200, salvage 1200, useful 12 months, freq 1 month (12 periods).
        // 4 opening booked depreciations @ 1000 each = 4000 opening accumulated.
        // Remaining 8 periods should each be 1000m, starting on DepreciationStartDate.
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-OP-01", "Imported Equipment",
            new DateTime(2026, 1, 1), 13200m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.StraightLine,
            UsefulLifeMonths = 12,
            FrequencyMonths = 1,
            AvailableForUseDate = new DateTime(2026, 1, 1),
            DepreciationStartDate = new DateTime(2026, 5, 31),
            ExpectedValueAfterUsefulLife = 1200m,
            OpeningNumberOfBookedDepreciations = 4,
            OpeningAccumulatedDepreciation = 4000m
        };

        asset.GenerateDepreciationSchedule();

        asset.DepreciationSchedule.Count.ShouldBe(8); // 12 - 4 = 8 remaining periods
        asset.DepreciationSchedule.All(r => r.DepreciationAmount == 1000m).ShouldBeTrue();
        asset.DepreciationSchedule[0].ScheduleDate.ShouldBe(new DateTime(2026, 5, 31));
        asset.DepreciationSchedule[7].AccumulatedDepreciation.ShouldBe(12000m);
    }

    [Fact]
    public void GenerateDepreciationSchedule_QuarterlyOpeningPeriod_PR59304()
    {
        // Quarterly asset with 1 opening period
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-OP-02", "Machinery",
            new DateTime(2026, 1, 1), 13200m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.StraightLine,
            UsefulLifeMonths = 36,
            FrequencyMonths = 3, // 12 periods
            AvailableForUseDate = new DateTime(2026, 1, 1),
            DepreciationStartDate = new DateTime(2026, 6, 30),
            ExpectedValueAfterUsefulLife = 1200m,
            OpeningNumberOfBookedDepreciations = 1,
            OpeningAccumulatedDepreciation = 1000m
        };

        asset.GenerateDepreciationSchedule();

        asset.DepreciationSchedule.Count.ShouldBe(11); // 12 - 1 = 11 periods
        asset.DepreciationSchedule.All(r => r.DepreciationAmount == 1000m).ShouldBeTrue();
        asset.DepreciationSchedule[0].ScheduleDate.ShouldBe(new DateTime(2026, 6, 30));
        asset.DepreciationSchedule[10].AccumulatedDepreciation.ShouldBe(12000m);
    }

    [Fact]
    public void SimulateBookValueAtDate_WithOpeningDepreciations()
    {
        var asset = new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-OP-03", "Vehicle",
            new DateTime(2026, 1, 1), 13200m)
        {
            CalculateDepreciation = true,
            DepreciationMethod = DepreciationMethod.StraightLine,
            UsefulLifeMonths = 12,
            FrequencyMonths = 1,
            AvailableForUseDate = new DateTime(2026, 1, 1),
            DepreciationStartDate = new DateTime(2026, 5, 31),
            ExpectedValueAfterUsefulLife = 1200m,
            OpeningNumberOfBookedDepreciations = 4,
            OpeningAccumulatedDepreciation = 4000m
        };

        // Before start date: purchase amount minus opening accumulated = 9200
        asset.SimulateBookValueAtDate(new DateTime(2026, 1, 1)).ShouldBe(9200m);
        // On first remaining period (2026-05-31): 9200 - 1000 = 8200
        asset.SimulateBookValueAtDate(new DateTime(2026, 5, 31)).ShouldBe(8200m);
    }

    [Fact]
    public void MarkFullyDepreciated_SetsValueToZero()
    {
        var asset = CreateAsset();
        asset.Submit();

        asset.MarkFullyDepreciated();

        asset.Status.ShouldBe(AssetStatus.FullyDepreciated);
        asset.IsFullyDepreciated.ShouldBeTrue();
        asset.ValueAfterDepreciation.ShouldBe(0);
    }

    private static Asset CreateAsset() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "AST-0001", "Office Laptop",
            new DateTime(2025, 1, 15), 50000m, Guid.NewGuid());
}
