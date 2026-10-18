using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class SerialNoWarrantyExpiryAppServiceTests
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepo = Substitute.For<IRepository<SerialNo, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();

    private readonly SerialNoWarrantyExpiryAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public SerialNoWarrantyExpiryAppServiceTests()
    {
        _appService = new SerialNoWarrantyExpiryAppService(
            _serialNoRepo,
            _itemRepo,
            _warehouseRepo,
            _customerRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesWarrantyAndAmcStatuses_Correctly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sn1 = new SerialNo(Guid.NewGuid(), _itemId, "SN-WARRANTY-ACTIVE", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = now.AddDays(60),
            CustomerId = _customerId
        };
        var sn2 = new SerialNo(Guid.NewGuid(), _itemId, "SN-AMC-ACTIVE", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = now.AddDays(-30),
            AmcExpiryDate = now.AddDays(60),
            CustomerId = _customerId
        };
        var sn3 = new SerialNo(Guid.NewGuid(), _itemId, "SN-OUT-OF-COVERAGE", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = now.AddDays(-90),
            AmcExpiryDate = now.AddDays(-15),
            CustomerId = _customerId
        };

        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { sn1, sn2, sn3 }.AsQueryable()));

        var item = new Item(_itemId, _companyId, "ITEM-LAPTOP-01", "Laptop Pro", ItemType.Goods) { HasSerialNo = true };
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(Task.FromResult(new List<Item> { item }));

        var wh = new Warehouse(_warehouseId, _companyId, "Finished Goods WH");
        _warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(Task.FromResult(new List<Warehouse> { wh }));

        var customer = new Customer(_customerId, _companyId, "Tech Corp") { CustomerCode = "CUST-001" };
        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(Task.FromResult(new List<Customer> { customer }));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoWarrantyExpiryFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(3);
        result.UnderWarrantyCount.ShouldBe(1);
        result.OutOfWarrantyCount.ShouldBe(2);
        result.UnderAmcCount.ShouldBe(1);
        result.OutOfAmcCount.ShouldBe(1);

        var r1 = result.Rows.First(r => r.SerialNumber == "SN-WARRANTY-ACTIVE");
        r1.MaintenanceStatus.ShouldBe("Under Warranty");
        r1.IsWarrantyExpired.ShouldBeFalse();
        r1.CustomerName.ShouldBe("Tech Corp");

        var r2 = result.Rows.First(r => r.SerialNumber == "SN-AMC-ACTIVE");
        r2.MaintenanceStatus.ShouldBe("Under AMC");
        r2.IsWarrantyExpired.ShouldBeTrue();
        r2.IsAmcExpired.ShouldBeFalse();

        var r3 = result.Rows.First(r => r.SerialNumber == "SN-OUT-OF-COVERAGE");
        r3.MaintenanceStatus.ShouldBe("Out of AMC");
        r3.IsWarrantyExpired.ShouldBeTrue();
        r3.IsAmcExpired.ShouldBeTrue();
    }

    [Fact]
    public async Task GetReportAsync_FilterByThreshold_ReturnsOnlyExpiringWithinThreshold()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var snExpiringSoon = new SerialNo(Guid.NewGuid(), _itemId, "SN-EXPIRING-SOON", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = now.AddDays(10) // within 30 days
        };
        var snFarFuture = new SerialNo(Guid.NewGuid(), _itemId, "SN-FAR-FUTURE", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = now.AddDays(90) // > 30 days
        };

        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SerialNo> { snExpiringSoon, snFarFuture }.AsQueryable()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(Task.FromResult(new List<Item>()));
        _warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(Task.FromResult(new List<Warehouse>()));
        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(Task.FromResult(new List<Customer>()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoWarrantyExpiryFilterDto
        {
            CompanyId = _companyId,
            DaysThreshold = 30
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SerialNumber.ShouldBe("SN-EXPIRING-SOON");
    }

    [Fact]
    public async Task GetReportAsync_FilterByCustomerAndWarehouse_AppliesFiltersCorrectly()
    {
        // Arrange
        var otherCustId = Guid.NewGuid();
        var snMatched = new SerialNo(Guid.NewGuid(), _itemId, "SN-MATCHED", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = DateTime.UtcNow.AddDays(30),
            CustomerId = _customerId
        };
        var snOtherCust = new SerialNo(Guid.NewGuid(), _itemId, "SN-OTHER-CUST", _companyId, _warehouseId)
        {
            WarrantyExpiryDate = DateTime.UtcNow.AddDays(30),
            CustomerId = otherCustId
        };

        var allSerials = new List<SerialNo> { snMatched, snOtherCust };
        _serialNoRepo.GetQueryableAsync().Returns(Task.FromResult(allSerials.AsQueryable()));
        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(Task.FromResult(new List<Item>()));
        _warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(Task.FromResult(new List<Warehouse>()));
        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(Task.FromResult(new List<Customer>()));

        // Act
        var result = await _appService.GetReportAsync(new SerialNoWarrantyExpiryFilterDto
        {
            CompanyId = _companyId,
            CustomerId = _customerId
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SerialNumber.ShouldBe("SN-MATCHED");
    }
}
