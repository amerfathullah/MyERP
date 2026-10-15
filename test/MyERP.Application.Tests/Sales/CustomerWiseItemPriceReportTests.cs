using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Unit tests for Customer-wise Item Price report per ERPNext PR #60300 / commit a1899ecf69.
/// Verifies:
/// - Customer filter is mandatory.
/// - Customer-specific price takes precedence over generic price.
/// - Other customers' special prices are strictly excluded (prevents rate leaks).
/// - Expired and non-matching UOM prices are ignored.
/// - Fallback to default price list when customer price list has zero/no rate.
/// - Stock on hand from Bin is included.
/// </summary>
public class CustomerWiseItemPriceReportTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<PriceList, Guid> _priceListRepo = Substitute.For<IRepository<PriceList, Guid>>();
    private readonly IRepository<ItemPrice, Guid> _itemPriceRepo = Substitute.For<IRepository<ItemPrice, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly ISettingProvider _settingProvider = Substitute.For<ISettingProvider>();

    private readonly SalesAnalyticsAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerAId = Guid.NewGuid();
    private readonly Guid _customerOtherId = Guid.NewGuid();
    private readonly Guid _standardPlId = Guid.NewGuid();
    private readonly Guid _customPlId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    public CustomerWiseItemPriceReportTests()
    {
        _appService = new SalesAnalyticsAppService(_siRepo, _customerRepo, _itemRepo);

        // Configure LazyServiceProvider mocks
        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<PriceList, Guid>>().Returns(_priceListRepo);
        lazySp.LazyGetRequiredService<IRepository<ItemPrice, Guid>>().Returns(_itemPriceRepo);
        lazySp.LazyGetService<IRepository<Bin, Guid>>().Returns(_binRepo);
        lazySp.LazyGetService<ISettingProvider>().Returns(_settingProvider);

        // Inject LazyServiceProvider via ABP property
        _appService.LazyServiceProvider = lazySp;
    }

    [Fact]
    public async Task CustomerFilter_IsMandatory()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
            {
                CustomerId = Guid.Empty
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task CustomerSpecificPrice_TakesPrecedenceOverGenericPrice()
    {
        // Arrange
        var customerA = new Customer(Guid.NewGuid(), _companyId, "Customer A") { DefaultPriceListId = _standardPlId };
        _customerRepo.GetAsync(_customerAId).Returns(customerA);

        var pl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { pl }.AsQueryable());

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Item 1", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        // Generic price: 250, Customer A price: 200
        var genericPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 250m, "Unit", "MYR") { CustomerId = null };
        var customerPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 200m, "Unit", "MYR") { CustomerId = _customerAId };

        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { genericPrice, customerPrice }.AsQueryable());
        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());

        // Act
        var result = await _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
        {
            CustomerId = _customerAId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].SellingRate.ShouldBe(200m); // Customer-specific 200 takes precedence
        result[0].PriceListName.ShouldBe("Standard Selling");
    }

    [Fact]
    public async Task OtherCustomersSpecialPrice_IsNotShown_PreventingRateLeak()
    {
        // Arrange: Per PR #60300 test_other_customers_special_price_is_not_shown
        var customerA = new Customer(Guid.NewGuid(), _companyId, "Customer A") { DefaultPriceListId = _standardPlId };
        _customerRepo.GetAsync(_customerAId).Returns(customerA);

        var pl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { pl }.AsQueryable());

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Item 1", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        // Generic price: 250, Other Customer special price: 700
        var genericPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 250m, "Unit", "MYR") { CustomerId = null };
        var otherCustomerPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 700m, "Unit", "MYR") { CustomerId = _customerOtherId };

        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { genericPrice, otherCustomerPrice }.AsQueryable());
        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());

        // Act: Customer A queries prices
        var result = await _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
        {
            CustomerId = _customerAId
        });

        // Assert: Must see generic rate 250, NOT Other Customer's rate 700
        result.Count.ShouldBe(1);
        result[0].SellingRate.ShouldBe(250m);
    }

    [Fact]
    public async Task ExpiredAndDifferentUomPrices_AreIgnored()
    {
        // Arrange: Per PR #60300 test_expired_and_other_uom_prices_are_ignored
        var customerA = new Customer(Guid.NewGuid(), _companyId, "Customer A") { DefaultPriceListId = _standardPlId };
        _customerRepo.GetAsync(_customerAId).Returns(customerA);

        var pl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { pl }.AsQueryable());

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Item 1", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        // Active generic price: 250 ("Unit")
        var genericPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 250m, "Unit", "MYR") { CustomerId = null };

        // Expired customer price (valid in past year): 500
        var expiredPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 500m, "Unit", "MYR")
        {
            CustomerId = _customerAId,
            ValidFrom = DateTime.UtcNow.AddYears(-2),
            ValidUpto = DateTime.UtcNow.AddYears(-1)
        };

        // Non-matching UOM price ("Box"): 999
        var boxPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 999m, "Box", "MYR") { CustomerId = null };

        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { genericPrice, expiredPrice, boxPrice }.AsQueryable());
        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());

        // Act
        var result = await _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
        {
            CustomerId = _customerAId
        });

        // Assert: Resolves active "Unit" rate 250
        result[0].SellingRate.ShouldBe(250m);
    }

    [Fact]
    public async Task FallsBackToSellingSettingsPriceList_WhenCustomerListYieldsZero()
    {
        // Arrange: Per PR #60300 test_falls_back_to_selling_settings_price_list
        var customerA = new Customer(Guid.NewGuid(), _companyId, "Customer A") { DefaultPriceListId = _customPlId };
        _customerRepo.GetAsync(_customerAId).Returns(customerA);

        var customPl = new PriceList(_customPlId, "Zero PL", "MYR", isSelling: true, isBuying: false);
        var fallbackPl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { customPl, fallbackPl }.AsQueryable());

        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Item 1", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        // Custom PL price is 0
        var zeroPrice = new ItemPrice(Guid.NewGuid(), item.Id, _customPlId, 0m, "Unit", "MYR") { CustomerId = null };
        // Fallback PL price is 250
        var fallbackPrice = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 250m, "Unit", "MYR") { CustomerId = null };

        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { zeroPrice, fallbackPrice }.AsQueryable());
        _binRepo.GetQueryableAsync().Returns(new List<Bin>().AsQueryable());

        _settingProvider.GetOrNullAsync(MyERPSettings.Selling.FallbackToDefaultPriceList).Returns("True");
        _settingProvider.GetOrNullAsync(MyERPSettings.Selling.DefaultPriceList).Returns(_standardPlId.ToString());

        // Act
        var result = await _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
        {
            CustomerId = _customerAId
        });

        // Assert: Resolved to fallback price list rate 250
        result[0].SellingRate.ShouldBe(250m);
        result[0].PriceListName.ShouldBe("Standard Selling");
    }

    [Fact]
    public async Task AvailableStock_AggregatedFromBin()
    {
        // Arrange
        var customerA = new Customer(Guid.NewGuid(), _companyId, "Customer A") { DefaultPriceListId = _standardPlId };
        _customerRepo.GetAsync(_customerAId).Returns(customerA);

        var pl = new PriceList(_standardPlId, "Standard Selling", "MYR", isSelling: true, isBuying: false);
        _priceListRepo.GetQueryableAsync().Returns(new List<PriceList> { pl }.AsQueryable());

        var item = new Item(_itemId, _companyId, "ITEM-001", "Item 1", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var bin1 = new Bin(Guid.NewGuid(), item.Id, Guid.NewGuid()) { ActualQty = 10m };
        var bin2 = new Bin(Guid.NewGuid(), item.Id, Guid.NewGuid()) { ActualQty = 5m };
        _binRepo.GetQueryableAsync().Returns(new List<Bin> { bin1, bin2 }.AsQueryable());

        var price = new ItemPrice(Guid.NewGuid(), item.Id, _standardPlId, 100m, "Unit", "MYR");
        _itemPriceRepo.GetQueryableAsync().Returns(new List<ItemPrice> { price }.AsQueryable());

        // Act
        var result = await _appService.GetCustomerWiseItemPriceReportAsync(new CustomerWiseItemPriceInputDto
        {
            CustomerId = _customerAId
        });

        // Assert
        result[0].AvailableStock.ShouldBe(15m); // 10 + 5
    }
}
