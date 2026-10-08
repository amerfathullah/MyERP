using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class GrossProfitReportGroupingTests
{
    [Fact]
    public void GrossProfitRequestDto_DefaultsToInvoiceGrouping()
    {
        var dto = new GrossProfitRequestDto
        {
            CompanyId = Guid.NewGuid()
        };

        dto.GroupBy.ShouldBe("Invoice");
        dto.CustomerId.ShouldBeNull();
        dto.ItemId.ShouldBeNull();
    }

    [Fact]
    public void GrossProfitLineDto_ItemNameAndFieldsSettable()
    {
        // Per ERPNext PR #58631 / commit 467f54162f: ItemName must be included in export and report data
        var dto = new GrossProfitLineDto
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceNumber = "SI-2026-0001",
            IssueDate = new DateTime(2026, 9, 1),
            CustomerId = Guid.NewGuid(),
            CustomerName = "ACME Corp",
            ItemId = Guid.NewGuid(),
            ItemCode = "ITEM-001",
            ItemName = "High Grade Steel Bearing",
            ItemGroup = "Bearings",
            WarehouseId = Guid.NewGuid(),
            WarehouseName = "Finished Goods - HQ",
            Quantity = 10m,
            SellingRate = 150m,
            ValuationRate = 90m,
            Revenue = 1500m,
            Cost = 900m,
            GrossProfit = 600m,
            GrossProfitPercentage = 40m
        };

        dto.ItemName.ShouldBe("High Grade Steel Bearing");
        dto.ItemCode.ShouldBe("ITEM-001");
        dto.ItemGroup.ShouldBe("Bearings");
        dto.WarehouseName.ShouldBe("Finished Goods - HQ");
        dto.SellingRate.ShouldBe(150m);
        dto.ValuationRate.ShouldBe(90m);
        dto.GrossProfit.ShouldBe(600m);
        dto.GrossProfitPercentage.ShouldBe(40m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_GetReportAsync_GroupByInvoice_IncludesItemNameAndDetails()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId1 = Guid.NewGuid();
        var itemId2 = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var customer = new Customer(customerId, companyId, "ACME Corp");
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(new List<Customer> { customer });

        var item1 = new Item(itemId1, companyId, "BEAR-01", "Precision Ball Bearing", ItemType.Goods);
        var item2 = new Item(itemId2, companyId, "GEAR-01", "Planetary Gear Assembly", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(new List<Item> { item1, item2 });

        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>())
            .Returns(new List<Warehouse>());

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 15));
        si.AddItem(itemId1, "Bearing line", 10m, 100m, 0m);
        si.Items[0].ValuationRate = 60m; // profit = (100-60)*10 = 400
        si.AddItem(itemId2, "Gear line", 5m, 200m, 0m);
        si.Items[1].ValuationRate = 120m; // profit = (200-120)*5 = 400
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Invoice"
        });

        report.ShouldNotBeNull();
        report.TotalRevenue.ShouldBe(2000m); // 10*100 + 5*200 = 2000
        report.TotalCost.ShouldBe(1200m);    // 10*60 + 5*120 = 1200
        report.GrossProfit.ShouldBe(800m);
        report.GrossProfitPercentage.ShouldBe(40m); // 800 / 2000 * 100 = 40%

        report.Items.Count.ShouldBe(2);
        var row0 = report.Items.First(r => r.ItemId == itemId1);
        row0.ItemCode.ShouldBe("BEAR-01");
        row0.ItemName.ShouldBe("Precision Ball Bearing");
        row0.CustomerName.ShouldBe("ACME Corp");
        row0.Quantity.ShouldBe(10m);
        row0.SellingRate.ShouldBe(100m);
        row0.ValuationRate.ShouldBe(60m);
        row0.Revenue.ShouldBe(1000m);
        row0.Cost.ShouldBe(600m);
        row0.GrossProfit.ShouldBe(400m);
        row0.GrossProfitPercentage.ShouldBe(40m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_GetReportAsync_GroupByItem_AggregatesAcrossInvoices()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "ITEM-X", "Universal Flange", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>())
            .Returns(new List<Warehouse>());

        // Invoice 1: 10 qty @ RM100, cost RM60 (Revenue 1000, Cost 600)
        var si1 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 1));
        si1.AddItem(itemId, "Flange batch 1", 10m, 100m, 0m);
        si1.Items[0].ValuationRate = 60m;
        si1.Submit();
        si1.Post();

        // Invoice 2: 20 qty @ RM120, cost RM70 (Revenue 2400, Cost 1400)
        var si2 = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0002", new DateTime(2026, 6, 10));
        si2.AddItem(itemId, "Flange batch 2", 20m, 120m, 0m);
        si2.Items[0].ValuationRate = 70m;
        si2.Submit();
        si2.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si1, si2 }.AsQueryable());

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Item"
        });

        report.ShouldNotBeNull();
        report.Items.Count.ShouldBe(1);

        var aggregated = report.Items[0];
        aggregated.ItemId.ShouldBe(itemId);
        aggregated.ItemCode.ShouldBe("ITEM-X");
        aggregated.ItemName.ShouldBe("Universal Flange");
        aggregated.Quantity.ShouldBe(30m); // 10 + 20
        aggregated.Revenue.ShouldBe(3400m); // 1000 + 2400
        aggregated.Cost.ShouldBe(2000m);    // 600 + 1400
        aggregated.GrossProfit.ShouldBe(1400m);
        aggregated.GrossProfitPercentage.ShouldBe(41.18m); // 1400 / 3400 * 100
        aggregated.SellingRate.ShouldBe(113.3333m); // 3400 / 30
        aggregated.ValuationRate.ShouldBe(66.6667m); // 2000 / 30
    }

    [Fact]
    public async Task GrossProfitReportAppService_GetReportAsync_GroupByCustomer_AggregatesAcrossInvoices()
    {
        var companyId = Guid.NewGuid();
        var customer1Id = Guid.NewGuid();
        var customer2Id = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var cust1 = new Customer(customer1Id, companyId, "Alpha Logistics");
        var cust2 = new Customer(customer2Id, companyId, "Beta Retail");
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(new List<Customer> { cust1, cust2 });
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(new List<Item>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>())
            .Returns(new List<Warehouse>());

        var si1 = new SalesInvoice(Guid.NewGuid(), companyId, customer1Id, "SI-2026-0001", new DateTime(2026, 6, 1));
        si1.AddItem(itemId, "Widget", 10m, 100m, 0m);
        si1.Items[0].ValuationRate = 60m;
        si1.Submit();
        si1.Post();

        var si2 = new SalesInvoice(Guid.NewGuid(), companyId, customer2Id, "SI-2026-0002", new DateTime(2026, 6, 2));
        si2.AddItem(itemId, "Widget", 5m, 100m, 0m);
        si2.Items[0].ValuationRate = 80m;
        si2.Submit();
        si2.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si1, si2 }.AsQueryable());

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Customer"
        });

        report.ShouldNotBeNull();
        report.Items.Count.ShouldBe(2);

        var c1Row = report.Items.First(r => r.CustomerId == customer1Id);
        c1Row.CustomerName.ShouldBe("Alpha Logistics");
        c1Row.Revenue.ShouldBe(1000m);
        c1Row.Cost.ShouldBe(600m);
        c1Row.GrossProfit.ShouldBe(400m);

        var c2Row = report.Items.First(r => r.CustomerId == customer2Id);
        c2Row.CustomerName.ShouldBe("Beta Retail");
        c2Row.Revenue.ShouldBe(500m);
        c2Row.Cost.ShouldBe(400m);
        c2Row.GrossProfit.ShouldBe(100m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_DropShipNotYetBilled_FallsBackToPurchaseOrderRate()
    {
        // Per ERPNext PR #59885 (commit 9f03f19f65):
        // When supplier has not invoiced drop-ship item yet, fall back to PO rate instead of 0 cost / 100% margin.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "DROP-01", "Drop Ship Item", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(new List<Warehouse>());

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 1));
        si.AddItem(itemId, "Drop Ship Item", 10m, 100m, 0m);
        si.Items[0].ValuationRate = 0m; // No stock valuation on drop-ship
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        // PO created for drop ship @ RM70/unit
        var po = new PurchaseOrder(Guid.NewGuid(), companyId, Guid.NewGuid(), "PO-2026-0001", new DateTime(2026, 5, 20));
        po.AddItem(itemId, "Drop Ship Item", 10m, 70m, 0m, deliveredBySupplier: true);
        po.Submit();

        poRepo.GetListAsync(Arg.Any<Expression<Func<PurchaseOrder, bool>>>()).Returns(new List<PurchaseOrder> { po });

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo,
            purchaseInvoiceRepository: null, purchaseOrderRepository: poRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        report.ShouldNotBeNull();
        report.TotalRevenue.ShouldBe(1000m);
        report.TotalCost.ShouldBe(700m); // 10 * 70 from PO fallback!
        report.GrossProfit.ShouldBe(300m);
        report.GrossProfitPercentage.ShouldBe(30m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_NonStockItem_UsesDiscountedPurchaseRate()
    {
        // Per ERPNext PR #59885 (commit e8496405d4):
        // Non-stock items priced at last net purchase rate (discounted base_net_rate).
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "SERV-01", "Consulting Service", ItemType.Service);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(new List<Warehouse>());

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 1));
        si.AddItem(itemId, "Consulting", 1m, 200m, 0m);
        si.Items[0].ValuationRate = 0m;
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        // PI has RM100 rate with 10% discount -> net rate is RM90
        var pi = new PurchaseInvoice(Guid.NewGuid(), companyId, Guid.NewGuid(), "PI-2026-0001", new DateTime(2026, 5, 10));
        pi.AddItem(itemId, "Subcontract", 10m, 100m, 0m);
        pi.AdditionalDiscountPercentage = 10m;
        pi.Submit();
        pi.Post();

        piRepo.GetListAsync(Arg.Any<Expression<Func<PurchaseInvoice, bool>>>()).Returns(new List<PurchaseInvoice> { pi });

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo,
            purchaseInvoiceRepository: piRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        report.ShouldNotBeNull();
        report.TotalRevenue.ShouldBe(200m);
        report.TotalCost.ShouldBe(90m); // Discounted net purchase rate
        report.GrossProfit.ShouldBe(110m);
        report.GrossProfitPercentage.ShouldBe(55m);
    }

    [Fact]
    public void GrossProfitService_CalculateDeliveryIncomingRate_GuardsAgainstZeroDeliveredQty()
    {
        // Per ERPNext PR #59885 (commit 3aa0af6844):
        // Guard sales order delivery rate against zero delivered qty (when delivery is fully returned).
        var service = new GrossProfitService(null!);

        var dnItem1 = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 2m, 100m, 0m);
        dnItem1.ValuationRate = 80m;
        var dnItem2 = new DeliveryNoteItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", -2m, 100m, 0m);
        dnItem2.ValuationRate = 80m;

        // Sum of stock qty is 0. Must not throw DivideByZeroException; falls back to valuation rate 65.
        var rate = service.CalculateDeliveryIncomingRate(new[] { dnItem1, dnItem2 }, 65m);
        rate.ShouldBe(65m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_GroupByProject_AggregatesAndResolvesProjectName()
    {
        // Per ERPNext PR #59885 (commit 202c50d475):
        // Group by Project resolves project name and aggregates metrics.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var projectRepo = Substitute.For<IRepository<Project, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "ITEM-01", "Widget", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(new List<Warehouse>());

        var project = new Project(projectId, companyId, "PRJ-001", "Solar Installation");
        projectRepo.GetListAsync(Arg.Any<Expression<Func<Project, bool>>>()).Returns(new List<Project> { project });

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 1))
        {
            ProjectId = projectId
        };
        si.AddItem(itemId, "Solar Panel", 5m, 200m, 0m);
        si.Items[0].ValuationRate = 120m;
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo,
            projectRepository: projectRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Project"
        });

        report.ShouldNotBeNull();
        report.Items.Count.ShouldBe(1);
        report.Items[0].ProjectId.ShouldBe(projectId);
        report.Items[0].ProjectName.ShouldBe("Solar Installation");
        report.Items[0].Revenue.ShouldBe(1000m);
        report.Items[0].Cost.ShouldBe(600m);
        report.Items[0].GrossProfit.ShouldBe(400m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_GroupByPaymentTerm_InvoicesWithoutScheduleCountAt100Percent()
    {
        // Per ERPNext PR #59885 (commit cc0d7021b1):
        // Count invoices without payment schedule (or returns) at 100% under "No Terms".
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var psRepo = Substitute.For<IRepository<PaymentScheduleEntry, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "ITEM-01", "Widget", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(new List<Warehouse>());
        psRepo.GetListAsync(Arg.Any<Expression<Func<PaymentScheduleEntry, bool>>>()).Returns(new List<PaymentScheduleEntry>());

        // POS invoice has no payment schedule entries
        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "POS-0001", new DateTime(2026, 6, 1))
        {
            IsPos = true
        };
        si.AddItem(itemId, "Over the counter", 5m, 100m, 0m);
        si.Items[0].ValuationRate = 40m;
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo,
            paymentScheduleRepository: psRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Payment Term"
        });

        report.ShouldNotBeNull();
        report.Items.Count.ShouldBe(1);
        report.Items[0].PaymentTerm.ShouldBe("No Terms");
        report.Items[0].Revenue.ShouldBe(500m); // Counts 100%, not 0!
        report.Items[0].Cost.ShouldBe(200m);
        report.Items[0].GrossProfit.ShouldBe(300m);
    }

    [Fact]
    public async Task GrossProfitReportAppService_GroupBySalesPerson_SharedInvoiceCountedOnceInGrandTotal()
    {
        // Per ERPNext PR #59885 (commit 2152a7d848):
        // Count shared invoices once in the sales person total.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var sp1Id = Guid.NewGuid();
        var sp2Id = Guid.NewGuid();

        var invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
        var customerRepo = Substitute.For<IRepository<Customer, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
        var spRepo = Substitute.For<IRepository<SalesPerson, Guid>>();
        var teamRepo = Substitute.For<IRepository<SalesTeamEntry, Guid>>();
        var grossProfitService = new GrossProfitService(null!);

        var item = new Item(itemId, companyId, "ITEM-01", "Widget", ItemType.Goods);
        itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>()).Returns(new List<Item> { item });
        customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>()).Returns(new List<Customer>());
        warehouseRepo.GetListAsync(Arg.Any<Expression<Func<Warehouse, bool>>>()).Returns(new List<Warehouse>());

        var sp1 = new SalesPerson(sp1Id, "Alice");
        var sp2 = new SalesPerson(sp2Id, "Bob");
        spRepo.GetListAsync(Arg.Any<Expression<Func<SalesPerson, bool>>>()).Returns(new List<SalesPerson> { sp1, sp2 });

        var si = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "SI-2026-0001", new DateTime(2026, 6, 1));
        si.AddItem(itemId, "Widget", 10m, 100m, 0m); // Revenue 1000
        si.Items[0].ValuationRate = 60m; // Cost 600, Profit 400
        si.Submit();
        si.Post();

        invoiceRepo.GetQueryableAsync().Returns(new List<SalesInvoice> { si }.AsQueryable());

        // Invoice shared 50/50 between Alice and Bob
        var team1 = new SalesTeamEntry(Guid.NewGuid(), sp1Id, "SalesInvoice", si.Id, 50m, 1000m, 5m);
        var team2 = new SalesTeamEntry(Guid.NewGuid(), sp2Id, "SalesInvoice", si.Id, 50m, 1000m, 5m);
        teamRepo.GetListAsync(Arg.Any<Expression<Func<SalesTeamEntry, bool>>>()).Returns(new List<SalesTeamEntry> { team1, team2 });

        var appService = new GrossProfitReportAppService(
            invoiceRepo, grossProfitService, customerRepo, itemRepo, warehouseRepo,
            salesPersonRepository: spRepo, salesTeamRepository: teamRepo);

        var report = await appService.GetReportAsync(new GrossProfitRequestDto
        {
            CompanyId = companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            GroupBy = "Sales Person"
        });

        report.ShouldNotBeNull();
        // Repeated under Alice and Bob in line rows
        report.Items.Count.ShouldBe(2);

        // But Grand Total counts the shared invoice only ONCE!
        report.TotalRevenue.ShouldBe(1000m); // NOT 2000!
        report.TotalCost.ShouldBe(600m);    // NOT 1200!
        report.GrossProfit.ShouldBe(400m);  // NOT 800!
    }

    [Theory]
    [InlineData("SellingRate")]
    [InlineData("AvgSellingRate")]
    [InlineData("ValuationRate")]
    [InlineData("GrossProfit")]
    [InlineData("GrossMargin")]
    public void Localization_SellingRateKeys_ExistInEnJson(string key)
    {
        var jsonPath = Path.Combine(
            TestHelper.GetSolutionRoot(), "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }
}

