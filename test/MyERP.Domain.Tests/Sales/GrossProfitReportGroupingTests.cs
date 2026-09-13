using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
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
