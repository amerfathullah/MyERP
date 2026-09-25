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

/// <summary>
/// Unit tests for SalesOrderAnalysisAppService.
/// Implements ERPNext selling/report/sales_order_analysis (PR #59236 / commit 30e0382aa9).
/// </summary>
public class SalesOrderAnalysisReportTests
{
    private readonly IRepository<SalesOrder, Guid> _soRepo;
    private readonly IRepository<Customer, Guid> _customerRepo;
    private readonly IRepository<Item, Guid> _itemRepo;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly SalesOrderAnalysisAppService _service;

    public SalesOrderAnalysisReportTests()
    {
        _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        _itemRepo = Substitute.For<IRepository<Item, Guid>>();
        _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        _companyRepo = Substitute.For<IRepository<Company, Guid>>();

        _service = new SalesOrderAnalysisAppService(
            _soRepo, _customerRepo, _itemRepo, _warehouseRepo, _companyRepo);
    }

    [Fact]
    public async Task GetAnalysisAsync_CannotCombineGroupBySoAndGroupByItem()
    {
        var input = new GetSalesOrderAnalysisDto
        {
            CompanyId = Guid.NewGuid(),
            GroupBySo = true,
            GroupByItem = true
        };

        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetAnalysisAsync(input));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GetAnalysisAsync_DetailedRows_CalculatesQuantitiesAndAmounts()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);
        var deliveryDate = new DateTime(2026, 9, 10);

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0001", orderDate)
        {
            DeliveryDate = deliveryDate
        };
        so.AddItem(itemId, "Test Item Description", quantity: 10, unitPrice: 100, taxAmount: 0, uom: "Nos");
        so.Items[0].DeliveredQty = 4;
        so.Items[0].BilledQty = 3;
        so.Submit();

        SetupRepositories(companyId, [so], [(customerId, "Customer A")], [(itemId, "ITEM-001", "Test Item")]);

        var result = await _service.GetAnalysisAsync(new GetSalesOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SalesOrderNumber.ShouldBe("SO-0001");
        row.CustomerName.ShouldBe("Customer A");
        row.ItemCode.ShouldBe("ITEM-001");
        row.Uom.ShouldBe("Nos");
        row.Qty.ShouldBe(10);
        row.DeliveredQty.ShouldBe(4);
        row.PendingQty.ShouldBe(6); // 10 - 4
        row.BilledQty.ShouldBe(3);
        row.QtyToBill.ShouldBe(7); // 10 - 3
        row.Amount.ShouldBe(1000);
        row.DeliveredQtyAmount.ShouldBe(400);
        row.BilledAmount.ShouldBe(300);
        row.PendingAmount.ShouldBe(600); // 6 * 100
        row.DeliveryDate.ShouldBe(deliveryDate);

        result.TotalAmount.ShouldBe(1000);
        result.TotalBilledAmount.ShouldBe(300);
        result.TotalAmountToBill.ShouldBe(600);
        result.TotalQty.ShouldBe(10);
        result.TotalDeliveredQty.ShouldBe(4);
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupBySo_AggregatesFieldsAndDelay()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var so = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0002", orderDate);
        so.AddItem(item1Id, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        so.AddItem(item2Id, "Item 2", quantity: 5, unitPrice: 200, taxAmount: 0, uom: "Nos");
        so.Items[0].DeliveredQty = 2;
        so.Items[0].BilledQty = 2;
        so.Items[1].DeliveredQty = 1;
        so.Items[1].BilledQty = 0;
        so.Submit();

        SetupRepositories(companyId, [so], [(customerId, "Customer A")],
            [(item1Id, "ITEM-001", "Item 1"), (item2Id, "ITEM-002", "Item 2")]);

        var result = await _service.GetAnalysisAsync(new GetSalesOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupBySo = true
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SalesOrderNumber.ShouldBe("SO-0002");
        row.Qty.ShouldBe(15); // 10 + 5
        row.DeliveredQty.ShouldBe(3); // 2 + 1
        row.PendingQty.ShouldBe(12); // 8 + 4
        row.BilledQty.ShouldBe(2); // 2 + 0
        row.QtyToBill.ShouldBe(13); // 8 + 5
        row.Amount.ShouldBe(1500); // 500 + 1000
        row.DeliveredQtyAmount.ShouldBe(300); // 100 + 200
        row.BilledAmount.ShouldBe(100); // 100 + 0
        row.PendingAmount.ShouldBe(1200); // 400 + 800
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupByItem_AggregatesAcrossSalesOrdersForSameUom()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var so1 = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0003", orderDate);
        so1.AddItem(itemId, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        so1.Items[0].DeliveredQty = 3;
        so1.Submit();

        var so2 = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0004", orderDate);
        so2.AddItem(itemId, "Item 1", quantity: 4, unitPrice: 50, taxAmount: 0, uom: "Nos");
        so2.Items[0].DeliveredQty = 0;
        so2.Submit();

        SetupRepositories(companyId, [so1, so2], [(customerId, "Customer A")], [(itemId, "ITEM-001", "Item 1")]);

        var result = await _service.GetAnalysisAsync(new GetSalesOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupByItem = true
        });

        // Combined into single item row
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.ItemCode.ShouldBe("ITEM-001");
        row.Uom.ShouldBe("Nos");
        row.Qty.ShouldBe(14); // 10 + 4
        row.DeliveredQty.ShouldBe(3);
        row.PendingQty.ShouldBe(11);
        row.Amount.ShouldBe(700); // 500 + 200
    }

    [Fact]
    public async Task GetAnalysisAsync_GroupByItem_KeepsDifferentUomsApart()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var orderDate = new DateTime(2026, 9, 1);

        var so1 = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0005", orderDate);
        so1.AddItem(itemId, "Item 1", quantity: 10, unitPrice: 50, taxAmount: 0, uom: "Nos");
        so1.Submit();

        var so2 = new SalesOrder(Guid.NewGuid(), companyId, customerId, "SO-0006", orderDate);
        so2.AddItem(itemId, "Item 1", quantity: 2, unitPrice: 500, taxAmount: 0, uom: "Box");
        so2.Submit();

        SetupRepositories(companyId, [so1, so2], [(customerId, "Customer A")], [(itemId, "ITEM-001", "Item 1")]);

        var result = await _service.GetAnalysisAsync(new GetSalesOrderAnalysisDto
        {
            CompanyId = companyId,
            FromDate = orderDate.AddDays(-1),
            ToDate = orderDate.AddDays(1),
            GroupByItem = true
        });

        // Keeps "Box" and "Nos" apart per ERPNext PR #59236
        result.Rows.Count.ShouldBe(2);
        result.Rows.Any(r => r.Uom == "Box" && r.Qty == 2).ShouldBeTrue();
        result.Rows.Any(r => r.Uom == "Nos" && r.Qty == 10).ShouldBeTrue();
    }

    private void SetupRepositories(
        Guid companyId,
        List<SalesOrder> orders,
        List<(Guid Id, string Name)> customers,
        List<(Guid Id, string ItemCode, string ItemName)> items)
    {
        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(orders.AsQueryable()));

        var customerEntities = customers.Select(c =>
        {
            var cust = new Customer(c.Id, companyId, c.Name);
            return cust;
        }).AsQueryable();
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(customerEntities));

        var itemEntities = items.Select(i =>
        {
            var itm = new Item(i.Id, companyId, i.ItemCode, i.ItemName, ItemType.Goods);
            return itm;
        }).AsQueryable();
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(itemEntities));

        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        var company = new Company(companyId, "Test Company");
        _companyRepo.FindAsync(companyId).Returns(Task.FromResult<Company?>(company));
    }
}
