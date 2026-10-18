using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class ItemVariantDetailsAppServiceTests
{
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemAttribute, Guid> _attributeRepo = Substitute.For<IRepository<ItemAttribute, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
    private readonly IRepository<PriceList, Guid> _priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();

    private readonly ItemVariantDetailsAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _var1Id = Guid.NewGuid();
    private readonly Guid _var2Id = Guid.NewGuid();
    private readonly Guid _colorAttrId = Guid.NewGuid();
    private readonly Guid _sizeAttrId = Guid.NewGuid();
    private readonly Guid _sellingPlId = Guid.NewGuid();
    private readonly Guid _buyingPlId = Guid.NewGuid();

    public ItemVariantDetailsAppServiceTests()
    {
        _appService = new ItemVariantDetailsAppService(
            _itemRepo,
            _attributeRepo,
            _binRepo,
            _itemPriceRepo,
            _priceListRepo,
            _salesOrderRepo);
    }

    [Fact]
    public async Task GetReportAsync_EmptyTemplateId_ReturnsEmptyReport()
    {
        var result = await _appService.GetReportAsync(new ItemVariantDetailsFilterDto());

        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TemplateItemId.ShouldBeNull();
    }

    [Fact]
    public async Task GetReportAsync_NonExistentTemplate_ReturnsEmptyReport()
    {
        _itemRepo.FindAsync(_templateId).Returns((Item?)null);

        var result = await _appService.GetReportAsync(new ItemVariantDetailsFilterDto
        {
            TemplateItemId = _templateId
        });

        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WithVariants_ReturnsCorrectVariantDetails()
    {
        // 1. Template item
        var template = new Item(_templateId, _companyId, "TSHIRT", "T-Shirt Template", ItemType.Goods)
        {
            HasVariants = true
        };
        _itemRepo.FindAsync(_templateId).Returns(template);

        // 2. Variants
        var var1 = new Item(_var1Id, _companyId, "TSHIRT-RED-S", "T-Shirt Red Small", ItemType.Goods)
        {
            VariantOfId = _templateId,
            IsActive = true,
            StandardSellingPrice = 25m,
            StandardBuyingPrice = 10m
        };
        var1.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), _var1Id, _colorAttrId, "Red"));
        var1.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), _var1Id, _sizeAttrId, "Small"));

        var var2 = new Item(_var2Id, _companyId, "TSHIRT-BLUE-M", "T-Shirt Blue Medium", ItemType.Goods)
        {
            VariantOfId = _templateId,
            IsActive = true,
            StandardSellingPrice = 30m,
            StandardBuyingPrice = 12m
        };
        var2.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), _var2Id, _colorAttrId, "Blue"));
        var2.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), _var2Id, _sizeAttrId, "Medium"));

        var variantsList = new List<Item> { var1, var2 };
        _itemRepo.WithDetailsAsync(Arg.Any<Expression<Func<Item, object>>[]>())
            .Returns(Task.FromResult(variantsList.AsQueryable()));

        // 3. Attributes
        var colorAttr = new ItemAttribute(_colorAttrId, "Color");
        var sizeAttr = new ItemAttribute(_sizeAttrId, "Size");
        _attributeRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemAttribute> { colorAttr, sizeAttr }.AsQueryable()));

        // 4. Bins
        var bin1 = new Bin(Guid.NewGuid(), _var1Id, _warehouseId)
        {
            ActualQty = 15m,
            PlannedQty = 5m
        };
        var bin2 = new Bin(Guid.NewGuid(), _var2Id, _warehouseId)
        {
            ActualQty = 20m,
            PlannedQty = 0m
        };
        _binRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        // 5. Price Lists
        var sellingPl = new PriceList(_sellingPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        var buyingPl = new PriceList(_buyingPlId, "Standard Buying", "MYR", isSelling: false, isBuying: true);
        _priceListRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<PriceList> { sellingPl, buyingPl }.AsQueryable()));

        // 6. Item Prices (only for var1, var2 will fallback to standard prices)
        var priceSelling1 = new ItemPrice(Guid.NewGuid(), _var1Id, _sellingPlId, 28m, "Unit", "MYR");
        var priceBuying1 = new ItemPrice(Guid.NewGuid(), _var1Id, _buyingPlId, 11m, "Unit", "MYR");
        _itemPriceRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ItemPrice> { priceSelling1, priceBuying1 }.AsQueryable()));

        // 7. Sales Order with pending delivery for var1
        var so = new SalesOrder(Guid.NewGuid(), _companyId, Guid.NewGuid(), "SO-2026-0001", DateTime.UtcNow)
        {
            DeliveryDate = DateTime.UtcNow.AddDays(7)
        };
        so.AddItem(_var1Id, "T-Shirt Red Small", 10m, 28m, 0m, "Unit", DateTime.UtcNow.AddDays(7));
        so.Items.First().DeliveredQty = 2m;
        so.Submit();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        // Act
        var result = await _appService.GetReportAsync(new ItemVariantDetailsFilterDto
        {
            TemplateItemId = _templateId,
            WarehouseId = _warehouseId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TemplateItemId.ShouldBe(_templateId);
        result.TemplateItemCode.ShouldBe("TSHIRT");
        result.AttributeNames.ShouldContain("Color");
        result.AttributeNames.ShouldContain("Size");
        result.Rows.Count.ShouldBe(2);

        var r1 = result.Rows.First(r => r.VariantId == _var1Id);
        r1.VariantCode.ShouldBe("TSHIRT-RED-S");
        r1.Attributes["Color"].ShouldBe("Red");
        r1.Attributes["Size"].ShouldBe("Small");
        r1.CurrentStock.ShouldBe(15m);
        r1.InProduction.ShouldBe(5m);
        r1.AvgSellingPrice.ShouldBe(28m);
        r1.AvgBuyingPrice.ShouldBe(11m);
        r1.OpenOrders.ShouldBe(1);

        var r2 = result.Rows.First(r => r.VariantId == _var2Id);
        r2.VariantCode.ShouldBe("TSHIRT-BLUE-M");
        r2.Attributes["Color"].ShouldBe("Blue");
        r2.Attributes["Size"].ShouldBe("Medium");
        r2.CurrentStock.ShouldBe(20m);
        r2.InProduction.ShouldBe(0m);
        r2.AvgSellingPrice.ShouldBe(30m);
        r2.AvgBuyingPrice.ShouldBe(12m);
        r2.OpenOrders.ShouldBe(0);
    }
}
