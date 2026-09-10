using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Assets;

/// <summary>
/// Regression coverage for real gaps found via ERPNext validate() parity: asset_movement.py
/// validate_location()/validate_employee() reject a claimed source location/custodian that doesn't
/// match the asset's actual current values, and require the receiving employee to belong to the
/// movement's own company. AssetMovementAppService.SubmitAsync trusted these fields entirely from
/// client input, silently corrupting the location/custodian audit trail this document exists to keep.
/// </summary>
public abstract class AssetMovementValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_SourceLocationDoesNotMatchAssetCurrentLocation_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Guard Co"), autoSave: true);
            var actualLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Actual Location"), autoSave: true);
            var claimedLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Claimed Wrong Location"), autoSave: true);
            var targetLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Target Location"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-MOVE-001", "AssetMove Guard Asset", DateTime.UtcNow, 5000m);
            asset.Submit();
            asset.LocationId = actualLocation.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, SourceLocationId = claimedLocation.Id, TargetLocationId = targetLocation.Id }
                }
            });

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.SubmitAsync(created.Id));
        });
    }

    [Fact]
    public async Task SubmitAsync_ToEmployeeFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Emp Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Emp Other Co"), autoSave: true);
            var targetLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "AssetMove Emp Target Location"), autoSave: true);
            var crossCoEmployee = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), otherCompany.Id, "EMP-AM-1", "Cross Co Employee"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), ownerCompany.Id, "AST-MOVE-002", "AssetMove Emp Guard Asset", DateTime.UtcNow, 3000m);
            asset.Submit();
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = ownerCompany.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, TargetLocationId = targetLocation.Id, ToEmployeeId = crossCoEmployee.Id }
                }
            });

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.SubmitAsync(created.Id));
        });
    }

    [Fact]
    public async Task SubmitAsync_ValidMovement_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Happy Co"), autoSave: true);
            var sourceLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "AssetMove Happy Source"), autoSave: true);
            var targetLocation = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "AssetMove Happy Target"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-MOVE-003", "AssetMove Happy Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = sourceLocation.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, SourceLocationId = sourceLocation.Id, TargetLocationId = targetLocation.Id }
                }
            });

            var submitted = await assetMovementAppService.SubmitAsync(created.Id);
            submitted.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
