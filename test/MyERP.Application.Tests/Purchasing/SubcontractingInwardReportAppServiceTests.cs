using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
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

    [Fact]
    public async Task GetRawMaterialsToBeReceivedReport_CalculatesBOMRawMaterialsAndPerUnitProcessLoss()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        // SCIO with FG item qty = 10
        var order = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-RM-001", today, supplierId);
        order.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order.Id, fgItemId, quantity: 10m, rate: 100m));
        order.Submit();

        var orders = new List<SubcontractingInwardOrder> { order }.AsQueryable();
        var orderRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
        orderRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var fgItem = new Item(fgItemId, companyId, "FG-001", "Finished Good", ItemType.Goods) { Uom = "Nos" };
        var rmItem = new Item(rmItemId, companyId, "RM-001", "Raw Material Steel", ItemType.Goods) { Uom = "Kg" };
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { fgItem, rmItem }.AsQueryable()));

        var supplier = new Supplier(supplierId, companyId, "Subcontractor Supplier");
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));

        // Multi-unit BOM: Quantity = 2 produces FG from 4 Kg of RM (perUnitBomQty = 2)
        // With ProcessLossPercentage = 10%, FG process loss is 10 * 10% = 1 FG
        // Per ERPNext PR #59397: process_loss_qty = perUnitBomQty * fg_loss = 2 * 1 = 2 Kg
        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-FG-001", fgItemId)
        {
            Quantity = 2m,
            IsDefault = true,
            IsActive = true,
            ProcessLossPercentage = 10m
        };
        bom.AddItem(new BomItem(Guid.NewGuid(), bom.Id, rmItemId, "Raw Material Steel", quantity: 4m, rate: 25m));

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { bom }.AsQueryable()));

        var appService = new SubcontractingInwardReportAppService(orderRepo, itemRepo, supplierRepo, soRepo, customerRepo, bomRepo);

        var result = await appService.GetRawMaterialsToBeReceivedReportAsync(new SubcontractingInwardReportFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.OrderNumber.ShouldBe("SCIO-RM-001");
        row.FinishedGoodItemCode.ShouldBe("FG-001");
        row.RawMaterialItemCode.ShouldBe("RM-001");
        row.StockUom.ShouldBe("Kg");
        // RequiredQty = 10 * (4 / 2) = 20
        row.RequiredQty.ShouldBe(20m);
        // ProcessLossQty = (4 / 2) * (10 * 10%) = 2 (PR #59397!)
        row.ProcessLossQty.ShouldBe(2m);
        // PendingQty = 20 + 2 = 22
        row.PendingQty.ShouldBe(22m);
        row.ReceivedQty.ShouldBe(0m);
        row.ReturnedQty.ShouldBe(0m);

        result.TotalRequiredQty.ShouldBe(20m);
        result.TotalProcessLossQty.ShouldBe(2m);
        result.TotalPendingQty.ShouldBe(22m);
    }

    [Fact]
    public async Task GetRawMaterialsToBeReceivedReport_VariantItemFallsBackToTemplateBom()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var templateItemId = Guid.NewGuid();
        var variantItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var order = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-VAR-001", today, supplierId);
        order.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), order.Id, variantItemId, quantity: 5m, rate: 80m));
        order.Submit();

        var orders = new List<SubcontractingInwardOrder> { order }.AsQueryable();
        var orderRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
        orderRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var templateItem = new Item(templateItemId, companyId, "FG-TEMPLATE", "Template FG", ItemType.Goods) { Uom = "Nos" };
        var variantItem = new Item(variantItemId, companyId, "FG-VAR-RED", "Variant Red FG", ItemType.Goods)
        {
            Uom = "Nos",
            VariantOfId = templateItemId
        };
        var rmItem = new Item(rmItemId, companyId, "RM-RESIN", "Resin Raw Material", ItemType.Goods) { Uom = "Kg" };

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { templateItem, variantItem, rmItem }.AsQueryable()));

        var supplier = new Supplier(supplierId, companyId, "Plastics Subcontractor");
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { supplier }.AsQueryable()));

        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        soRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesOrder>().AsQueryable()));

        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer>().AsQueryable()));

        // Template item has the default BOM
        var templateBom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-TEMPLATE-001", templateItemId)
        {
            Quantity = 1m,
            IsDefault = true,
            IsActive = true
        };
        templateBom.AddItem(new BomItem(Guid.NewGuid(), templateBom.Id, rmItemId, "Resin Raw Material", quantity: 3m, rate: 10m));

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<BillOfMaterials, object>>[]>())
            .Returns(Task.FromResult(new List<BillOfMaterials> { templateBom }.AsQueryable()));

        var appService = new SubcontractingInwardReportAppService(orderRepo, itemRepo, supplierRepo, soRepo, customerRepo, bomRepo);

        var result = await appService.GetRawMaterialsToBeReceivedReportAsync(new SubcontractingInwardReportFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.FinishedGoodItemCode.ShouldBe("FG-VAR-RED");
        row.RawMaterialItemCode.ShouldBe("RM-RESIN");
        row.RequiredQty.ShouldBe(15m); // 5 * 3
        row.PendingQty.ShouldBe(15m);
    }

    [Fact]
    public async Task GetRawMaterialsToBeReceivedReport_ExcludesDraftAndClosedOrders()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var rmItemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var draftOrder = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-DRAFT", today, supplierId);
        draftOrder.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), draftOrder.Id, fgItemId, quantity: 10m, rate: 50m));

        var closedOrder = new SubcontractingInwardOrder(Guid.NewGuid(), companyId, "SCIO-CLOSED", today, supplierId);
        closedOrder.AddItem(new SubcontractingInwardOrderItem(Guid.NewGuid(), closedOrder.Id, fgItemId, quantity: 10m, rate: 50m));
        closedOrder.Submit();
        closedOrder.Close();

        var orders = new List<SubcontractingInwardOrder> { draftOrder, closedOrder }.AsQueryable();
        var orderRepo = Substitute.For<IRepository<SubcontractingInwardOrder, Guid>>();
        orderRepo.GetQueryableAsync().Returns(Task.FromResult(orders));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();

        var appService = new SubcontractingInwardReportAppService(orderRepo, itemRepo, supplierRepo, soRepo, customerRepo, bomRepo);

        var result = await appService.GetRawMaterialsToBeReceivedReportAsync(new SubcontractingInwardReportFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        result.Rows.ShouldBeEmpty();
    }
}
