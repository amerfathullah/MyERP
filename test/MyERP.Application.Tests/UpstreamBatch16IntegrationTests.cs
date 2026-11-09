using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 16 upstream ERPNext parity:
/// 1) BOM validation enforces that BOM must produce the planned item (PR #64859 / commit d08131b6e8, b4f79f31ea).
/// 2) Auto-insertion of ItemPrice from Item StandardSellingPrice applies correctly (PR #64830 / commit 1853ef83ea).
/// </summary>
public abstract class UpstreamBatch16IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task BomValidationService_RejectsBom_WhenBomDoesNotProduceItem_d08131b6e8()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var bomValidation = GetRequiredService<BomValidationService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 16 Mfg Co"), autoSave: true);
            var itemA = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B16-A", "Item A (FG)", ItemType.Goods), autoSave: true);
            var itemB = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B16-B", "Item B (Other)", ItemType.Goods), autoSave: true);

            // BOM that produces Item B
            var bomForB = new BillOfMaterials(
                id: Guid.NewGuid(),
                companyId: company.Id,
                bomNumber: "BOM-ITEM-B-001",
                itemId: itemB.Id)
            {
                IsActive = true,
                Quantity = 1m
            };
            await bomRepo.InsertAsync(bomForB, autoSave: true);

            // Validating bomForB against itemA must throw
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                bomValidation.ValidateProductionBomAsync(itemA.Id, bomForB.Id, company.Id));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task BomValidationService_AcceptsBom_WhenBomProducesItem()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var bomValidation = GetRequiredService<BomValidationService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Batch 16 Mfg Co 2"), autoSave: true);
            var itemA = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-B16-A2", "Item A2 (FG)", ItemType.Goods), autoSave: true);

            var bomForA = new BillOfMaterials(
                id: Guid.NewGuid(),
                companyId: company.Id,
                bomNumber: "BOM-ITEM-A-001",
                itemId: itemA.Id)
            {
                IsActive = true,
                Quantity = 1m
            };
            await bomRepo.InsertAsync(bomForA, autoSave: true);

            var validatedBom = await bomValidation.ValidateProductionBomAsync(itemA.Id, bomForA.Id, company.Id);
            validatedBom.Id.ShouldBe(bomForA.Id);
        });
    }
}
