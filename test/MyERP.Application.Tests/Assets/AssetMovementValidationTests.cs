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

    [Fact]
    public async Task SubmitAsync_DuplicateAssetInMultipleRows_ThrowsValidationFailed()
    {
        // Per ERPNext PR #60019 (commit 23adc7295b): Refuse the same asset in more than one row
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Dup Co"), autoSave: true);
            var sourceLoc = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Dup Source"), autoSave: true);
            var targetLoc = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Dup Target"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-DUP-001", "AssetMove Dup Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = sourceLoc.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, SourceLocationId = sourceLoc.Id, TargetLocationId = targetLoc.Id },
                    new() { AssetId = asset.Id, SourceLocationId = sourceLoc.Id, TargetLocationId = targetLoc.Id }
                }
            });

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.SubmitAsync(created.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]?.ToString().ShouldNotBeNull().ShouldContain("is already added in another row");
        });
    }

    [Fact]
    public async Task SubmitAsync_AssetInDraftOrScrappedStatus_ThrowsAssetCannotBeMoved()
    {
        // Per ERPNext PR #60019 / commit 6b8511def7: Refuse moving assets in Draft, Scrapped, Sold, Capitalized status
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Status Co"), autoSave: true);
            var sourceLoc = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Status Source"), autoSave: true);
            var targetLoc = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Status Target"), autoSave: true);

            // Asset in Draft status (not submitted)
            var assetDraft = new Asset(Guid.NewGuid(), company.Id, "AST-DRAFT-001", "Draft Asset", DateTime.UtcNow, 1000m);
            assetDraft.LocationId = sourceLoc.Id;
            await assetRepository.InsertAsync(assetDraft, autoSave: true);

            var createdDraft = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = assetDraft.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = assetDraft.Id, SourceLocationId = sourceLoc.Id, TargetLocationId = targetLoc.Id }
                }
            });

            var exDraft = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.SubmitAsync(createdDraft.Id));
            exDraft.Code.ShouldBe(MyERPDomainErrorCodes.AssetCannotBeMoved);

            // Asset in Scrapped status
            var assetScrapped = new Asset(Guid.NewGuid(), company.Id, "AST-SCRAP-001", "Scrapped Asset", DateTime.UtcNow, 1000m);
            assetScrapped.Submit();
            assetScrapped.Scrap(DateTime.UtcNow);
            assetScrapped.LocationId = sourceLoc.Id;
            await assetRepository.InsertAsync(assetScrapped, autoSave: true);

            var createdScrap = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = assetScrapped.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = assetScrapped.Id, SourceLocationId = sourceLoc.Id, TargetLocationId = targetLoc.Id }
                }
            });

            var exScrap = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.SubmitAsync(createdScrap.Id));
            exScrap.Code.ShouldBe(MyERPDomainErrorCodes.AssetCannotBeMoved);
        });
    }

    [Fact]
    public async Task SubmitAsync_TransferOnly_UpdatesLocationLeavesCustodianIntact()
    {
        // Per ERPNext PR #60019 / commits f3d11a89fc, 1073df2a15: Transfer only changes location, never wipes custodian
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Sep Co"), autoSave: true);
            var loc1 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Sep Loc 1"), autoSave: true);
            var loc2 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Sep Loc 2"), autoSave: true);
            var emp = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-SEP-1", "Sep Custodian"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-SEP-001", "Sep Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = loc1.Id;
            asset.CustodianEmployeeId = emp.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, SourceLocationId = loc1.Id, TargetLocationId = loc2.Id }
                }
            });

            await assetMovementAppService.SubmitAsync(created.Id);

            var updatedAsset = await assetRepository.GetAsync(asset.Id);
            updatedAsset.LocationId.ShouldBe(loc2.Id);
            updatedAsset.CustodianEmployeeId.ShouldBe(emp.Id); // Untouched!
        });
    }

    [Fact]
    public async Task SubmitAsync_IssueOnly_UpdatesCustodianLeavesLocationIntact()
    {
        // Per ERPNext PR #60019 / commits f3d11a89fc, 1073df2a15: Issue only changes custodian, never wipes location
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Issue Co"), autoSave: true);
            var loc = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Issue Loc"), autoSave: true);
            var emp = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-ISS-1", "Issue Custodian"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-ISS-001", "Issue Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = loc.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Issue,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, ToEmployeeId = emp.Id }
                }
            });

            await assetMovementAppService.SubmitAsync(created.Id);

            var updatedAsset = await assetRepository.GetAsync(asset.Id);
            updatedAsset.LocationId.ShouldBe(loc.Id); // Untouched!
            updatedAsset.CustodianEmployeeId.ShouldBe(emp.Id);
        });
    }

    [Fact]
    public async Task CancelAsync_RevertsLocationAndCustodian()
    {
        // Per ERPNext PR #60019: Cancelling movement reverts to previous location/custodian
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Cancel Co"), autoSave: true);
            var loc1 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Cancel Loc 1"), autoSave: true);
            var loc2 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Cancel Loc 2"), autoSave: true);
            var loc3 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Cancel Loc 3"), autoSave: true);
            var emp1 = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-CNC-1", "Cancel Custodian 1"), autoSave: true);
            var emp2 = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-CNC-2", "Cancel Custodian 2"), autoSave: true);
            var emp3 = await employeeRepository.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-CNC-3", "Cancel Custodian 3"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-CNC-001", "Cancel Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = loc1.Id;
            asset.CustodianEmployeeId = emp1.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            // Movement 1: transfer & issue to loc2 & emp2
            var mov1 = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.TransferAndIssue,
                AssetId = asset.Id,
                TransactionDate = DateTime.UtcNow.AddHours(-2),
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new()
                    {
                        AssetId = asset.Id,
                        SourceLocationId = loc1.Id,
                        TargetLocationId = loc2.Id,
                        FromEmployeeId = emp1.Id,
                        ToEmployeeId = emp2.Id
                    }
                }
            });
            await assetMovementAppService.SubmitAsync(mov1.Id);

            // Movement 2: transfer & issue to loc3 & emp3
            var mov2 = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.TransferAndIssue,
                AssetId = asset.Id,
                TransactionDate = DateTime.UtcNow.AddHours(-1),
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new()
                    {
                        AssetId = asset.Id,
                        SourceLocationId = loc2.Id,
                        TargetLocationId = loc3.Id,
                        FromEmployeeId = emp2.Id,
                        ToEmployeeId = emp3.Id
                    }
                }
            });
            await assetMovementAppService.SubmitAsync(mov2.Id);

            var movedAsset = await assetRepository.GetAsync(asset.Id);
            movedAsset.LocationId.ShouldBe(loc3.Id);
            movedAsset.CustodianEmployeeId.ShouldBe(emp3.Id);

            // Cancel Movement 2 -> should revert back to Movement 1 values (loc2, emp2)
            await assetMovementAppService.CancelAsync(mov2.Id);

            var revertedAsset = await assetRepository.GetAsync(asset.Id);
            revertedAsset.LocationId.ShouldBe(loc2.Id);
            revertedAsset.CustodianEmployeeId.ShouldBe(emp2.Id);
        });
    }

    [Fact]
    public async Task CancelAsync_OnlyMovementOfActiveAsset_ThrowsValidationFailed()
    {
        // Per ERPNext PR #47291: cannot cancel sole movement of active asset
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var assetRepository = GetRequiredService<IRepository<Asset, Guid>>();
            var locationRepository = GetRequiredService<IRepository<Location, Guid>>();
            var assetMovementAppService = GetRequiredService<IAssetMovementAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "AssetMove Sole Co"), autoSave: true);
            var loc1 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Sole Loc 1"), autoSave: true);
            var loc2 = await locationRepository.InsertAsync(new Location(Guid.NewGuid(), "Sole Loc 2"), autoSave: true);

            var asset = new Asset(Guid.NewGuid(), company.Id, "AST-SOLE-001", "Sole Asset", DateTime.UtcNow, 2000m);
            asset.Submit();
            asset.LocationId = loc1.Id;
            await assetRepository.InsertAsync(asset, autoSave: true);

            var created = await assetMovementAppService.CreateAsync(new CreateUpdateAssetMovementDto
            {
                CompanyId = company.Id,
                Purpose = AssetMovementPurpose.Transfer,
                AssetId = asset.Id,
                Items = new List<CreateUpdateAssetMovementItemDto>
                {
                    new() { AssetId = asset.Id, SourceLocationId = loc1.Id, TargetLocationId = loc2.Id }
                }
            });
            await assetMovementAppService.SubmitAsync(created.Id);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => assetMovementAppService.CancelAsync(created.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]?.ToString().ShouldNotBeNull().ShouldContain("has only one movement record");
        });
    }
}
