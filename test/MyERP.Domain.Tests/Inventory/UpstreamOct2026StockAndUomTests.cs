using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class UpstreamOct2026StockAndUomTests
{
    // --- ERPNext PR #59796: Disallow same source and target warehouse on Stock Entry Submit ---

    [Fact]
    public void StockEntry_Submit_Throws_SameWarehouseTransfer_WhenSourceAndTargetMatch()
    {
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5m, sourceWarehouseId: warehouseId, targetWarehouseId: warehouseId);

        var ex = Should.Throw<BusinessException>(() => entry.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.SameWarehouseTransfer);
    }

    [Fact]
    public void StockEntry_Submit_Succeeds_WhenSourceAndTargetWarehousesDiffer()
    {
        var companyId = Guid.NewGuid();
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialTransfer, DateTime.UtcNow);
        entry.AddItem(itemId, 5m, sourceWarehouseId: sourceWh, targetWarehouseId: targetWh);

        entry.Submit();
        entry.Status.ShouldBe(DocumentStatus.Submitted);
    }

    // --- ERPNext PR #59754: Enforce UOM restrictions when AllowUomWithConversionRateDefinedInItem is active ---

    [Fact]
    public async Task UomConversionService_ValidateItemUomsAsync_Skips_WhenRestrictionDisabled()
    {
        var repo = Substitute.For<IRepository<UomConversion, Guid>>();
        var service = new UomConversionService(repo);

        var companyId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), companyId, "ITEM-01", "Item 01", ItemType.Goods)
        {
            Uom = "Nos"
        };

        // Custom UOM not configured, but restriction is disabled => should not throw
        await service.ValidateItemUomsAsync(new (Item item, string? uom)[] { (item, "Box") }, isRestrictionEnabled: false);
    }

    [Fact]
    public async Task UomConversionService_ValidateItemUomsAsync_Succeeds_WhenUomMatchesStockUom()
    {
        var repo = Substitute.For<IRepository<UomConversion, Guid>>();
        var service = new UomConversionService(repo);

        var companyId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), companyId, "ITEM-01", "Item 01", ItemType.Goods)
        {
            Uom = "Nos"
        };

        // Same UOM (case-insensitive) => should not throw even if restriction is active
        await service.ValidateItemUomsAsync(new (Item item, string? uom)[] { (item, "nos") }, isRestrictionEnabled: true);
    }

    [Fact]
    public async Task UomConversionService_ValidateItemUomsAsync_Succeeds_WhenItemConversionExists()
    {
        var repo = Substitute.For<IRepository<UomConversion, Guid>>();
        var service = new UomConversionService(repo);

        var companyId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), companyId, "ITEM-01", "Item 01", ItemType.Goods)
        {
            Uom = "Nos"
        };

        var conversions = new List<UomConversion>
        {
            new(Guid.NewGuid(), "Box", "Nos", 12m, itemId: item.Id)
        };
        repo.GetQueryableAsync().Returns(Task.FromResult(conversions.AsQueryable()));

        await service.ValidateItemUomsAsync(new (Item item, string? uom)[] { (item, "Box") }, isRestrictionEnabled: true);
    }

    [Fact]
    public async Task UomConversionService_ValidateItemUomsAsync_Succeeds_WhenVariantTemplateConversionExists()
    {
        var repo = Substitute.For<IRepository<UomConversion, Guid>>();
        var service = new UomConversionService(repo);

        var companyId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var variantItem = new Item(Guid.NewGuid(), companyId, "SHIRT-RED", "Red Shirt", ItemType.Goods)
        {
            Uom = "Nos",
            VariantOfId = templateId
        };

        // Conversion defined on template item, not on variant directly
        var conversions = new List<UomConversion>
        {
            new(Guid.NewGuid(), "Pack", "Nos", 6m, itemId: templateId)
        };
        repo.GetQueryableAsync().Returns(Task.FromResult(conversions.AsQueryable()));

        await service.ValidateItemUomsAsync(new (Item item, string? uom)[] { (variantItem, "Pack") }, isRestrictionEnabled: true);
    }

    [Fact]
    public async Task UomConversionService_ValidateItemUomsAsync_Throws_WhenUomNotConfigured()
    {
        var repo = Substitute.For<IRepository<UomConversion, Guid>>();
        var service = new UomConversionService(repo);

        var companyId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), companyId, "ITEM-01", "Item 01", ItemType.Goods)
        {
            Uom = "Nos"
        };

        var conversions = new List<UomConversion>();
        repo.GetQueryableAsync().Returns(Task.FromResult(conversions.AsQueryable()));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateItemUomsAsync(new (Item item, string? uom)[] { (item, "Crate") }, isRestrictionEnabled: true));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.UomNotConfiguredForItem);
    }

    [Fact]
    public async Task ItemTransactionValidationService_ValidateItemUomsForTransactionAsync_EnforcesRestriction()
    {
        var companyId = Guid.NewGuid();
        var company = new Company(companyId, "Test Company")
        {
            AllowUomWithConversionRateDefinedInItem = true
        };

        var item = new Item(Guid.NewGuid(), companyId, "ITEM-01", "Item 01", ItemType.Goods)
        {
            Uom = "Nos"
        };

        var companyRepo = Substitute.For<IRepository<Company, Guid>>();
        companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        var uomConversionRepo = Substitute.For<IRepository<UomConversion, Guid>>();
        uomConversionRepo.GetQueryableAsync().Returns(Task.FromResult(new List<UomConversion>().AsQueryable()));
        var uomService = new UomConversionService(uomConversionRepo);

        var validationService = new ItemTransactionValidationService(
            itemRepo,
            companyRepository: companyRepo,
            uomConversionService: uomService);

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await validationService.ValidateItemUomsForTransactionAsync(
                companyId,
                new[] { (item.Id, (string?)"UnknownUom") }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.UomNotConfiguredForItem);
    }

    // --- ERPNext PR #59840: Company Restriction on Item Standard Cost ---

    [Fact]
    public async Task CompanyRestrictionValidationService_Blocks_Item_WhenCompanyNotAllowed()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var item = new Item(itemId, companyA, "ITEM-RESTR", "Restricted Item", ItemType.Goods)
        {
            RestrictToCompanies = true
        };

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var allowedEntries = new List<CompanyRestrictionEntry>
        {
            new(Guid.NewGuid(), "Item", itemId, companyA)
        };
        restrictionRepo.GetQueryableAsync().Returns(Task.FromResult(allowedEntries.AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var accountRepo = Substitute.For<IRepository<Account, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var service = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, customerRepo, supplierRepo, accountRepo, warehouseRepo);

        // Transaction in Company B must fail because Item is restricted to Company A
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateTransactionCompanyAsync(
                "ItemStandardCost",
                companyB,
                itemIds: new[] { itemId }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyRestrictionBlocked);
    }
}
