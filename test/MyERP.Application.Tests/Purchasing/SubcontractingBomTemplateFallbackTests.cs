using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for ERPNext PR #59373 (commit 87113d7c2c):
/// 1. Finished good variant items fall back to their template item's BOM when no BOM is explicitly specified.
/// 2. Validates that a specified BOM belongs to either the finished good or its template item (throws if unrelated).
/// </summary>
public abstract class SubcontractingBomTemplateFallbackTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateOrderAsync_VariantWithoutBom_UsesTemplateBom()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var scoAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCO Variant Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCO Variant Supplier"), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SCO Series", "SCO", "SCO-.####"), autoSave: true);

            var templateItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-FG-TEMPLATE", "SCO FG Template", ItemType.Goods)
                {
                    HasVariants = true
                }, autoSave: true);

            var rawMaterial = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-RM-1", "SCO RM 1", ItemType.Goods), autoSave: true);

            var templateBom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SCO-TMPL", templateItem.Id)
            {
                Quantity = 1,
                IsActive = true,
                IsDefault = true
            };
            templateBom.Items.Add(new Manufacturing.Entities.BomItem(Guid.NewGuid(), templateBom.Id, rawMaterial.Id, "RM Item", 2, 5));
            await bomRepo.InsertAsync(templateBom, autoSave: true);

            templateItem.DefaultBomId = templateBom.Id;
            await itemRepo.UpdateAsync(templateItem, autoSave: true);

            var variantItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-FG-VARIANT-RED", "SCO FG Variant Red", ItemType.Goods)
                {
                    VariantOfId = templateItem.Id
                }, autoSave: true);

            var scoDto = await scoAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow,
                Items = new List<CreateScoItemDto>
                {
                    new() { ItemId = variantItem.Id, ItemName = variantItem.ItemName, Qty = 5m, Rate = 100m }
                }
            });

            scoDto.ShouldNotBeNull();
            scoDto.Items.Count.ShouldBe(1);
            scoDto.Items[0].BomId.ShouldBe(templateBom.Id);
        });
    }

    [Fact]
    public async Task CreateOrderAsync_UnrelatedBomSpecified_ThrowsValidationFailed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            var scoAppService = GetRequiredService<ISubcontractingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCO Unrelated Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCO Unrelated Supplier"), autoSave: true);

            var unrelatedItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-UNRELATED-ITEM", "Unrelated Item", ItemType.Goods), autoSave: true);
            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-FG-ITEM", "FG Item", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCO-RM-2", "RM Item 2", ItemType.Goods), autoSave: true);

            var unrelatedBom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-UNRELATED", unrelatedItem.Id)
            {
                Quantity = 1,
                IsActive = true
            };
            unrelatedBom.Items.Add(new Manufacturing.Entities.BomItem(Guid.NewGuid(), unrelatedBom.Id, rmItem.Id, "RM Item 2", 1, 10));
            await bomRepo.InsertAsync(unrelatedBom, autoSave: true);

            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scoAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = fgItem.Id, ItemName = fgItem.ItemName, Qty = 1m, Rate = 50m, BomId = unrelatedBom.Id }
                    }
                }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
            ex.Data["detail"]?.ToString().ShouldNotBeNull().ShouldContain("does not belong to Item");
        });
    }

    [Fact]
    public async Task CreateInwardOrderAsync_VariantWithoutBom_UsesTemplateBom()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<Manufacturing.Entities.BillOfMaterials, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SCIO Variant Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SCIO Variant Supplier"), autoSave: true);

            var templateItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCIO-FG-TMPL", "SCIO FG Template", ItemType.Goods)
                {
                    HasVariants = true
                }, autoSave: true);

            var rawMaterial = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCIO-RM-1", "SCIO RM 1", ItemType.Goods), autoSave: true);

            var templateBom = new Manufacturing.Entities.BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-SCIO-TMPL", templateItem.Id)
            {
                Quantity = 1,
                IsActive = true,
                IsDefault = true
            };
            templateBom.Items.Add(new Manufacturing.Entities.BomItem(Guid.NewGuid(), templateBom.Id, rawMaterial.Id, "RM Item", 1, 10));
            await bomRepo.InsertAsync(templateBom, autoSave: true);

            templateItem.DefaultBomId = templateBom.Id;
            await itemRepo.UpdateAsync(templateItem, autoSave: true);

            var variantItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SCIO-FG-VAR-BLUE", "SCIO FG Variant Blue", ItemType.Goods)
                {
                    VariantOfId = templateItem.Id
                }, autoSave: true);

            var scioDto = await scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                OrderDate = DateTime.UtcNow,
                Items = new List<CreateScioItemDto>
                {
                    new() { ItemId = variantItem.Id, Quantity = 3m, Rate = 80m }
                }
            });

            scioDto.ShouldNotBeNull();
            scioDto.Items.Count.ShouldBe(1);
            scioDto.Items[0].BomId.ShouldBe(templateBom.Id);
        });
    }
}
