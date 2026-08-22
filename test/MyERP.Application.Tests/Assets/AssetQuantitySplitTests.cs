using System;
using System.Threading.Tasks;
using MyERP.Assets;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for a gap found while closing the "Fixed Asset Splitting on partial sale"
/// migration backlog item: AssetLifecycleManager.SplitAssetAsync and its AppService wrapper
/// (AssetAppService.SplitAsync) were both already fully implemented and unit-tested at the domain
/// level (AssetSplitTests), but CreateAssetDto never exposed AssetQuantity — every asset created
/// through the actual application entry point silently stayed at the entity default of 1, making
/// SplitAsync unreachable in practice (SplitAssetAsync throws whenever splitQty >= AssetQuantity,
/// and no asset could ever have a quantity greater than 1 to split from). Angular's create form and
/// asset-detail page had no UI for either AssetQuantity or Split for the same reason.
/// </summary>
public abstract class AssetQuantitySplitTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_SetsAssetQuantity_ThenSplitAsync_WorksEndToEnd()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var assetAppService = GetRequiredService<IAssetAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Qty Split Test Co"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Asset Series", "Asset", "AST-"), autoSave: true);

            // Act 1: create a single asset record representing 5 identical units.
            var created = await assetAppService.CreateAsync(new CreateAssetDto
            {
                CompanyId = company.Id,
                AssetName = "Laptop Bundle",
                PurchaseDate = DateTime.Today,
                PurchaseAmount = 10000m,
                AssetQuantity = 5,
            });

            // This is the exact gap: pre-fix, CreateAssetDto had no AssetQuantity property at all,
            // so this would always come back as 1 regardless of what was requested.
            created.AssetQuantity.ShouldBe(5);

            await assetAppService.SubmitAsync(created.Id);

            // Act 2: split off 2 of the 5 units — proves the full Create -> Submit -> Split chain
            // now works through the real AppService, not just the domain service directly.
            var splitAsset = await assetAppService.SplitAsync(created.Id, 2);

            splitAsset.AssetQuantity.ShouldBe(2);
            splitAsset.PurchaseAmount.ShouldBe(4000m); // 2/5 of 10000
            splitAsset.SplitFromAssetId.ShouldBe(created.Id);

            var remaining = await assetAppService.GetAsync(created.Id);
            remaining.AssetQuantity.ShouldBe(3);
            remaining.PurchaseAmount.ShouldBe(6000m); // 3/5 of 10000
        });
    }
}
