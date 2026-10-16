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
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class ItemSalesAppServiceTests
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<CustomerGroup, Guid> _customerGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _customerGroupId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();

    private readonly Company _company;
    private readonly Customer _customer;
    private readonly CustomerGroup _customerGroup;
    private readonly Item _item;
    private readonly ItemGroup _itemGroup;

    public ItemSalesAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customerGroup = new CustomerGroup(_customerGroupId, "Commercial");
        _customerGroupRepo.GetListAsync().Returns(Task.FromResult(new List<CustomerGroup> { _customerGroup }));

        _customer = new Customer(_customerId, _companyId, "Apex Retail")
        {
            CustomerCode = "CUST-APEX",
            CustomerGroupId = _customerGroupId,
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _itemGroup = new ItemGroup(_itemGroupId, "Electronics");
        _itemGroupRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ItemGroup> { _itemGroup }.AsQueryable()));

        _item = new Item(_itemId, _companyId, "ITM-001", "Smartphone X", ItemType.Goods)
        {
            ItemGroup = "Electronics",
            ItemGroupId = _itemGroupId,
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_ShouldAggregateItemSalesFromPostedInvoices()
    {
        // Arrange
        var invoice = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-001", DateTime.UtcNow);
        invoice.AddItem(_itemId, "Smartphone X", 5, 2000m, 0);
        invoice.Submit();
        invoice.Post();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invoice }.AsQueryable()));

        var service = new ItemSalesAppService(
            _invoiceRepo,
            _salesOrderRepo,
            _customerRepo,
            _customerGroupRepo,
            _itemRepo,
            _itemGroupRepo,
            _companyRepo);

        // Act
        var result = await service.GetReportAsync(new RegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = DateTime.UtcNow.AddDays(-10),
            ToDate = DateTime.UtcNow.AddDays(1),
        });

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.Items[0].ItemId.ShouldBe(_itemId);
        result.Items[0].TotalQty.ShouldBe(5m);
        result.Items[0].TotalRevenue.ShouldBe(10000m);
        result.Items[0].AverageRate.ShouldBe(2000m);
        result.TotalRevenue.ShouldBe(10000m);
        result.TotalQty.ShouldBe(5m);
    }

    [Fact]
    public async Task GetHistoryReportAsync_ShouldThrow_WhenFromDateGreaterThanToDate()
    {
        // Arrange
        var service = new ItemSalesAppService(
            _invoiceRepo,
            _salesOrderRepo,
            _customerRepo,
            _customerGroupRepo,
            _itemRepo,
            _itemGroupRepo,
            _companyRepo);

        // Act & Assert (ERPNext PR #60350 commit 326663e9a0 guard)
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await service.GetHistoryReportAsync(new GetItemWiseSalesHistoryDto
            {
                CompanyId = _companyId,
                FromDate = new DateTime(2026, 5, 10),
                ToDate = new DateTime(2026, 5, 1),
            });
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetHistoryReportAsync_ShouldReturnItemWiseSalesHistory_FromSalesOrders()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new SalesOrder(orderId, _companyId, _customerId, "SO-2026-100", new DateTime(2026, 4, 15));
        order.AddItem(_itemId, "Smartphone X description", 10, 1500m, 0, "Unit");
        order.Submit();

        _salesOrderRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { order }.AsQueryable()));

        var service = new ItemSalesAppService(
            _invoiceRepo,
            _salesOrderRepo,
            _customerRepo,
            _customerGroupRepo,
            _itemRepo,
            _itemGroupRepo,
            _companyRepo);

        // Act
        var result = await service.GetHistoryReportAsync(new GetItemWiseSalesHistoryDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 4, 1),
            ToDate = new DateTime(2026, 4, 30),
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITM-001");
        row.ItemName.ShouldBe("Smartphone X");
        row.ItemGroup.ShouldBe("Electronics");
        row.Quantity.ShouldBe(10m);
        row.Rate.ShouldBe(1500m);
        row.Amount.ShouldBe(15000m);
        row.SalesOrderNumber.ShouldBe("SO-2026-100");
        row.CustomerName.ShouldBe("Apex Retail");
        row.CustomerGroup.ShouldBe("Commercial");

        result.TotalQty.ShouldBe(10m);
        result.TotalAmount.ShouldBe(15000m);
    }
}
