using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class BilledItemsToBeReceivedAppServiceTests
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId1 = Guid.NewGuid();
    private readonly Guid _supplierId2 = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemIdNonStock = Guid.NewGuid();

    private readonly Company _company;
    private readonly Supplier _supplier1;
    private readonly Supplier _supplier2;
    private readonly Item _item1;
    private readonly Item _itemNonStock;

    public BilledItemsToBeReceivedAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Manufacturing") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier1 = new Supplier(_supplierId1, _companyId, "Premier Steel Sdn Bhd") { SupplierCode = "SUPP-001" };
        _supplier2 = new Supplier(_supplierId2, _companyId, "Apex Tools") { SupplierCode = "SUPP-002" };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier1, _supplier2 }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "RAW-001", "Steel Pipe", ItemType.Goods)
        {
            MaintainStock = true
        };
        _itemNonStock = new Item(_itemIdNonStock, _companyId, "SERV-002", "Consulting", ItemType.Service)
        {
            MaintainStock = false
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item1, _itemNonStock }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_PendingReceiptItems_CalculatesAmountsCorrectly()
    {
        // Invoice 1: 10 billed, 4 received -> 6 pending
        var inv = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-2026-001", new DateTime(2026, 1, 15))
        {
            UpdateStock = false,
            ExchangeRate = 1m
        };
        inv.AddItem(_itemId1, "Steel Pipe Grade A", 10m, 50m, 0m, "PCS");
        inv.Items[0].ReceivedQty = 4m;
        inv.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv }.AsQueryable()));

        var appService = new BilledItemsToBeReceivedAppService(_invoiceRepo, _supplierRepo, _itemRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetBilledItemsToBeReceivedInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.InvoiceNumber.ShouldBe("PINV-2026-001");
        row.SupplierName.ShouldBe("Premier Steel Sdn Bhd");
        row.ItemCode.ShouldBe("RAW-001");
        row.InvoicedQty.ShouldBe(10m);
        row.ReceivedQty.ShouldBe(4m);
        row.PendingQty.ShouldBe(6m);
        row.Rate.ShouldBe(50m);
        row.Amount.ShouldBe(500m);
        row.ReceivedAmount.ShouldBe(200m);
        row.PendingAmount.ShouldBe(300m);

        result.TotalAmount.ShouldBe(500m);
        result.TotalReceivedAmount.ShouldBe(200m);
        result.TotalPendingAmount.ShouldBe(300m);
    }

    [Fact]
    public async Task GetReportAsync_IgnoresUpdateStockOrFullyReceived()
    {
        // Invoice with UpdateStock=true (stock was updated at billing time)
        var invStock = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-STOCK", new DateTime(2026, 1, 10))
        {
            UpdateStock = true
        };
        invStock.AddItem(_itemId1, "Steel Pipe", 10m, 50m, 0m);
        invStock.Submit();

        // Invoice fully received (ReceivedQty == Quantity)
        var invReceived = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-FULL", new DateTime(2026, 1, 10))
        {
            UpdateStock = false
        };
        invReceived.AddItem(_itemId1, "Steel Pipe", 5m, 50m, 0m, receivedQty: 5m);
        invReceived.Submit();

        // Non-stock item invoice
        var invNonStock = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-SERVICE", new DateTime(2026, 1, 10))
        {
            UpdateStock = false
        };
        invNonStock.AddItem(_itemIdNonStock, "Consulting", 1m, 1000m, 0m);
        invNonStock.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { invStock, invReceived, invNonStock }.AsQueryable()));

        var appService = new BilledItemsToBeReceivedAppService(_invoiceRepo, _supplierRepo, _itemRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new GetBilledItemsToBeReceivedInput
        {
            CompanyId = _companyId,
            PostingDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(0);
        result.TotalPendingAmount.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_FilterBySupplierAndInvoice_RestrictsResults()
    {
        var inv1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-001", new DateTime(2026, 1, 10))
        {
            UpdateStock = false
        };
        inv1.AddItem(_itemId1, "Steel Pipe", 10m, 50m, 0m);
        inv1.Items[0].ReceivedQty = 2m;
        inv1.Submit();

        var inv2 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId2, "PINV-002", new DateTime(2026, 1, 10))
        {
            UpdateStock = false
        };
        inv2.AddItem(_itemId1, "Steel Pipe", 20m, 50m, 0m);
        inv2.Items[0].ReceivedQty = 5m;
        inv2.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv1, inv2 }.AsQueryable()));

        var appService = new BilledItemsToBeReceivedAppService(_invoiceRepo, _supplierRepo, _itemRepo, _companyRepo);

        // Act: Filter by Supplier 1
        var result = await appService.GetReportAsync(new GetBilledItemsToBeReceivedInput
        {
            CompanyId = _companyId,
            SupplierId = _supplierId1,
            PostingDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].PurchaseInvoiceId.ShouldBe(inv1.Id);
    }
}
