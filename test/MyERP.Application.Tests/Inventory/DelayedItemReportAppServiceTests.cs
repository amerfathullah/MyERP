using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

public class DelayedItemReportAppServiceTests
{
    private readonly IRepository<DeliveryNote, Guid> _dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

    private readonly DelayedItemReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public DelayedItemReportAppServiceTests()
    {
        _appService = new DelayedItemReportAppService(
            _dnRepo,
            _siRepo,
            _soRepo,
            _customerRepo,
            _itemRepo,
            _itemGroupRepo);
    }

    [Fact]
    public async Task GetReportAsync_DeliveryNote_CalculatesDelayedDaysCorrectly()
    {
        // Arrange
        var customer = new Customer(_customerId, _companyId, "Global Retailers Ltd") { CustomerCode = "CUST-001" };
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-LAPTOP", "Laptop Core i7", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, _customerId, "SO-2026-0001", new DateTime(2026, 6, 1))
        {
            DeliveryDate = new DateTime(2026, 6, 5),
            CustomerPoNumber = "PO-GLOBAL-99"
        };
        so.AddItem(itemId, "Laptop Core i7", 10m, 1200m, 0m, "Unit", deliveryDate: new DateTime(2026, 6, 5));
        var soItemId = so.Items[0].Id;

        _soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(new List<SalesOrder> { so }.AsQueryable());

        // Delivery Note posted on June 10 (5 days after expected delivery date June 5)
        var warehouseId = Guid.NewGuid();
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, warehouseId, "DN-2026-0001", new DateTime(2026, 6, 10))
        {
            SalesOrderId = soId
        };
        dn.AddItem(itemId, "Laptop Core i7", 10m, 1200m, 0m, "Unit", salesOrderItemId: soItemId);
        dn.Submit();

        _dnRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(new List<DeliveryNote> { dn }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new DelayedItemFilterDto
        {
            CompanyId = _companyId,
            BasedOn = "Delivery Note",
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            DelayedOnly = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItems.ShouldBe(1);
        result.DelayedItems.ShouldBe(1);
        result.AverageDelayDays.ShouldBe(5);
        result.TotalDelayedAmount.ShouldBe(12000m); // 10 * 1200

        var row = result.Rows[0];
        row.VoucherNumber.ShouldBe("DN-2026-0001");
        row.CustomerName.ShouldBe("Global Retailers Ltd");
        row.SalesOrderNumber.ShouldBe("SO-2026-0001");
        row.CustomerPoNumber.ShouldBe("PO-GLOBAL-99");
        row.ItemCode.ShouldBe("ITEM-LAPTOP");
        row.ExpectedDeliveryDate.ShouldBe(new DateTime(2026, 6, 5));
        row.ActualDeliveryDate.ShouldBe(new DateTime(2026, 6, 10));
        row.DelayedDays.ShouldBe(5);
    }

    [Fact]
    public async Task GetReportAsync_DelayedOnly_ExcludesOnTimeDeliveries()
    {
        // Arrange
        var customer = new Customer(_customerId, _companyId, "Fast Deliveries") { CustomerCode = "CUST-002" };
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-PARTS", "Spare Parts", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());
        _itemGroupRepo.GetQueryableAsync().Returns(new List<ItemGroup>().AsQueryable());

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, _customerId, "SO-ONTIME", new DateTime(2026, 6, 1))
        {
            DeliveryDate = new DateTime(2026, 6, 5)
        };
        so.AddItem(itemId, "Spare Parts", 5m, 100m, 0m, "Unit", deliveryDate: new DateTime(2026, 6, 5));

        _soRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(new List<SalesOrder> { so }.AsQueryable());

        // Delivery Note posted on June 4 (early/on-time)
        var warehouseId = Guid.NewGuid();
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, warehouseId, "DN-ONTIME", new DateTime(2026, 6, 4))
        {
            SalesOrderId = soId
        };
        dn.AddItem(itemId, "Spare Parts", 5m, 100m, 0m, "Unit", salesOrderItemId: so.Items[0].Id);
        dn.Submit();

        _dnRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(new List<DeliveryNote> { dn }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new DelayedItemFilterDto
        {
            CompanyId = _companyId,
            DelayedOnly = true
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalItems.ShouldBe(0);
        result.Rows.ShouldBeEmpty();
    }
}
