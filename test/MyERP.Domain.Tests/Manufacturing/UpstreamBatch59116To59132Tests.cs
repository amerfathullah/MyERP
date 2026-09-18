using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59116To59132Tests
{
    // --- PR #59129: Job Card Secondary Items & Deterministic Collation ---

    [Fact]
    public void JobCard_AddSecondaryItem_AddsItemWithIncrementedIndex()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, 1);
        var itemId = Guid.NewGuid();

        var item1 = jc.AddSecondaryItem(itemId, "Scrap Metal", 5m, "Kg", SecondaryItemType.Scrap);
        var item2 = jc.AddSecondaryItem(itemId, "Scrap Metal", 3m, "Kg", SecondaryItemType.Scrap);

        jc.SecondaryItems.Count.ShouldBe(2);
        item1.Idx.ShouldBe(1);
        item2.Idx.ShouldBe(2);
        item1.StockQty.ShouldBe(5m);
        item2.StockQty.ShouldBe(3m);
    }

    [Fact]
    public async Task WorkOrderProductionService_GetSecondaryItemsFromJobCards_PicksRepresentativeFromFirstLineByIdx()
    {
        var woId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var scrapItemId = Guid.NewGuid();

        var jc1 = new JobCard(Guid.NewGuid(), companyId, woId, operationId, 5m, 1);
        jc1.Start();
        jc1.AddTimeLog(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), 5m);
        jc1.Complete();
        // Line 1: earlier idx holds "Box"
        jc1.AddSecondaryItem(scrapItemId, "Scrap A", 5m, "Box", SecondaryItemType.Scrap, idx: 1);

        var jc2 = new JobCard(Guid.NewGuid(), companyId, woId, operationId, 5m, 2);
        jc2.Start();
        jc2.AddTimeLog(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, 5m);
        jc2.Complete();
        // Line 2: higher idx holds "Nos" (alphabetically later, but should not win over idx 1)
        jc2.AddSecondaryItem(scrapItemId, "Scrap A Renamed", 3m, "Nos", SecondaryItemType.Scrap, idx: 2);

        var jcRepo = Substitute.For<IRepository<JobCard, Guid>>();
        var jcList = new List<JobCard> { jc1, jc2 };
        jcRepo.GetQueryableAsync().Returns(Task.FromResult(jcList.AsQueryable()));

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        var service = new WorkOrderProductionService(woRepo, jcRepo);

        var result = await service.GetSecondaryItemsFromJobCardsAsync(woId);

        result.Count.ShouldBe(1);
        var aggregated = result[0];
        aggregated.StockQty.ShouldBe(8m);
        // Must come from representative line (idx 1), not "Nos" or "Scrap A Renamed"
        aggregated.StockUom.ShouldBe("Box");
        aggregated.ItemName.ShouldBe("Scrap A");
    }

    // --- PR #59116: BOM Stock Analysis Warehouse Filter with Zero-Stock Components ---

    [Fact]
    public async Task BomStockAnalysis_WithWarehouseFilter_RetainsComponentsWithNoStock()
    {
        var bomId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var targetWarehouseId = Guid.NewGuid();

        var bom = new BillOfMaterials(bomId, companyId, "BOM-001", fgItemId) { Quantity = 1m };
        bom.AddItem(new BomItem(Guid.NewGuid(), bomId, item1Id, "Component 1", 2m, 10m, "Unit"));
        bom.AddItem(new BomItem(Guid.NewGuid(), bomId, item2Id, "Component 2 (Out of stock)", 3m, 20m, "Unit"));

        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        bomRepo.GetAsync(bomId).Returns(Task.FromResult(bom));

        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        // Bin exists only for item1 in targetWarehouseId
        var bin1 = new Bin(Guid.NewGuid(), item1Id, targetWarehouseId);
        bin1.UpdateActualQty(100m, 1000m);
        var bins = new List<Bin> { bin1 };
        binRepo.GetQueryableAsync().Returns(Task.FromResult(bins.AsQueryable()));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var item1 = new Item(item1Id, companyId, "COMP-1", "Component 1", ItemType.Goods);
        var item2 = new Item(item2Id, companyId, "COMP-2", "Component 2 (Out of stock)", ItemType.Goods);
        var fgItem = new Item(fgItemId, companyId, "FG-1", "Finished Good", ItemType.Goods);

        var items = new List<Item> { item1, item2 };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(items.AsQueryable()));
        itemRepo.FindAsync(fgItemId).Returns(Task.FromResult<Item?>(fgItem));

        var appService = new BomStockAnalysisAppService(bomRepo, binRepo, itemRepo);

        var analysis = await appService.GetAnalysisAsync(bomId, requiredQty: 1, warehouseId: targetWarehouseId);

        analysis.Materials.Count.ShouldBe(2);
        var comp1 = analysis.Materials.First(m => m.ItemId == item1Id);
        var comp2 = analysis.Materials.First(m => m.ItemId == item2Id);

        comp1.AvailableQty.ShouldBe(100m);
        comp1.IsSufficient.ShouldBeTrue();

        // Component 2 must be retained in analysis with 0 available stock and shortage
        comp2.AvailableQty.ShouldBe(0m);
        comp2.Shortage.ShouldBe(3m);
        comp2.IsSufficient.ShouldBeFalse();
        analysis.AllMaterialsSufficient.ShouldBeFalse();
        analysis.CanManufactureQty.ShouldBe(0m);
    }

    // --- PR #59130: Sales Payment Summary Earliest Invoice Label Tie-Breaker ---

    [Fact]
    public async Task SalesPaymentSummary_PicksLabelsFromEarliestInvoice()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var si1 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "INV-001", today)
        {
            IsPos = true,
            WarehouseId = warehouseId,
            CreationTime = today.AddHours(8) // earlier invoice
        };
        si1.AddItem(Guid.NewGuid(), "Item 1", 1m, 100m, 10m);
        si1.AmountPaid = 110m;
        si1.Submit();
        si1.Post();

        var si2 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "INV-002", today)
        {
            IsPos = true,
            WarehouseId = warehouseId,
            CreationTime = today.AddHours(10) // later invoice
        };
        si2.AddItem(Guid.NewGuid(), "Item 2", 2m, 100m, 20m);
        si2.AmountPaid = 220m;
        si2.Submit();
        si2.Post();

        var invoices = new List<SalesInvoice> { si2, si1 }; // unordered input
        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(invoices.AsQueryable()));

        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var whList = new List<Warehouse> { new Warehouse(warehouseId, companyId, "Stores") };
        whRepo.GetQueryableAsync().Returns(Task.FromResult(whList.AsQueryable()));

        var posProfileRepo = Substitute.For<IRepository<PosProfile, Guid>>();
        posProfileRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PosProfile, bool>>>())
            .Returns(Task.FromResult(new List<PosProfile>()));

        var peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
        var pe1 = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive, today, 110m, Guid.NewGuid(), Guid.NewGuid())
        {
            ModeOfPayment = "Bank Card",
            AgainstOrderId = si1.Id
        };
        pe1.Submit();
        pe1.Post();

        var pe2 = new PaymentEntry(Guid.NewGuid(), companyId, PaymentType.Receive, today, 220m, Guid.NewGuid(), Guid.NewGuid())
        {
            ModeOfPayment = "Cash",
            AgainstOrderId = si2.Id
        };
        pe2.Submit();
        pe2.Post();

        var peList = new List<PaymentEntry> { pe1, pe2 };
        peRepo.GetQueryableAsync().Returns(Task.FromResult(peList.AsQueryable()));

        var appService = new SalesPaymentSummaryAppService(invoiceRepo, whRepo, posProfileRepo, peRepo);

        var report = await appService.GetReportAsync(new SalesPaymentSummaryFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.NetTotal.ShouldBe(300m);
        row.TotalTaxes.ShouldBe(30m);
        // ModeOfPayment must come from the earliest invoice (si1 -> Bank Card), not Cash
        row.ModeOfPayment.ShouldBe("Bank Card");
    }

    // --- PR #59132: Requested Items To Order & Receive Paired UOM Resolution ---

    [Fact]
    public async Task RequestedItemsToOrderAndReceive_PicksUomAndStockUomAsPairFromFirstLine()
    {
        var companyId = Guid.NewGuid();
        var mrId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        var mr = new MaterialRequest(mrId, companyId, "MR-001", MaterialRequestType.Purchase, today);
        mr.AddItem(itemId, "Steel Bar", 10m, "Box", conversionFactor: 10m);
        mr.Items[0].OrderedQuantity = 5m;
        mr.Items[0].ReceivedQuantity = 2m;

        mr.AddItem(itemId, "Steel Bar", 20m, "Kg", conversionFactor: 1m);
        mr.Items[1].OrderedQuantity = 10m;
        mr.Items[1].ReceivedQuantity = 5m;

        mr.Submit();

        var mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
        mrRepo.GetQueryableAsync().Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var itemEntity = new Item(itemId, companyId, "STL-01", "Steel Bar", ItemType.Goods) { Uom = "Box" };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { itemEntity }.AsQueryable()));

        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        whRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse>().AsQueryable()));

        var appService = new RequestedItemsToOrderAndReceiveAppService(mrRepo, itemRepo, whRepo);

        var report = await appService.GetReportAsync(new RequestedItemsFilterDto
        {
            CompanyId = companyId,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.Qty.ShouldBe(30m);
        // Uom must come from first line
        row.Uom.ShouldBe("Box");
        row.StockUom.ShouldBe("Box");
    }

    // --- PR #59120: Sales Order Cancel Resets OrderedQty ---

    [Fact]
    public void SalesOrder_Cancel_ResetsOrderedQtyOnAllItems()
    {
        var order = new SalesOrder(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SO-001", DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Item 1", 10m, 100m, 0m, "Unit");
        order.Items[0].OrderedQty = 10m;
        order.AddItem(Guid.NewGuid(), "Item 2", 5m, 50m, 0m, "Unit");
        order.Items[1].OrderedQty = 5m;

        order.Submit();
        order.Cancel();

        order.Status.ShouldBe(DocumentStatus.Cancelled);
        order.Items.All(i => i.OrderedQty == 0m).ShouldBeTrue();
    }
}
