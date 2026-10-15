using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.CRM;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Assets;

public class AssetDepreciationsBalancesAndContractTermsTests
{
    // =========================================================================
    // ERPNext PR #59919 (commits cdaa83185d & 897cffd9f3): Contract template terms rendering
    // =========================================================================

    [Fact]
    public void ContractTemplate_RenderTerms_RendersSingleLineTermsWithFileExtensionAsText()
    {
        var terms = "Standard terms apply, see https://example.com/terms.html";
        var rendered = ContractTemplateAppService.RenderTerms(terms, new Dictionary<string, string?>());

        rendered.ShouldBe(terms);
    }

    [Fact]
    public void ContractTemplate_RenderTerms_RendersBlankAndMissingFieldsAsEmpty()
    {
        var terms = "From: {{ start_date }}, To: {{ end_date }}";

        // Case 1: end_date is null
        var contextWithNull = new Dictionary<string, string?>
        {
            { "start_date", "2026-10-03" },
            { "end_date", null }
        };
        var rendered1 = ContractTemplateAppService.RenderTerms(terms, contextWithNull);
        rendered1.ShouldBe("From: 2026-10-03, To: ");

        // Case 2: end_date is missing from context
        var contextWithMissing = new Dictionary<string, string?>
        {
            { "start_date", "2026-10-03" }
        };
        var rendered2 = ContractTemplateAppService.RenderTerms(terms, contextWithMissing);
        rendered2.ShouldBe("From: 2026-10-03, To: ");
    }

    // =========================================================================
    // ERPNext PR #59925: Asset Depreciations and Balances Report
    // =========================================================================

    [Fact]
    public async Task AssetReporting_CostColumnsAddUp_WithAValueAdjustment()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var asset = new Asset(assetId, companyId, "AST-001", "MacBook Pro", new DateTime(2020, 1, 1), 100_000m)
        {
            AssetCategoryId = categoryId,
        };
        asset.Submit();

        var adjustment = new AssetValueAdjustment(
            Guid.NewGuid(), "ADJ-001", companyId, assetId,
            new DateTime(2021, 1, 15), 90_000m, 100_000m,
            Guid.NewGuid());
        adjustment.Submit(); // DifferenceAmount = +10,000

        var service = BuildReportingService(new[] { asset }, new[] { adjustment });

        var request = new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2021, 1, 1),
            ToDate = new DateTime(2021, 6, 30),
            GroupBy = "Asset",
            AssetId = assetId,
        };

        var result = await service.GetAssetDepreciationsAndBalancesAsync(request);

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];

        row.ValueAsOnFromDate.ShouldBe(100_000m);
        row.AdjustmentDuringPeriod.ShouldBe(10_000m);
        row.ValueOfSoldAsset.ShouldBe(0m);
        row.ValueOfScrappedAsset.ShouldBe(0m);
        row.ValueAsOnToDate.ShouldBe(110_000m); // 100k + 10k adjustment
    }

    [Fact]
    public async Task AssetReporting_ValueAdjustmentOfDisposedAssetInPeriod_ShownAndNetsToZero()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var asset = new Asset(assetId, companyId, "AST-002", "Dell Server", new DateTime(2020, 1, 1), 100_000m)
        {
            AssetCategoryId = categoryId,
        };
        asset.Submit();

        // Adjustment in period (+10k)
        var adjustment = new AssetValueAdjustment(
            Guid.NewGuid(), "ADJ-002", companyId, assetId,
            new DateTime(2021, 1, 15), 90_000m, 100_000m,
            Guid.NewGuid());
        adjustment.Submit();

        // Scrapped at period end (2021-06-30)
        asset.Scrap(new DateTime(2021, 6, 30));

        var service = BuildReportingService(new[] { asset }, new[] { adjustment });

        var request = new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2021, 1, 1),
            ToDate = new DateTime(2021, 6, 30),
            GroupBy = "Asset",
            AssetId = assetId,
        };

        var result = await service.GetAssetDepreciationsAndBalancesAsync(request);

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];

        row.AdjustmentDuringPeriod.ShouldBe(10_000m);
        // Scrapped value includes the adjustment till disposal: 100k + 10k = 110k
        row.ValueOfScrappedAsset.ShouldBe(110_000m);
        row.ValueAsOnToDate.ShouldBe(0m);
        row.NetAssetValueAsOnToDate.ShouldBe(0m);
    }

    [Fact]
    public async Task AssetReporting_AssetScrappedOnToDate_SubtractedOnce()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var toDate = DateTime.Today;
        var fromDate = toDate.AddDays(-30);

        var asset = new Asset(assetId, companyId, "AST-003", "Office Chair", fromDate.AddDays(-10), 100_000m);
        asset.Submit();
        asset.Scrap(toDate);

        var service = BuildReportingService(new[] { asset }, Array.Empty<AssetValueAdjustment>());

        var request = new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            GroupBy = "Asset",
            AssetId = assetId,
        };

        var result = await service.GetAssetDepreciationsAndBalancesAsync(request);

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];

        row.ValueOfScrappedAsset.ShouldBe(100_000m);
        row.ValueAsOnToDate.ShouldBe(0m);
        row.NetAssetValueAsOnToDate.ShouldBe(0m);
    }

    [Fact]
    public async Task AssetReporting_ExcludesOtherFinanceBooks_WhenSpecificOrNoneSelected()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var book1 = Guid.NewGuid();
        var book2 = Guid.NewGuid();

        var asset = new Asset(assetId, companyId, "AST-004", "CNC Machine", new DateTime(2020, 1, 1), 100_000m);
        asset.DepreciationDetails.Add(new AssetDepreciationDetail(Guid.NewGuid(), assetId, DepreciationMethod.StraightLine, 4, 12, 100_000m) { FinanceBookId = book1 });
        asset.DepreciationDetails.Add(new AssetDepreciationDetail(Guid.NewGuid(), assetId, DepreciationMethod.StraightLine, 2, 12, 100_000m) { FinanceBookId = book2 });
        asset.Submit();

        // Value adjustment specifically for Finance Book 2 (+10k)
        var adjBook2 = new AssetValueAdjustment(
            Guid.NewGuid(), "ADJ-BOOK2", companyId, assetId,
            new DateTime(2021, 1, 15), 50_000m, 60_000m,
            Guid.NewGuid(), financeBookId: book2);
        adjBook2.Submit();

        var service = BuildReportingService(new[] { asset }, new[] { adjBook2 });

        // Query with Book 1 filter: Book 2 adjustment must be excluded
        var resultBook1 = await service.GetAssetDepreciationsAndBalancesAsync(new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2020, 1, 1),
            ToDate = new DateTime(2021, 6, 30),
            GroupBy = "Asset",
            FinanceBookId = book1,
        });

        resultBook1.Rows[0].ValueAsOnToDate.ShouldBe(100_000m);

        // Query with Book 2 filter: Book 2 adjustment is included
        var resultBook2 = await service.GetAssetDepreciationsAndBalancesAsync(new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2020, 1, 1),
            ToDate = new DateTime(2021, 6, 30),
            GroupBy = "Asset",
            FinanceBookId = book2,
        });

        resultBook2.Rows[0].ValueAsOnToDate.ShouldBe(110_000m);
    }

    [Fact]
    public async Task AssetReporting_GroupByAssetCategory_AggregatesCategories()
    {
        var companyId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var asset1 = new Asset(Guid.NewGuid(), companyId, "AST-C1", "Laptop 1", new DateTime(2021, 1, 1), 50_000m) { AssetCategoryId = categoryId };
        var asset2 = new Asset(Guid.NewGuid(), companyId, "AST-C2", "Laptop 2", new DateTime(2021, 2, 1), 60_000m) { AssetCategoryId = categoryId };
        asset1.Submit();
        asset2.Submit();

        var category = new AssetCategory(categoryId, "Computers");

        var service = BuildReportingService(new[] { asset1, asset2 }, Array.Empty<AssetValueAdjustment>(), new[] { category });

        var result = await service.GetAssetDepreciationsAndBalancesAsync(new AssetDepreciationsAndBalancesRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2021, 1, 1),
            ToDate = new DateTime(2021, 12, 31),
            GroupBy = "Asset Category",
            AssetCategoryId = categoryId,
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].AssetCategoryName.ShouldBe("Computers");
        result.Rows[0].ValueOfNewPurchase.ShouldBe(110_000m);
        result.Rows[0].ValueAsOnToDate.ShouldBe(110_000m);
    }

    // =========================================================================
    // ERPNext PR #59928 (commit 59862127b8): Fixed Asset Register Report
    // =========================================================================

    [Fact]
    public void FixedAssetRegister_IsDisposedBy_EvaluatesAgainstEndDate()
    {
        var companyId = Guid.NewGuid();
        var asset = new Asset(Guid.NewGuid(), companyId, "AST-DISP-1", "Disposed Item", new DateTime(2020, 1, 1), 50_000m);
        asset.Submit();

        // Active asset is not disposed
        AssetReportingAppService.IsDisposedBy(asset, new DateTime(2021, 12, 31)).ShouldBeFalse();

        // Disposed on 2021-06-30
        asset.Scrap(new DateTime(2021, 6, 30));

        // When end_date is 2021-12-31 (after disposal) -> disposed
        AssetReportingAppService.IsDisposedBy(asset, new DateTime(2021, 12, 31)).ShouldBeTrue();

        // When end_date is 2021-03-31 (before disposal) -> NOT disposed as of that date
        AssetReportingAppService.IsDisposedBy(asset, new DateTime(2021, 3, 31)).ShouldBeFalse();

        // When no end_date is specified -> disposed
        AssetReportingAppService.IsDisposedBy(asset, null).ShouldBeTrue();
    }

    [Fact]
    public void FixedAssetRegister_CalculateAssetValue_CarriesZeroIfDisposedByEndDate_ElseCarriesValue()
    {
        var companyId = Guid.NewGuid();
        var asset = new Asset(Guid.NewGuid(), companyId, "AST-VAL-1", "Machine", new DateTime(2020, 1, 1), 100_000m)
        {
            OpeningAccumulatedDepreciation = 10_000m
        };
        asset.Submit();

        // When active: 100k - 10k opening - 20k depr + 5k reval = 75k
        var activeValue = AssetReportingAppService.CalculateAssetValue(asset, 20_000m, 5_000m, new DateTime(2021, 12, 31));
        activeValue.ShouldBe(75_000m);

        // Disposed on 2021-06-30
        asset.Scrap(new DateTime(2021, 6, 30));

        // As of 2021-12-31 (after disposal) -> carries 0 value
        var disposedValue = AssetReportingAppService.CalculateAssetValue(asset, 20_000m, 5_000m, new DateTime(2021, 12, 31));
        disposedValue.ShouldBe(0m);

        // As of 2021-03-31 (before disposal) -> carries computed carrying value as of that date
        var priorPeriodValue = AssetReportingAppService.CalculateAssetValue(asset, 20_000m, 5_000m, new DateTime(2021, 3, 31));
        priorPeriodValue.ShouldBe(75_000m);
    }

    [Fact]
    public async Task FixedAssetRegister_FromDateGreaterThanToDate_ThrowsDateRangeInvalid()
    {
        var companyId = Guid.NewGuid();
        var service = BuildReportingService(Array.Empty<Asset>(), Array.Empty<AssetValueAdjustment>());

        var request = new FixedAssetRegisterRequestDto
        {
            CompanyId = companyId,
            FilterBasedOn = "Date Range",
            FromDate = new DateTime(2021, 12, 31),
            ToDate = new DateTime(2021, 1, 1),
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => service.GetFixedAssetRegisterAsync(request));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.DateRangeInvalid);
    }

    [Fact]
    public async Task FixedAssetRegister_IncludeDefaultBookAndDifferentBook_ThrowsValidationFailed()
    {
        var companyId = Guid.NewGuid();
        var defaultBookId = Guid.NewGuid();
        var otherBookId = Guid.NewGuid();

        var defaultBook = new FinanceBook(defaultBookId, companyId, "Default Book") { IsDefault = true };
        var otherBook = new FinanceBook(otherBookId, companyId, "Tax Book") { IsDefault = false };

        var service = BuildReportingService(
            Array.Empty<Asset>(),
            Array.Empty<AssetValueAdjustment>(),
            books: new[] { defaultBook, otherBook });

        var request = new FixedAssetRegisterRequestDto
        {
            CompanyId = companyId,
            IncludeDefaultBookAssets = true,
            FinanceBookId = otherBookId,
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => service.GetFixedAssetRegisterAsync(request));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("please uncheck 'Include Default FB Assets'");
    }

    [Fact]
    public async Task FixedAssetRegister_GeneratesReportWithAccurateValuesAndGrouping()
    {
        var companyId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new AssetCategory(categoryId, "Vehicles");

        var asset1 = new Asset(Guid.NewGuid(), companyId, "AST-V1", "Truck 1", new DateTime(2020, 1, 1), 80_000m)
        {
            AssetCategoryId = categoryId,
            OpeningAccumulatedDepreciation = 10_000m,
        };
        asset1.Submit();

        var asset2 = new Asset(Guid.NewGuid(), companyId, "AST-V2", "Van 2", new DateTime(2020, 1, 1), 40_000m)
        {
            AssetCategoryId = categoryId,
            OpeningAccumulatedDepreciation = 5_000m,
        };
        asset2.Submit();

        var service = BuildReportingService(
            new[] { asset1, asset2 },
            Array.Empty<AssetValueAdjustment>(),
            categories: new[] { category });

        var request = new FixedAssetRegisterRequestDto
        {
            CompanyId = companyId,
            GroupBy = "Asset Category",
            AssetCategoryId = categoryId,
        };

        var result = await service.GetFixedAssetRegisterAsync(request);

        result.Rows.Count.ShouldBe(1);
        var groupRow = result.Rows[0];
        groupRow.AssetCategoryName.ShouldBe("Vehicles");
        groupRow.GrossPurchaseAmount.ShouldBe(120_000m); // 80k + 40k
        groupRow.OpeningAccumulatedDepreciation.ShouldBe(15_000m); // 10k + 5k
        groupRow.AssetValue.ShouldBe(105_000m); // 120k - 15k
    }

    private static AssetReportingAppService BuildReportingService(
        IEnumerable<Asset> assets,
        IEnumerable<AssetValueAdjustment> adjustments,
        IEnumerable<AssetCategory>? categories = null,
        IEnumerable<FinanceBook>? books = null)
    {
        var assetList = assets.ToList();
        var adjList = adjustments.ToList();
        var catList = categories?.ToList() ?? new List<AssetCategory>();
        var bookList = books?.ToList() ?? new List<FinanceBook>();

        var assetRepo = Substitute.For<IRepository<Asset, Guid>>();
        assetRepo.WithDetailsAsync(Arg.Any<Expression<Func<Asset, object>>[]>())
            .Returns(Task.FromResult(assetList.AsQueryable()));

        var catRepo = Substitute.For<IRepository<AssetCategory, Guid>>();
        catRepo.GetListAsync().Returns(Task.FromResult(catList));

        var adjRepo = Substitute.For<IRepository<AssetValueAdjustment, Guid>>();
        adjRepo.GetQueryableAsync().Returns(Task.FromResult(adjList.AsQueryable()));

        var bookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();
        bookRepo.FindAsync(Arg.Any<Expression<Func<FinanceBook, bool>>>())
            .Returns(call =>
            {
                var predicate = call.Arg<Expression<Func<FinanceBook, bool>>>().Compile();
                return Task.FromResult(bookList.FirstOrDefault(predicate));
            });
        bookRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<FinanceBook, bool>>>())
            .Returns(call =>
            {
                var predicate = call.Arg<Expression<Func<FinanceBook, bool>>>().Compile();
                return Task.FromResult(bookList.FirstOrDefault(predicate));
            });
        bookRepo.GetQueryableAsync()
            .Returns(Task.FromResult(bookList.AsQueryable()));

        var capRepo = Substitute.For<IRepository<AssetCapitalization, Guid>>();
        capRepo.WithDetailsAsync(Arg.Any<Expression<Func<AssetCapitalization, object>>[]>())
            .Returns(Task.FromResult(new List<AssetCapitalization>().AsQueryable()));

        return new AssetReportingAppService(assetRepo, catRepo, adjRepo, bookRepo, capRepo);
    }
}
