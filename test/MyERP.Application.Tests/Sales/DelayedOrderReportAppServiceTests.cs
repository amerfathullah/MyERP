using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Sales;

public class DelayedOrderReportAppServiceTests
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly DelayedOrderReportAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public DelayedOrderReportAppServiceTests()
    {
        _appService = new DelayedOrderReportAppService(
            _deliveryNoteRepo,
            _salesOrderRepo,
            _customerRepo,
            _itemRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_CalculatesDelayedDaysAndOnTimeDeliveryRate()
    {
        // Arrange
        var company = new Company(_companyId, "Test Company");
        _companyRepo.FindAsync(_companyId).Returns(company);

        var customerId = Guid.NewGuid();
        var customer = new Customer(customerId, _companyId, "Acme Corp");
        _customerRepo.GetQueryableAsync().Returns(new List<Customer> { customer }.AsQueryable());

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-001", "Hydraulic Pump", ItemType.Goods) { Description = "Heavy pump" };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        var whId = Guid.NewGuid();

        // 1. SalesOrder 1: Promised delivery date 2026-06-01
        var so1Id = Guid.NewGuid();
        var so1 = new SalesOrder(so1Id, _companyId, customerId, "SO-0001", new DateTime(2026, 5, 20))
        {
            DeliveryDate = new DateTime(2026, 6, 1),
            CustomerPoNumber = "PO-ACME-01"
        };

        // 2. SalesOrder 2: Promised delivery date 2026-06-10
        var so2Id = Guid.NewGuid();
        var so2 = new SalesOrder(so2Id, _companyId, customerId, "SO-0002", new DateTime(2026, 6, 1))
        {
            DeliveryDate = new DateTime(2026, 6, 10),
            CustomerPoNumber = "PO-ACME-02"
        };
        _salesOrderRepo.GetQueryableAsync().Returns(new List<SalesOrder> { so1, so2 }.AsQueryable());

        // DeliveryNote 1: Delivered 2026-06-05 (4 days delayed!)
        var dn1 = new DeliveryNote(Guid.NewGuid(), _companyId, customerId, whId, "DN-0001", new DateTime(2026, 6, 5))
        {
            SalesOrderId = so1Id,
            NetTotal = 1000m,
            TaxAmount = 60m,
            GrandTotal = 1060m
        };
        dn1.AddItem(itemId, "Heavy pump", 2m, 500m, 60m, "Nos");
        dn1.Submit();

        // DeliveryNote 2: Delivered 2026-06-08 (on time! 2 days before deadline)
        var dn2 = new DeliveryNote(Guid.NewGuid(), _companyId, customerId, whId, "DN-0002", new DateTime(2026, 6, 8))
        {
            SalesOrderId = so2Id,
            NetTotal = 500m,
            TaxAmount = 30m,
            GrandTotal = 530m
        };
        dn2.AddItem(itemId, "Heavy pump", 1m, 500m, 30m, "Nos");
        dn2.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(new List<DeliveryNote> { dn1, dn2 }.AsQueryable());

        var filter = new DelayedOrderFilterDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            DelayedOnly = true
        };

        // Act
        var result = await _appService.GetReportAsync(filter);

        // Assert
        result.ShouldNotBeNull();
        result.TotalDeliveries.ShouldBe(2);
        result.DelayedDeliveries.ShouldBe(1);
        result.OnTimeDeliveryRate.ShouldBe(50m);
        result.AverageDelayDays.ShouldBe(4.0);
        result.TotalDelayedAmount.ShouldBe(1060m);

        result.Rows.Count.ShouldBe(1);
        var delayedRow = result.Rows[0];
        delayedRow.DeliveryNumber.ShouldBe("DN-0001");
        delayedRow.OrderNumber.ShouldBe("SO-0001");
        delayedRow.CustomerName.ShouldBe("Acme Corp");
        delayedRow.CustomerPoNumber.ShouldBe("PO-ACME-01");
        delayedRow.ExpectedDeliveryDate.ShouldBe(new DateTime(2026, 6, 1));
        delayedRow.ActualDeliveryDate.ShouldBe(new DateTime(2026, 6, 5));
        delayedRow.DelayedDays.ShouldBe(4);
        delayedRow.GrandTotal.ShouldBe(1060m);
        delayedRow.Items.Count.ShouldBe(1);
        delayedRow.Items[0].ItemCode.ShouldBe("ITEM-001");
    }

    [Fact]
    public async Task GetReportAsync_ThrowsIfDateRangeInvalid()
    {
        var filter = new DelayedOrderFilterDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 6, 10),
            ToDate = new DateTime(2026, 6, 1)
        };

        await Should.ThrowAsync<UserFriendlyException>(() => _appService.GetReportAsync(filter));
    }
}
