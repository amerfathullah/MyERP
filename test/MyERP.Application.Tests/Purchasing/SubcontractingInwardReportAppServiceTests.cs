using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class SubcontractingInwardReportAppServiceTests
{
    [Fact]
    public async Task GetItemsToBeDeliveredReport_FiltersActiveOrdersAndComputesPendingQty()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId1 = Guid.NewGuid();
        var itemId2 = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // Order 1: Submitted / Open with 2 items (one fully delivered, one partially)
        var order1 = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-001", today, supplierId);
        order1.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order1.Id, itemId1, quantity: 10m, rate: 50m)
        {
            ReceivedQty = 4m // Pending: 6m
        });
        order1.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order1.Id, itemId2, quantity: 5m, rate: 30m)
        {
            ReceivedQty = 5m // Pending: 0m -> Should NOT appear in items to be delivered
        });
        order1.Submit();

        // Order 2: Draft order -> Should NOT appear in items to be delivered
        var order2 = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-DRAFT", today, supplierId);
        order2.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order2.Id, itemId1, quantity: 20m, rate: 50m));

        // Order 3: Closed order -> Should NOT appear
        var order3 = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-CLOSED", today, supplierId);
        order3.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order3.Id, itemId1, quantity: 15m, rate: 50m) { ReceivedQty = 10m });
        order3.Submit();
        order3.Close();

        var orders = new List<SubcontractingInwardOrder> { order1, order2, order3 }.AsQueryable();
        var orderRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
        orderRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var item1 = new Item(itemId1, companyId, "ITEM-01", "Finished Good 1", ItemType.Goods) { Uom = "Nos" };
        var item2 = new Item(itemId2, companyId, "ITEM-02", "Finished Good 2", ItemType.Goods) { Uom = "Nos" };
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item1, item2 }.AsQueryable()));

        var supplier = new Supplier(supplierId, companyId, "Subcontractor Supplier");
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));

        var appService = new SubcontractingInwardReportAppService(orderRepo, itemRepo, supplierRepo, soRepo, customerRepo);

        var result = await appService.GetItemsToBeDeliveredReportAsync(new SubcontractingInwardReportFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.OrderNumber.ShouldBe("SCIO-001");
        row.ItemCode.ShouldBe("ITEM-01");
        row.PartyName.ShouldBe("Subcontractor Supplier");
        row.OrderQty.ShouldBe(10m);
        row.DeliveredQty.ShouldBe(4m);
        row.PendingQty.ShouldBe(6m);

        result.TotalOrderQty.ShouldBe(10m);
        result.TotalDeliveredQty.ShouldBe(4m);
        result.TotalPendingQty.ShouldBe(6m);
    }

    [Fact]
    public async Task GetOrderSummaryReport_IncludesAllItemsAndCalculatesTotals()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var order = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-SUM-001", today, supplierId);
        order.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order.Id, itemId, quantity: 8m, rate: 100m)
        {
            ReceivedQty = 3m
        });
        order.Submit();

        var orders = new List<SubcontractingInwardOrder> { order }.AsQueryable();
        var orderRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
        orderRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var item = new Item(itemId, companyId, "FG-ITEM", "Finished Good Item", ItemType.Goods) { Uom = "Pcs" };
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var supplier = new Supplier(supplierId, companyId, "Machining Partner");
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));

        var appService = new SubcontractingInwardReportAppService(orderRepo, itemRepo, supplierRepo, soRepo, customerRepo);

        var result = await appService.GetOrderSummaryReportAsync(new SubcontractingInwardReportFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.OrderNumber.ShouldBe("SCIO-SUM-001");
        row.ItemCode.ShouldBe("FG-ITEM");
        row.OrderQty.ShouldBe(8m);
        row.DeliveredQty.ShouldBe(3m);
        row.PendingQty.ShouldBe(5m);
        row.Rate.ShouldBe(100m);
        row.Amount.ShouldBe(800m);

        result.TotalOrderQty.ShouldBe(8m);
        result.TotalDeliveredQty.ShouldBe(3m);
        result.TotalPendingQty.ShouldBe(5m);
        result.TotalAmount.ShouldBe(800m);
    }
}
