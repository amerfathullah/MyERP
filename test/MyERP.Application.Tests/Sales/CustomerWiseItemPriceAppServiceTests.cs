using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class CustomerWiseItemPriceAppServiceTests
{
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<PriceList, Guid> _priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly CustomerWiseItemPriceAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _wholesalePlId = Guid.NewGuid();
    private readonly Guid _standardPlId = Guid.NewGuid();
    private readonly Guid _wh1Id = Guid.NewGuid();
    private readonly Guid _wh2Id = Guid.NewGuid();
    private readonly Guid _itemAId = Guid.NewGuid();
    private readonly Guid _itemBId = Guid.NewGuid();
    private readonly Guid _itemCId = Guid.NewGuid();

    public CustomerWiseItemPriceAppServiceTests()
    {
        _appService = new CustomerWiseItemPriceAppService(
            _customerRepo,
            _itemRepo,
            _priceListRepo,
            _itemPriceRepo,
            _binRepo,
            _warehouseRepo);
    }

    [Fact]
    public async Task GetReportAsync_ShouldResolveCustomerSpecificPrice_WhenDefined()
    {
        var customer = new Customer(_customerId, _companyId, "Acme Customer")
        {
            DefaultPriceListId = _wholesalePlId
        };
        _customerRepo.FindAsync(_customerId).Returns(Task.FromResult<Customer?>(customer));

        var wholesalePl = new PriceList(_wholesalePlId, "Wholesale", "MYR", isSelling: true, isBuying: false) { IsActive = true };
        var standardPl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false) { IsActive = true, IsDefault = true };

        _priceListRepo.FindAsync(_wholesalePlId).Returns(Task.FromResult<PriceList?>(wholesalePl));
        _priceListRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PriceList> { wholesalePl, standardPl }.AsQueryable()));

        var itemA = new Item(_itemAId, _companyId, "ITEM-A", "Widget A", ItemType.Goods) { IsActive = true, Uom = "Pcs" };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemA }.AsQueryable()));

        var wh1 = new Warehouse(_wh1Id, _companyId, "Main Warehouse") { IsActive = true };
        var wh2 = new Warehouse(_wh2Id, _companyId, "Secondary Warehouse") { IsActive = true };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { wh1, wh2 }.AsQueryable()));

        var bin1 = new Bin(Guid.NewGuid(), _itemAId, _wh1Id) { ActualQty = 50m };
        var bin2 = new Bin(Guid.NewGuid(), _itemAId, _wh2Id) { ActualQty = 25m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin1, bin2 }.AsQueryable()));

        var ipGeneral = new ItemPrice(Guid.NewGuid(), _itemAId, _wholesalePlId, 90m, "Pcs", "MYR");
        var ipCustomer = new ItemPrice(Guid.NewGuid(), _itemAId, _wholesalePlId, 80m, "Pcs", "MYR") { CustomerId = _customerId };
        _itemPriceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemPrice> { ipGeneral, ipCustomer }.AsQueryable()));

        var result = await _appService.GetReportAsync(new GetCustomerWiseItemPriceInput
        {
            CompanyId = _companyId,
            CustomerId = _customerId
        });

        result.CustomerName.ShouldBe("Acme Customer");
        result.DefaultPriceListName.ShouldBe("Wholesale");
        result.TotalItems.ShouldBe(1);
        result.TotalAvailableStock.ShouldBe(75m);

        var row = result.Rows.First();
        row.ItemCode.ShouldBe("ITEM-A");
        row.SellingRate.ShouldBe(80m);
        row.PriceListName.ShouldBe("Wholesale");
        row.AvailableStock.ShouldBe(75m);
        row.StockUom.ShouldBe("Pcs");
    }

    [Fact]
    public async Task GetReportAsync_ShouldFallbackToGeneralPrice_OrStandardSellingPrice()
    {
        var customer = new Customer(_customerId, _companyId, "Regular Customer");
        _customerRepo.FindAsync(_customerId).Returns(Task.FromResult<Customer?>(customer));

        var standardPl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false) { IsActive = true, IsDefault = true };
        _priceListRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PriceList> { standardPl }.AsQueryable()));

        var itemB = new Item(_itemBId, _companyId, "ITEM-B", "Widget B", ItemType.Goods) { IsActive = true };
        var itemC = new Item(_itemCId, _companyId, "ITEM-C", "Widget C", ItemType.Goods) { IsActive = true, StandardSellingPrice = 45m };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemB, itemC }.AsQueryable()));

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin>().AsQueryable()));

        var ipGeneral = new ItemPrice(Guid.NewGuid(), _itemBId, _standardPlId, 120m, "Unit", "MYR");
        _itemPriceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemPrice> { ipGeneral }.AsQueryable()));

        var result = await _appService.GetReportAsync(new GetCustomerWiseItemPriceInput
        {
            CompanyId = _companyId,
            CustomerId = _customerId
        });

        result.TotalItems.ShouldBe(2);

        var rowB = result.Rows.First(r => r.ItemCode == "ITEM-B");
        rowB.SellingRate.ShouldBe(120m);
        rowB.PriceListName.ShouldBe("Standard Selling");

        var rowC = result.Rows.First(r => r.ItemCode == "ITEM-C");
        rowC.SellingRate.ShouldBe(45m);
        rowC.PriceListName.ShouldBe("Standard Selling");
    }

    [Fact]
    public async Task GetReportAsync_ShouldThrowEntityNotFound_WhenCustomerDoesNotExist()
    {
        _customerRepo.FindAsync(_customerId).Returns(Task.FromResult<Customer?>(null));

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            _appService.GetReportAsync(new GetCustomerWiseItemPriceInput
            {
                CompanyId = _companyId,
                CustomerId = _customerId
            }));
    }
}
