using System;
using System.Linq;
using MyERP.Assets.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Assets;

public class AssetLifecycleManagerTests
{
    [Fact]
    public void DisposalGainLoss_Gain_WhenSoldAboveBookValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 4000; // Depreciated to 4000
        asset.Sell(DateTime.UtcNow, 6000); // Sold for 6000

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset);

        gainLoss.ShouldBe(2000m); // 6000 - 4000 = 2000 gain
    }

    [Fact]
    public void DisposalGainLoss_Loss_WhenSoldBelowBookValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 7000;
        asset.Sell(DateTime.UtcNow, 5000);

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset);

        gainLoss.ShouldBe(-2000m); // 5000 - 7000 = -2000 loss
    }

    [Fact]
    public void DisposalGainLoss_Zero_WhenSoldAtBookValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 5000;
        asset.Sell(DateTime.UtcNow, 5000);

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        manager.CalculateDisposalGainLoss(asset).ShouldBe(0);
    }

    [Fact]
    public void DisposalGainLoss_Scrap_IsAlwaysLoss()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 3000;
        asset.Scrap(DateTime.UtcNow); // DisposalAmount = 0

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset);

        gainLoss.ShouldBe(-3000m); // 0 - 3000 = -3000 loss
    }

    [Fact]
    public void RepairOptions_FullyDepreciated_CannotCapitalize()
    {
        var asset = CreateAsset();
        asset.MarkFullyDepreciated();

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var (canCapitalize, canExtendLife) = manager.GetRepairOptions(asset);

        canCapitalize.ShouldBeFalse();
        canExtendLife.ShouldBeFalse();
    }

    [Fact]
    public void RepairOptions_PartiallyDepreciated_CanCapitalize()
    {
        var asset = CreateAsset();
        asset.Submit();
        asset.MarkPartiallyDepreciated();

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var (canCapitalize, canExtendLife) = manager.GetRepairOptions(asset);

        canCapitalize.ShouldBeTrue();
        canExtendLife.ShouldBeTrue();
    }

    [Fact]
    public void RepairOptions_Submitted_CanCapitalize()
    {
        var asset = CreateAsset();
        asset.Submit();

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        var (canCapitalize, canExtendLife) = manager.GetRepairOptions(asset);

        canCapitalize.ShouldBeTrue();
        canExtendLife.ShouldBeTrue();
    }

    [Fact]
    public void ValidateForSubmission_WithDepreciation_RequiresUsefulLife()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 0;
        asset.AvailableForUseDate = DateTime.UtcNow;

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        Should.Throw<Volo.Abp.BusinessException>(() =>
            manager.ValidateForSubmission(asset));
    }

    [Fact]
    public void ValidateForSubmission_WithDepreciation_RequiresAvailableDate()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.AvailableForUseDate = null;

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        Should.Throw<Volo.Abp.BusinessException>(() =>
            manager.ValidateForSubmission(asset));
    }

    [Fact]
    public void ValidateForSubmission_WithoutDepreciation_AlwaysPasses()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = false;

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        // Should not throw regardless of other fields
        manager.ValidateForSubmission(asset);
    }

    [Fact]
    public void ValidateForSubmission_ValidSettings_Passes()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = true;
        asset.UsefulLifeMonths = 60;
        asset.AvailableForUseDate = DateTime.UtcNow;

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!);
        manager.ValidateForSubmission(asset);
    }

    [Fact]
    public void DepreciationSchedule_StraightLine_EvenDistribution()
    {
        var asset = CreateAsset(purchaseAmount: 12000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60; // 5 years
        asset.FrequencyMonths = 12;  // Annual
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);

        asset.GenerateDepreciationSchedule();

        asset.DepreciationSchedule.Count.ShouldBe(5);
        // Total should equal purchase amount
        var totalDepreciation = asset.DepreciationSchedule.Sum(e => e.DepreciationAmount);
        totalDepreciation.ShouldBe(12000m);
    }

    [Fact]
    public void DepreciationSchedule_LastPeriod_AbsorbsRounding()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 36; // 3 years
        asset.FrequencyMonths = 12;  // Annual = 3 periods
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);

        asset.GenerateDepreciationSchedule();

        // 10000/3 = 3333.33... so rounding must absorb
        var total = asset.DepreciationSchedule.Sum(e => e.DepreciationAmount);
        total.ShouldBe(10000m);
    }

    private static Asset CreateAsset(decimal purchaseAmount = 10000m)
    {
        return new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Test Asset",
            DateTime.UtcNow, purchaseAmount);
    }
}
