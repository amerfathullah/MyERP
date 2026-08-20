using System;
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
