using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

namespace MyERP.Sales;

public class SalesPersonWiseTransactionSummaryAppServiceTests
{
    private readonly IRepository<SalesTeamEntry, Guid> _teamEntryRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Territory, Guid> _territoryRepo = Substitute.For<IRepository<Territory, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<SalesPerson, Guid> _salesPersonRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _territoryId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _salesPersonId = Guid.NewGuid();

    private readonly Company _company;
    private readonly Customer _customer;
    private readonly Territory _territory;
    private readonly Warehouse _warehouse;
    private readonly Item _item;
    private readonly SalesPerson _salesPerson;

    public SalesPersonWiseTransactionSummaryAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _territory = new Territory(_territoryId, "Penang");
        _territoryRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Territory> { _territory }.AsQueryable()));

        _customer = new Customer(_customerId, _companyId, "Northern Retailer")
        {
            CustomerCode = "CUST-NR",
            TerritoryId = _territoryId,
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _warehouse = new Warehouse(_warehouseId, _companyId, "Main Store");
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { _warehouse }.AsQueryable()));

        _item = new Item(_itemId, _companyId, "ITM-001", "Precision Sensor", ItemType.Goods)
        {
            ItemGroup = "Sensors",
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item }.AsQueryable()));

        _salesPerson = new SalesPerson(_salesPersonId, "David Chong") { CommissionRate = 8m };
        _salesPersonRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesPerson> { _salesPerson }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnEmpty_WhenNoTransactions()
    {
        // Arrange
        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice>().AsQueryable()));
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry>().AsQueryable()));

        var service = new SalesPersonWiseTransactionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _deliveryNoteRepo,
            _itemRepo,
            _customerRepo,
            _territoryRepo,
            _warehouseRepo,
            _salesPersonRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonWiseTransactionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
        result.TotalQuantity.ShouldBe(0);
        result.TotalNetAmount.ShouldBe(0);
        result.TotalContributionQuantity.ShouldBe(0);
        result.TotalContributionAmount.ShouldBe(0);
        result.TransactionCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnItemRows_ForSalesInvoice()
    {
        // Arrange
        var invoiceId = Guid.NewGuid();
        var invoice = new SalesInvoice(invoiceId, _companyId, _customerId, "SINV-2026-001", new DateTime(2026, 4, 10));
        invoice.AddItem(_itemId, "Precision Sensor", 10, 200m, 0); // NetTotal = 2000
        invoice.Submit();

        _salesInvoiceRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var teamEntry = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "SalesInvoice", invoiceId, 50m, 2000m, 8m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { teamEntry }.AsQueryable()));

        var service = new SalesPersonWiseTransactionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _deliveryNoteRepo,
            _itemRepo,
            _customerRepo,
            _territoryRepo,
            _warehouseRepo,
            _salesPersonRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonWiseTransactionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Invoice",
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        result.TransactionCount.ShouldBe(1);

        var row = result.Rows[0];
        row.TransactionNumber.ShouldBe("SINV-2026-001");
        row.DocType.ShouldBe("Sales Invoice");
        row.CustomerName.ShouldBe("Northern Retailer");
        row.TerritoryName.ShouldBe("Penang");
        row.ItemCode.ShouldBe("ITM-001");
        row.ItemName.ShouldBe("Precision Sensor");
        row.ItemGroup.ShouldBe("Sensors");
        row.Quantity.ShouldBe(10m);
        row.Rate.ShouldBe(200m);
        row.BaseNetAmount.ShouldBe(2000m);
        row.SalesPersonName.ShouldBe("David Chong");
        row.AllocatedPercentage.ShouldBe(50m);
        row.ContributionQuantity.ShouldBe(5m); // 10 * 50%
        row.ContributionAmount.ShouldBe(1000m); // 2000 * 50%

        result.TotalQuantity.ShouldBe(5m);
        result.TotalNetAmount.ShouldBe(1000m);
        result.TotalContributionQuantity.ShouldBe(5m);
        result.TotalContributionAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnItemRows_ForSalesOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new SalesOrder(orderId, _companyId, _customerId, "SO-2026-005", new DateTime(2026, 4, 11));
        order.AddItem(_itemId, "Precision Sensor", 4, 250m, 0); // NetTotal = 1000
        order.Submit();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        var teamEntry = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "SalesOrder", orderId, 100m, 1000m, 8m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { teamEntry }.AsQueryable()));

        var service = new SalesPersonWiseTransactionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _deliveryNoteRepo,
            _itemRepo,
            _customerRepo,
            _territoryRepo,
            _warehouseRepo,
            _salesPersonRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonWiseTransactionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Order",
        });

        // Assert
        result.ShouldNotBeNull();
        result.DocType.ShouldBe("Sales Order");
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].TransactionNumber.ShouldBe("SO-2026-005");
        result.Rows[0].ContributionQuantity.ShouldBe(4m);
        result.Rows[0].ContributionAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_ShouldReturnItemRows_ForDeliveryNote()
    {
        // Arrange
        var dnId = Guid.NewGuid();
        var dn = new DeliveryNote(dnId, _companyId, _customerId, _warehouseId, "DN-2026-003", new DateTime(2026, 4, 12));
        dn.AddItem(_itemId, "Precision Sensor", 20, 150m, 0); // NetTotal = 3000
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var teamEntry = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "DeliveryNote", dnId, 100m, 3000m, 8m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { teamEntry }.AsQueryable()));

        var service = new SalesPersonWiseTransactionSummaryAppService(
            _teamEntryRepo,
            _salesInvoiceRepo,
            _salesOrderRepo,
            _deliveryNoteRepo,
            _itemRepo,
            _customerRepo,
            _territoryRepo,
            _warehouseRepo,
            _salesPersonRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new GetSalesPersonWiseTransactionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Delivery Note",
        });

        // Assert
        result.ShouldNotBeNull();
        result.DocType.ShouldBe("Delivery Note");
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].TransactionNumber.ShouldBe("DN-2026-003");
        result.Rows[0].WarehouseName.ShouldBe("Main Store");
        result.Rows[0].ContributionQuantity.ShouldBe(20m);
        result.Rows[0].ContributionAmount.ShouldBe(3000m);
    }

    [Fact]
    public async Task GetReportAsync_ClosedSalesOrder_UsesDeliveredQtyAndAvoidsLineDoubleCounting()
    {
        // Per ERPNext PR #61280 (commit d07288a757):
        // 1. Closed Sales Orders use DeliveredQty for item qty and net amount.
        // 2. When multiple sales persons share an item row, Quantity and BaseNetAmount appear once on the first row.
        var sp2Id = Guid.NewGuid();
        var sp2 = new SalesPerson(sp2Id, "Evelyn Lim") { CommissionRate = 5m };
        _salesPersonRepo.GetQueryableAsync().Returns(Task.FromResult(
            new List<SalesPerson> { _salesPerson, sp2 }.AsQueryable()));

        var orderId = Guid.NewGuid();
        var order = new SalesOrder(orderId, _companyId, _customerId, "SO-CLOSED-001", new DateTime(2026, 4, 15));
        order.AddItem(_itemId, "Precision Sensor", 10, 200m, 0); // Ordered 10, but only 4 delivered before Close
        order.Items.First().DeliveredQty = 4m;
        order.Submit();
        order.Close();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        var entry1 = new SalesTeamEntry(Guid.NewGuid(), _salesPersonId, "SalesOrder", orderId, 50m, 800m, 8m);
        var entry2 = new SalesTeamEntry(Guid.NewGuid(), sp2Id, "SalesOrder", orderId, 50m, 800m, 5m);
        _teamEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<SalesTeamEntry> { entry1, entry2 }.AsQueryable()));

        var service = new SalesPersonWiseTransactionSummaryAppService(
            _teamEntryRepo, _salesInvoiceRepo, _salesOrderRepo, _deliveryNoteRepo,
            _itemRepo, _customerRepo, _territoryRepo, _warehouseRepo, _salesPersonRepo, _companyRepo);

        var result = await service.GetReportAsync(new GetSalesPersonWiseTransactionSummaryInput
        {
            CompanyId = _companyId,
            DocType = "Sales Order",
        });

        result.Rows.Count.ShouldBe(2);
        result.Rows.Sum(r => r.Quantity).ShouldBe(4m); // Shown once (4 + 0), not double-counted (4 + 4)
        result.Rows.Sum(r => r.BaseNetAmount).ShouldBe(800m); // 4 * 200 = 800 shown once
        result.TotalContributionQuantity.ShouldBe(4m); // 2 + 2
        result.TotalContributionAmount.ShouldBe(800m); // 400 + 400
    }
}
