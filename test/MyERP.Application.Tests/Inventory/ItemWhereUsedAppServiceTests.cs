using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class ItemWhereUsedAppServiceTests
{
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<BillOfMaterials, Guid> _bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
    private readonly IRepository<ProductBundle, Guid> _bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
    private readonly IRepository<SubcontractingBom, Guid> _scBomRepo = Substitute.For<IRepository<SubcontractingBom, Guid>>();
    private readonly IRepository<ItemAlternative, Guid> _altRepo = Substitute.For<IRepository<ItemAlternative, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly ItemWhereUsedAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemWhereUsedAppServiceTests()
    {
        _appService = new ItemWhereUsedAppService(
            _itemRepo,
            _bomRepo,
            _bundleRepo,
            _scBomRepo,
            _altRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsWhereUsed_BomComponentsAndSecondaryItems()
    {
        // Arrange
        var targetItemId = Guid.NewGuid();
        var targetItem = new Item(targetItemId, _companyId, "RM-STEEL", "Steel Sheet", ItemType.Goods);

        var fgItemId = Guid.NewGuid();
        var fgItem = new Item(fgItemId, _companyId, "FG-CABINET", "Metal Cabinet", ItemType.Goods);

        var company = new Company(_companyId, "Apex Mfg");
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _companyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Company> { company }.AsQueryable()));

        _itemRepo.FindAsync(targetItemId).Returns(Task.FromResult<Item?>(targetItem));
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { targetItem, fgItem }.AsQueryable()));

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-CAB-001", fgItemId);
        var bomItem = new BomItem(Guid.NewGuid(), bom.Id, targetItemId, "Steel Sheet", 4m, 20m)
        {
            Uom = "Nos",
            StockUom = "Nos"
        };
        bom.Items.Add(bomItem);

        var secItem = new BomSecondaryItem(Guid.NewGuid(), bom.Id, targetItemId, SecondaryItemType.Scrap, 0.5m)
        {
            StockUom = "Kg"
        };
        bom.SecondaryItems.Add(secItem);

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        _scBomRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SubcontractingBom>().AsQueryable()));

        _altRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemAlternative>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWhereUsedRequestDto
        {
            ItemId = targetItemId,
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.ItemCode.ShouldBe("RM-STEEL");
        result.TotalWhereUsedCount.ShouldBe(2);

        var compRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "BOM Component");
        compRow.ShouldNotBeNull();
        compRow.DocumentName.ShouldBe("BOM-CAB-001");
        compRow.RelatedItemCode.ShouldBe("FG-CABINET");
        compRow.Quantity.ShouldBe(4m);

        var secRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "BOM Secondary Item");
        secRow.ShouldNotBeNull();
        secRow.Quantity.ShouldBe(0.5m);
        secRow.Details.ShouldBe("Scrap");
    }

    [Fact]
    public async Task GetReportAsync_ReturnsReferences_BomOutputAndVariants()
    {
        // Arrange
        var targetItemId = Guid.NewGuid();
        var targetItem = new Item(targetItemId, _companyId, "ITEM-TSHIRT", "Cotton T-Shirt", ItemType.Goods)
        {
            HasVariants = true
        };

        var variantItem = new Item(Guid.NewGuid(), _companyId, "TSHIRT-RED-L", "Cotton T-Shirt Red L", ItemType.Goods)
        {
            VariantOfId = targetItemId
        };

        var company = new Company(_companyId, "Apex Mfg");
        _companyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Company> { company }.AsQueryable()));

        _itemRepo.FindAsync(targetItemId).Returns(Task.FromResult<Item?>(targetItem));
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { targetItem, variantItem }.AsQueryable()));

        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-TSHIRT-001", targetItemId)
        {
            Quantity = 10m,
            Uom = "Nos"
        };

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        _scBomRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SubcontractingBom>().AsQueryable()));

        _altRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemAlternative>().AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWhereUsedRequestDto
        {
            ItemId = targetItemId,
            Section = "References"
        });

        // Assert
        result.Rows.Count.ShouldBe(2);

        var outputRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "BOM Output");
        outputRow.ShouldNotBeNull();
        outputRow.DocumentName.ShouldBe("BOM-TSHIRT-001");
        outputRow.Quantity.ShouldBe(10m);

        var variantRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "Item Variant");
        variantRow.ShouldNotBeNull();
        variantRow.DocumentName.ShouldBe("TSHIRT-RED-L");
    }

    [Fact]
    public async Task GetReportAsync_ReturnsProductBundleAndAlternative()
    {
        // Arrange
        var targetItemId = Guid.NewGuid();
        var targetItem = new Item(targetItemId, _companyId, "MOUSE", "Wireless Mouse", ItemType.Goods);

        var bundleParentId = Guid.NewGuid();
        var bundleParentItem = new Item(bundleParentId, _companyId, "KIT-DESKTOP", "Desktop Kit", ItemType.Goods);

        var altItemId = Guid.NewGuid();
        var altItem = new Item(altItemId, _companyId, "MOUSE-BT", "Bluetooth Mouse", ItemType.Goods);

        _companyRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Company>().AsQueryable()));
        _itemRepo.FindAsync(targetItemId).Returns(Task.FromResult<Item?>(targetItem));
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { targetItem, bundleParentItem, altItem }.AsQueryable()));

        _bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials>().AsQueryable()));

        var bundle = new ProductBundle(Guid.NewGuid(), bundleParentId);
        bundle.AddItem(targetItemId, 2m, "Wireless Mouse", "Nos");

        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        _scBomRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SubcontractingBom>().AsQueryable()));

        var alt = new ItemAlternative(Guid.NewGuid(), _companyId, targetItemId, altItemId, true);
        _altRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemAlternative> { alt }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemWhereUsedRequestDto
        {
            ItemId = targetItemId
        });

        // Assert
        result.TotalWhereUsedCount.ShouldBe(1);
        result.TotalReferencesCount.ShouldBe(1);

        var bundleRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "Product Bundle Component");
        bundleRow.ShouldNotBeNull();
        bundleRow.RelatedItemCode.ShouldBe("KIT-DESKTOP");
        bundleRow.Quantity.ShouldBe(2m);

        var altRow = result.Rows.FirstOrDefault(r => r.ReferenceType == "Item Alternative");
        altRow.ShouldNotBeNull();
        altRow.RelatedItemCode.ShouldBe("MOUSE-BT");
        altRow.Details.ShouldBe("Two-Way Alternative");
    }

    [Fact]
    public async Task GetReportAsync_WhenItemNotFound_ReturnsEmptyReport()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        _itemRepo.FindAsync(missingId).Returns(Task.FromResult<Item?>(null));

        // Act
        var result = await _appService.GetReportAsync(new ItemWhereUsedRequestDto
        {
            ItemId = missingId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.ItemCode.ShouldBeEmpty();
    }
}
