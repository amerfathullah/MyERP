using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

public class BomSearchAppServiceTests
{
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<ProductBundle, Guid> _bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly BomSearchAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public BomSearchAppServiceTests()
    {
        _appService = new BomSearchAppService(
            _bomRepo,
            _bundleRepo,
            _itemRepo,
            _companyRepo);
    }

    [Fact]
    public async Task SearchAsync_EmptySearchItems_ReturnsEmptyReport()
    {
        // Act
        var result = await _appService.SearchAsync(new BomSearchFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalMatches.ShouldBe(0);
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_DirectBomItem_MatchesContainedItem()
    {
        // Arrange (corresponds to ERPNext test_bom_found_by_contained_item)
        var fgItemId = Guid.NewGuid();
        var rawMaterialId = Guid.NewGuid();

        var fgItem = new Item(fgItemId, _companyId, "ITEM-FG", "Finished Laptop", ItemType.Goods);
        var rmItem = new Item(rawMaterialId, _companyId, "ITEM-RM", "Motherboard", ItemType.Goods);
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }));

        var company = new Company(_companyId, "Acme Corp") { CurrencyCode = "USD" };
        _companyRepo.GetListAsync().Returns(Task.FromResult(new List<Company> { company }));

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-LAPTOP-001", fgItemId)
        {
            Quantity = 1m,
            IsActive = true,
            TotalMaterialCost = 450m
        };
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rawMaterialId, "Motherboard", 1m, 450m));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));
        _bundleRepo.WithDetailsAsync()
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        // Act
        var result = await _appService.SearchAsync(new BomSearchFilterDto
        {
            CompanyId = _companyId,
            Item1Id = rawMaterialId,
            SearchSubAssemblies = false
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalMatches.ShouldBe(1);
        result.BomMatchesCount.ShouldBe(1);
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.DocType.ShouldBe("BOM");
        row.ParentName.ShouldBe("BOM-LAPTOP-001");
        row.ItemCode.ShouldBe("ITEM-FG");
        row.FoundInSubAssembly.ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_SubAssemblies_FindsTopLevelBomWhenEnabled()
    {
        // Arrange (corresponds to ERPNext test_search_sub_assemblies_finds_top_level_bom)
        var topFgItemId = Guid.NewGuid();
        var subAssemblyItemId = Guid.NewGuid();
        var rawMaterialId = Guid.NewGuid();

        var topFgItem = new Item(topFgItemId, _companyId, "ITEM-FG-CAR", "Automobile", ItemType.Goods);
        var saItem = new Item(subAssemblyItemId, _companyId, "ITEM-SA-ENGINE", "Engine Assembly", ItemType.Goods);
        var rmItem = new Item(rawMaterialId, _companyId, "ITEM-RM-PISTON", "Piston Ring", ItemType.Goods);
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { topFgItem, saItem, rmItem }));

        var company = new Company(_companyId, "Motor Corp") { CurrencyCode = "USD" };
        _companyRepo.GetListAsync().Returns(Task.FromResult(new List<Company> { company }));

        // Sub-BOM: Engine contains Piston
        var subBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-ENGINE-001", subAssemblyItemId)
        {
            Quantity = 1m,
            IsActive = true,
            IsDefault = true
        };
        subBom.Items.Add(new BomItem(Guid.NewGuid(), subBom.Id, rawMaterialId, "Piston Ring", 4m, 25m));

        // Top-BOM: Car contains Engine (does not list Piston directly)
        var topBom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-CAR-001", topFgItemId)
        {
            Quantity = 1m,
            IsActive = true
        };
        var engineLine = new BomItem(Guid.NewGuid(), topBom.Id, subAssemblyItemId, "Engine Assembly", 1m, 1000m)
        {
            SubBomId = subBom.Id
        };
        topBom.Items.Add(engineLine);

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { subBom, topBom }.AsQueryable()));
        _bundleRepo.WithDetailsAsync()
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        // Act 1: Search with SearchSubAssemblies = true -> should find BOTH Top BOM and Sub BOM
        var resultExploded = await _appService.SearchAsync(new BomSearchFilterDto
        {
            CompanyId = _companyId,
            Item1Id = rawMaterialId,
            SearchSubAssemblies = true
        });

        // Assert 1
        resultExploded.TotalMatches.ShouldBe(2);
        var topMatch = resultExploded.Rows.FirstOrDefault(r => r.ParentName == "BOM-CAR-001");
        topMatch.ShouldNotBeNull();
        topMatch.FoundInSubAssembly.ShouldBeTrue();

        var subMatch = resultExploded.Rows.FirstOrDefault(r => r.ParentName == "BOM-ENGINE-001");
        subMatch.ShouldNotBeNull();
        subMatch.FoundInSubAssembly.ShouldBeFalse();

        // Act 2: Search with SearchSubAssemblies = false -> should ONLY find Sub BOM, NOT Top BOM
        var resultDirect = await _appService.SearchAsync(new BomSearchFilterDto
        {
            CompanyId = _companyId,
            Item1Id = rawMaterialId,
            SearchSubAssemblies = false
        });

        // Assert 2
        resultDirect.TotalMatches.ShouldBe(1);
        resultDirect.Rows[0].ParentName.ShouldBe("BOM-ENGINE-001");
        resultDirect.Rows.Any(r => r.ParentName == "BOM-CAR-001").ShouldBeFalse();
    }

    [Fact]
    public async Task SearchAsync_ProductBundle_MatchesContainedItems()
    {
        // Arrange
        var bundleItemId = Guid.NewGuid();
        var comp1Id = Guid.NewGuid();
        var comp2Id = Guid.NewGuid();

        var bundleItem = new Item(bundleItemId, _companyId, "ITEM-BUNDLE-DESK", "Standing Desk Set", ItemType.Goods);
        var comp1 = new Item(comp1Id, _companyId, "ITEM-TOP", "Wood Tabletop", ItemType.Goods);
        var comp2 = new Item(comp2Id, _companyId, "ITEM-LEGS", "Electric Motor Legs", ItemType.Goods);
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { bundleItem, comp1, comp2 }));

        var company = new Company(_companyId, "Furniture Inc") { CurrencyCode = "USD" };
        _companyRepo.GetListAsync().Returns(Task.FromResult(new List<Company> { company }));

        var bundle = new ProductBundle(Guid.NewGuid(), bundleItemId);
        bundle.AddItem(comp1Id, 1m, "Wood Tabletop");
        bundle.AddItem(comp2Id, 1m, "Electric Motor Legs");

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials>().AsQueryable()));
        _bundleRepo.WithDetailsAsync()
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        // Act
        var result = await _appService.SearchAsync(new BomSearchFilterDto
        {
            Item1Id = comp1Id,
            Item2Id = comp2Id
        });

        // Assert
        result.ShouldNotBeNull();
        result.ProductBundleMatchesCount.ShouldBe(1);
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].DocType.ShouldBe("Product Bundle");
        result.Rows[0].ItemCode.ShouldBe("ITEM-BUNDLE-DESK");
    }

    [Fact]
    public async Task SearchAsync_MultipleItems_MatchesOnlyBomsContainingAllItems()
    {
        // Arrange
        var fgItemId1 = Guid.NewGuid();
        var fgItemId2 = Guid.NewGuid();
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var itemC = Guid.NewGuid();

        var fg1 = new Item(fgItemId1, _companyId, "FG-1", "Product 1", ItemType.Goods);
        var fg2 = new Item(fgItemId2, _companyId, "FG-2", "Product 2", ItemType.Goods);
        var itmA = new Item(itemA, _companyId, "RM-A", "Component A", ItemType.Goods);
        var itmB = new Item(itemB, _companyId, "RM-B", "Component B", ItemType.Goods);
        var itmC = new Item(itemC, _companyId, "RM-C", "Component C", ItemType.Goods);
        _itemRepo.GetListAsync().Returns(Task.FromResult(new List<Item> { fg1, fg2, itmA, itmB, itmC }));

        var company = new Company(_companyId, "Tech Corp") { CurrencyCode = "USD" };
        _companyRepo.GetListAsync().Returns(Task.FromResult(new List<Company> { company }));

        // BOM 1 contains A and B
        var bom1 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-1", fgItemId1) { IsActive = true };
        bom1.Items.Add(new BomItem(Guid.NewGuid(), bom1.Id, itemA, "Component A", 1, 10));
        bom1.Items.Add(new BomItem(Guid.NewGuid(), bom1.Id, itemB, "Component B", 2, 20));

        // BOM 2 contains A and C (NOT B)
        var bom2 = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-2", fgItemId2) { IsActive = true };
        bom2.Items.Add(new BomItem(Guid.NewGuid(), bom2.Id, itemA, "Component A", 1, 10));
        bom2.Items.Add(new BomItem(Guid.NewGuid(), bom2.Id, itemC, "Component C", 3, 30));

        _bomRepo.WithDetailsAsync(Arg.Any<Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom1, bom2 }.AsQueryable()));
        _bundleRepo.WithDetailsAsync()
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        // Act: Searching for Item A AND Item B
        var result = await _appService.SearchAsync(new BomSearchFilterDto
        {
            CompanyId = _companyId,
            Item1Id = itemA,
            Item2Id = itemB
        });

        // Assert: Only BOM-1 should match, BOM-2 does not contain Item B
        result.TotalMatches.ShouldBe(1);
        result.Rows[0].ParentName.ShouldBe("BOM-1");
    }
}
