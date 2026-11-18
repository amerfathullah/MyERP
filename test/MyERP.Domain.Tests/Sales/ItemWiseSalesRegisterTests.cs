using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class ItemWiseSalesRegisterTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<Customer, Guid> _custRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<CustomerGroup, Guid> _custGroupRepo = Substitute.For<IRepository<CustomerGroup, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<DeliveryNote, Guid> _dnRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ItemGroup, Guid> _itemGroupRepo = Substitute.For<IRepository<ItemGroup, Guid>>();

    private readonly SalesRegisterAppService _service;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemWiseSalesRegisterTests()
    {
        _service = new SalesRegisterAppService(
            _siRepo, _peRepo, _custRepo, _custGroupRepo, _journalRepo,
            _soRepo, _dnRepo, _itemRepo, _itemGroupRepo);
    }

    [Fact]
    public async Task GetItemWiseReport_ResolvesOrderDeliveryGroupsAndMultiCurrency()
    {
        var parentCustGroupId = Guid.NewGuid();
        var childCustGroupId = Guid.NewGuid();
        var parentCustGroup = new CustomerGroup(parentCustGroupId, "All Customers", isGroup: true);
        var childCustGroup = new CustomerGroup(childCustGroupId, "Enterprise", parentId: parentCustGroupId);
        _custGroupRepo.GetListAsync().Returns(Task.FromResult(new List<CustomerGroup> { parentCustGroup, childCustGroup }));

        var customer = new Customer(Guid.NewGuid(), _companyId, "Acme Holdings")
        {
            CustomerGroupId = childCustGroupId,
        };
        _custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));

        var parentItemGroupId = Guid.NewGuid();
        var childItemGroupId = Guid.NewGuid();
        var parentItemGroup = new ItemGroup(parentItemGroupId, "Products", isGroup: true);
        var childItemGroup = new ItemGroup(childItemGroupId, "Electronics", parentId: parentItemGroupId);
        _itemGroupRepo.GetListAsync().Returns(Task.FromResult(new List<ItemGroup> { parentItemGroup, childItemGroup }));

        var itemA = new Item(Guid.NewGuid(), _companyId, "SKU-100", "Widget Pro", ItemType.Goods)
        {
            ItemGroup = "Electronics",
            ItemGroupId = childItemGroupId,
        };
        var itemB = new Item(Guid.NewGuid(), _companyId, "SKU-200", "Consulting", ItemType.Service)
        {
            ItemGroup = "Services",
            ItemGroupId = Guid.NewGuid(),
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemA, itemB }.AsQueryable()));

        var warehouseId = Guid.NewGuid();
        var so = new SalesOrder(Guid.NewGuid(), _companyId, customer.Id, "SO-001", new DateTime(2026, 11, 1));
        so.AddItem(itemA.Id, "Widget Pro", 2m, 50m, 0m);
        var soItemId = so.Items.First().Id;
        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, customer.Id, warehouseId, "DN-001", new DateTime(2026, 11, 2));
        dn.AddItem(itemA.Id, "Widget Pro", 2m, 50m, 0m, salesOrderItemId: soItemId);
        dn.Submit();
        var dnItemId = dn.Items.First().Id;
        _dnRepo.WithDetailsAsync(Arg.Any<Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var si = new SalesInvoice(Guid.NewGuid(), _companyId, customer.Id, "SINV-001", new DateTime(2026, 11, 5))
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
        };
        si.AddItem(itemA.Id, "Widget Pro", 2m, 50m, 6m, uom: "Box");
        si.Items.Last().ConversionFactor = 10m;
        si.Items.Last().StockUom = "Unit";
        si.Items.Last().SalesOrderItemId = soItemId;
        si.Items.Last().DeliveryNoteItemId = dnItemId;
        si.AddItem(itemB.Id, "Consulting", 1m, 100m, 0m);
        si.Submit();
        si.Post();

        _siRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        var report = await _service.GetItemWiseReportAsync(new ItemWiseSalesRegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 30),
            CustomerGroupId = parentCustGroupId,
            ItemGroupId = parentItemGroupId,
        });

        Assert.Equal(1, report.Count);
        var row = Assert.Single(report.Items);
        Assert.Equal("SINV-001", row.InvoiceNumber);
        Assert.Equal("Acme Holdings", row.CustomerName);
        Assert.Equal("Enterprise", row.CustomerGroupName);
        Assert.Equal("SKU-100", row.ItemCode);
        Assert.Equal(20m, row.StockQty);
        Assert.Equal("Unit", row.StockUom);
        Assert.Equal(22.50m, row.Rate); // (50 / 10) * 4.5
        Assert.Equal(450m, row.Amount); // (2 * 50) * 4.5
        Assert.Equal(27m, row.TotalTax); // 6 * 4.5
        Assert.Equal(477m, row.Total);
        Assert.Equal("SO-001", row.SalesOrderNumber);
        Assert.Equal("DN-001", row.DeliveryNoteNumber);
        Assert.Equal("USD", row.Currency);
    }

    [Fact]
    public async Task GetItemWiseReport_ResolvesDeliveryNoteViaSalesOrderItemFallback()
    {
        var customer = new Customer(Guid.NewGuid(), _companyId, "Beta Corp");
        _custRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { customer }.AsQueryable()));
        _custGroupRepo.GetListAsync().Returns(Task.FromResult(new List<CustomerGroup>()));

        var item = new Item(Guid.NewGuid(), _companyId, "SKU-300", "Bolt", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var so = new SalesOrder(Guid.NewGuid(), _companyId, customer.Id, "SO-099", new DateTime(2026, 11, 1));
        so.AddItem(item.Id, "Bolt", 5m, 10m, 0m);
        var soItemId = so.Items.First().Id;
        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, customer.Id, Guid.NewGuid(), "DN-099", new DateTime(2026, 11, 3));
        dn.AddItem(item.Id, "Bolt", 5m, 10m, 0m, salesOrderItemId: soItemId);
        dn.Submit();
        _dnRepo.WithDetailsAsync(Arg.Any<Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var si = new SalesInvoice(Guid.NewGuid(), _companyId, customer.Id, "SINV-099", new DateTime(2026, 11, 4));
        si.AddItem(item.Id, "Bolt", 5m, 10m, 3m);
        si.Items.Last().SalesOrderItemId = soItemId;
        si.Submit();
        si.Post();
        _siRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        var report = await _service.GetItemWiseReportAsync(new ItemWiseSalesRegisterFilterDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 30),
            ItemCode = "sku-300",
        });

        var row = Assert.Single(report.Items);
        Assert.Equal("SO-099", row.SalesOrderNumber);
        Assert.Equal("DN-099", row.DeliveryNoteNumber);
        Assert.Equal(50m, report.TotalNet);
        Assert.Equal(3m, report.TotalTax);
        Assert.Equal(53m, report.TotalGrand);
    }
}
