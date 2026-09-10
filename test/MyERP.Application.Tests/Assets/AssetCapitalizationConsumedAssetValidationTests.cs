using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: asset_capitalization.py
/// validate_consumed_asset_item() blocks a consumed asset that is Draft (no real value/depreciation
/// schedule yet) or already Capitalized (already consumed into a different target — allowing it
/// again would capitalize the same underlying value twice). AssetCapitalizationAppService.CreateAsync
/// only rejected Sold/Scrapped, leaving both of these reachable.
/// </summary>
public abstract class AssetCapitalizationConsumedAssetValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ConsumedAssetIsDraft_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var capAppService = GetRequiredService<IAssetCapitalizationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Cap Draft Co"), autoSave: true);

            var targetAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-001", "Asset Cap Target 1", DateTime.UtcNow, 10000m);
            targetAsset.Submit();
            await assetRepository.InsertAsync(targetAsset, autoSave: true);

            var draftConsumedAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-002", "Asset Cap Draft Consumed", DateTime.UtcNow, 500m);
            // Deliberately left in Draft — never submitted.
            await assetRepository.InsertAsync(draftConsumedAsset, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                capAppService.CreateAsync(new CreateUpdateAssetCapitalizationDto
                {
                    CompanyId = company.Id,
                    TargetAssetId = targetAsset.Id,
                    ConsumedAssets = new List<CreateUpdateAssetCapitalizationAssetItemDto>
                    {
                        new() { AssetId = draftConsumedAsset.Id, AssetName = draftConsumedAsset.AssetName, CurrentValue = 500m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ConsumedAssetAlreadyCapitalized_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var capAppService = GetRequiredService<IAssetCapitalizationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Cap Already Co"), autoSave: true);

            var targetAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-003", "Asset Cap Target 2", DateTime.UtcNow, 10000m);
            targetAsset.Submit();
            await assetRepository.InsertAsync(targetAsset, autoSave: true);

            var alreadyCapitalizedAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-004", "Asset Cap Already Consumed", DateTime.UtcNow, 800m);
            alreadyCapitalizedAsset.Submit();
            alreadyCapitalizedAsset.MarkAsCapitalized(DateTime.UtcNow);
            await assetRepository.InsertAsync(alreadyCapitalizedAsset, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                capAppService.CreateAsync(new CreateUpdateAssetCapitalizationDto
                {
                    CompanyId = company.Id,
                    TargetAssetId = targetAsset.Id,
                    ConsumedAssets = new List<CreateUpdateAssetCapitalizationAssetItemDto>
                    {
                        new() { AssetId = alreadyCapitalizedAsset.Id, AssetName = alreadyCapitalizedAsset.AssetName, CurrentValue = 800m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ConsumedAssetSubmitted_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var capAppService = GetRequiredService<IAssetCapitalizationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Asset Cap Happy Co"), autoSave: true);

            var targetAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-005", "Asset Cap Target 3", DateTime.UtcNow, 10000m);
            targetAsset.Submit();
            await assetRepository.InsertAsync(targetAsset, autoSave: true);

            var consumedAsset = new Asset(Guid.NewGuid(), company.Id, "AST-CAP-006", "Asset Cap Happy Consumed", DateTime.UtcNow, 600m);
            consumedAsset.Submit();
            await assetRepository.InsertAsync(consumedAsset, autoSave: true);

            var dto = await capAppService.CreateAsync(new CreateUpdateAssetCapitalizationDto
            {
                CompanyId = company.Id,
                TargetAssetId = targetAsset.Id,
                ConsumedAssets = new List<CreateUpdateAssetCapitalizationAssetItemDto>
                {
                    new() { AssetId = consumedAsset.Id, AssetName = consumedAsset.AssetName, CurrentValue = 600m }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
