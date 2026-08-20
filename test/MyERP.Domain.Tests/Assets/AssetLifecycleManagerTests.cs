using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
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
        var disposalDate = DateTime.UtcNow;
        asset.Sell(disposalDate, 6000); // Sold for 6000

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset, disposalDate, 6000);

        gainLoss.ShouldBe(2000m); // 6000 - 4000 = 2000 gain
    }

    [Fact]
    public void DisposalGainLoss_Loss_WhenSoldBelowBookValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 7000;
        var disposalDate = DateTime.UtcNow;
        asset.Sell(disposalDate, 5000);

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset, disposalDate, 5000);

        gainLoss.ShouldBe(-2000m); // 5000 - 7000 = -2000 loss
    }

    [Fact]
    public void DisposalGainLoss_Zero_WhenSoldAtBookValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 5000;
        var disposalDate = DateTime.UtcNow;
        asset.Sell(disposalDate, 5000);

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        manager.CalculateDisposalGainLoss(asset, disposalDate, 5000).ShouldBe(0);
    }

    [Fact]
    public void DisposalGainLoss_Scrap_IsAlwaysLoss()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 3000;
        var disposalDate = DateTime.UtcNow;
        asset.Scrap(disposalDate); // DisposalAmount = 0

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        var gainLoss = manager.CalculateDisposalGainLoss(asset, disposalDate, 0);

        gainLoss.ShouldBe(-3000m); // 0 - 3000 = -3000 loss
    }

    [Fact]
    public void Restore_NotScrapped_Throws()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();

        Should.Throw<BusinessException>(() => asset.Restore());
    }

    [Fact]
    public void Restore_SoldAsset_Throws()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.Sell(DateTime.UtcNow, 6000);

        // Restore is scrap-only — a sold asset has real external proceeds, not reversible this way.
        Should.Throw<BusinessException>(() => asset.Restore());
    }

    [Fact]
    public void Restore_Scrapped_ClearsDisposalFieldsAndRecomputesPartiallyDepreciatedStatus()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 4000; // some depreciation already posted
        asset.Scrap(DateTime.UtcNow);

        asset.Restore();

        asset.Status.ShouldBe(AssetStatus.PartiallyDepreciated);
        asset.DisposalDate.ShouldBeNull();
        asset.DisposalAmount.ShouldBeNull();
    }

    [Fact]
    public void Restore_Scrapped_FullyDepreciated_RecomputesFullyDepreciatedStatus()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.ValueAfterDepreciation = 0; // already fully depreciated before it was scrapped
        asset.Scrap(DateTime.UtcNow);

        asset.Restore();

        asset.Status.ShouldBe(AssetStatus.FullyDepreciated);
    }

    [Fact]
    public void Restore_Scrapped_NoDepreciationYet_RecomputesSubmittedStatus()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        // ValueAfterDepreciation still equals the original purchase amount — no depreciation posted.
        asset.Scrap(DateTime.UtcNow);

        asset.Restore();

        asset.Status.ShouldBe(AssetStatus.Submitted);
    }

    [Fact]
    public async Task PostDisposalJournalEntry_SaleWithoutSettlementAccount_Throws()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.Sell(DateTime.UtcNow, 6000);

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);

        await Should.ThrowAsync<BusinessException>(
            () => manager.PostDisposalJournalEntryAsync(asset, disposalAmount: 6000, preDisposalValueAfterDepreciation: 4000, settlementAccountId: null));
    }

    [Fact]
    public async Task PostDisposalJournalEntry_NoAssetCategory_Throws()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.Submit();
        asset.Scrap(DateTime.UtcNow);
        // AssetCategoryId left unset (null) — nothing to resolve accounts from.

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);

        await Should.ThrowAsync<BusinessException>(
            () => manager.PostDisposalJournalEntryAsync(asset, disposalAmount: 0, preDisposalValueAfterDepreciation: 3000, settlementAccountId: null));
    }

    [Fact]
    public async Task PostDisposalJournalEntry_CategoryHasNoAccountsForCompany_Throws()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        var categoryId = Guid.NewGuid();
        asset.AssetCategoryId = categoryId;
        asset.Submit();
        asset.Scrap(DateTime.UtcNow);

        var category = new AssetCategory(categoryId, "Test Category");
        // No AddAccount() call for asset.CompanyId — GetAccountForCompany() returns null.

        var categoryRepo = Substitute.For<IRepository<AssetCategory, Guid>>();
        categoryRepo.FindAsync(categoryId, Arg.Any<bool>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(category);

        var manager = new DomainServices.AssetLifecycleManager(null!, categoryRepo, null!, null!, null!, null!);

        await Should.ThrowAsync<BusinessException>(
            () => manager.PostDisposalJournalEntryAsync(asset, disposalAmount: 0, preDisposalValueAfterDepreciation: 3000, settlementAccountId: null));
    }

    [Fact]
    public void RepairOptions_FullyDepreciated_CannotCapitalize()
    {
        var asset = CreateAsset();
        asset.MarkFullyDepreciated();

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
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

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        var (canCapitalize, canExtendLife) = manager.GetRepairOptions(asset);

        canCapitalize.ShouldBeTrue();
        canExtendLife.ShouldBeTrue();
    }

    [Fact]
    public void RepairOptions_Submitted_CanCapitalize()
    {
        var asset = CreateAsset();
        asset.Submit();

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
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

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
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

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
        Should.Throw<Volo.Abp.BusinessException>(() =>
            manager.ValidateForSubmission(asset));
    }

    [Fact]
    public void ValidateForSubmission_WithoutDepreciation_AlwaysPasses()
    {
        var asset = CreateAsset();
        asset.CalculateDepreciation = false;

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
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

        var manager = new DomainServices.AssetLifecycleManager(null!, null!, null!, null!, null!, null!);
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

    [Fact]
    public void DepreciationSchedule_Regenerate_PreservesBookedRows()
    {
        var asset = CreateAsset(purchaseAmount: 12000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60; // 5 years
        asset.FrequencyMonths = 12;  // Annual = 5 periods
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);

        asset.GenerateDepreciationSchedule();
        var firstPeriod = asset.DepreciationSchedule[0];
        var bookedJournalEntryId = Guid.NewGuid();
        firstPeriod.Book(bookedJournalEntryId);

        // Simulate a repair capitalization / value adjustment triggering regeneration
        // on an asset that already has a posted period.
        asset.GenerateDepreciationSchedule();

        // The booked row must survive unchanged — same id, same amount, still linked to its JE.
        asset.DepreciationSchedule.ShouldContain(e => e.Id == firstPeriod.Id && e.IsBooked
            && e.JournalEntryId == bookedJournalEntryId && e.DepreciationAmount == firstPeriod.DepreciationAmount);

        // Exactly one row per period still — no duplicate unbooked row for the already-booked period.
        asset.DepreciationSchedule.Count(e => e.ScheduleDate == firstPeriod.ScheduleDate).ShouldBe(1);
        asset.DepreciationSchedule.Count.ShouldBe(5);
    }

    [Fact]
    public void DepreciationSchedule_Regenerate_AfterCapitalization_ExtendsRemainingPeriods()
    {
        var asset = CreateAsset(purchaseAmount: 12000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60; // 5 years
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);

        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());
        var bookedAmount = asset.DepreciationSchedule[0].DepreciationAmount;

        // Repair capitalization adds cost and regenerates — booked period 1 must be untouched,
        // remaining 4 periods rebuilt against the new total cost basis.
        asset.ApplyRepairCapitalization(additionalCost: 3000, increaseInUsefulLifeMonths: 0);

        asset.DepreciationSchedule.Count.ShouldBe(5);
        asset.DepreciationSchedule[0].IsBooked.ShouldBeTrue();
        asset.DepreciationSchedule[0].DepreciationAmount.ShouldBe(bookedAmount);
        var totalDepreciation = asset.DepreciationSchedule.Sum(e => e.DepreciationAmount);
        totalDepreciation.ShouldBe(15000m); // 12000 original + 3000 capitalized
    }

    [Fact]
    public void SimulateBookValueAtDate_NoDepreciation_UsesStoredValue()
    {
        var asset = CreateAsset(purchaseAmount: 10000);
        asset.ValueAfterDepreciation = 4000; // set directly, not via a schedule

        asset.SimulateBookValueAtDate(DateTime.UtcNow).ShouldBe(4000m);
    }

    [Fact]
    public void SimulateBookValueAtDate_MidPeriod_ProratesBeyondLastBookedEntry()
    {
        var asset = CreateAsset(purchaseAmount: 12000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60; // 5 annual periods of 2400 each
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid()); // booked through 2027-01-01, book value 9600

        // Disposal happens exactly halfway through the second period (2027-01-01 to 2028-01-01).
        var disposalDate = new DateTime(2027, 7, 2);

        var simulated = asset.SimulateBookValueAtDate(disposalDate);

        // ~half of the 2400 second-period charge accrued beyond the last booked entry.
        simulated.ShouldBeLessThan(9600m);
        simulated.ShouldBeGreaterThan(9600m - 2400m);
    }

    [Fact]
    public void SimulateBookValueAtDate_ExactlyOnScheduleDate_MatchesFullPeriodAmount()
    {
        var asset = CreateAsset(purchaseAmount: 12000);
        asset.CalculateDepreciation = true;
        asset.DepreciationMethod = DepreciationMethod.StraightLine;
        asset.UsefulLifeMonths = 60;
        asset.FrequencyMonths = 12;
        asset.AvailableForUseDate = new DateTime(2026, 1, 1);
        asset.GenerateDepreciationSchedule();
        asset.DepreciationSchedule[0].Book(Guid.NewGuid());

        // Disposing exactly on the third period's schedule date should match a fully-booked value.
        var disposalDate = new DateTime(2029, 1, 1);

        asset.SimulateBookValueAtDate(disposalDate).ShouldBe(12000m - 2400m * 3);
    }

    private static Asset CreateAsset(decimal purchaseAmount = 10000m)
    {
        return new Asset(Guid.NewGuid(), Guid.NewGuid(), "AST-001", "Test Asset",
            DateTime.UtcNow, purchaseAmount);
    }
}
