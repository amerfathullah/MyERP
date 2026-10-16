using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
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

public class SalesOrderTrendsAppServiceTests
{
    private readonly IRepository<SalesOrder, Guid> _orderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    private readonly Customer _customer;
    private readonly Item _item1;
    private readonly Item _item2;
    private readonly Company _company;

    public SalesOrderTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer = new Customer(_customerId, _companyId, "Acme Customer")
        {
            CustomerCode = "CUST-001"
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "ITEM-001", "Widget A", ItemType.Goods) { ItemGroup = "Widgets" };
        _item2 = new Item(_itemId2, _companyId, "ITEM-002", "Gadget B", ItemType.Goods) { ItemGroup = "Gadgets" };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _item1, _item2 }.AsQueryable()));

        var fiscalYear = new FiscalYear(
            Guid.NewGuid(),
            _companyId,
            "2026",
            new DateTime(2026, 1, 1),
            new DateTime(2026, 12, 31));
        _fiscalYearRepo.GetQueryableAsync().Returns(Task.FromResult(new List<FiscalYear> { fiscalYear }.AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_Monthly_CalculatesPeriodicSumsAndTotals()
    {
        // Order 1: 15 Jan 2026, 2x ITEM-001 @ 100 = 200
        var so1 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", new DateTime(2026, 1, 15));
        so1.AddItem(_itemId1, "Widget A", 2m, 100m, 0m);
        so1.Submit();

        // Order 2: 20 Feb 2026, 3x ITEM-001 @ 100 = 300
        var so2 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-002", new DateTime(2026, 2, 20));
        so2.AddItem(_itemId1, "Widget A", 3m, 100m, 0m);
        so2.Submit();

        _orderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so1, so2 }.AsQueryable()));

        var appService = new SalesOrderTrendsAppService(
            _orderRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new SalesOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Monthly",
            BasedOn = "Customer",
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.EntityName.ShouldBe("Acme Customer");
        row.SecondaryInfo.ShouldBe("—");

        // Jan (P1): 2 qty, 200 amt
        row.PeriodQuantities["P1"].ShouldBe(2m);
        row.PeriodAmounts["P1"].ShouldBe(200m);

        // Feb (P2): 3 qty, 300 amt
        row.PeriodQuantities["P2"].ShouldBe(3m);
        row.PeriodAmounts["P2"].ShouldBe(300m);

        // Totals
        row.TotalQty.ShouldBe(5m);
        row.TotalAmount.ShouldBe(500m);

        // Grand total row
        result.TotalRow.TotalAmount.ShouldBe(500m);
        result.TotalRow.TotalQty.ShouldBe(5m);

        // Chart datapoints match
        result.Chart.Datapoints.Sum().ShouldBe(500m);
        result.Chart.Currency.ShouldBe("MYR");
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_GroupByCustomer_SlicesCorrectly()
    {
        var so1 = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", new DateTime(2026, 3, 10));
        so1.AddItem(_itemId1, "Widget A", 5m, 10m, 0m);
        so1.Submit();

        _orderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so1 }.AsQueryable()));

        var appService = new SalesOrderTrendsAppService(
            _orderRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new SalesOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Monthly",
            BasedOn = "Item",
            GroupBy = "Customer",
        });

        // 1 Item header row + 1 Customer detail row
        result.Rows.Count.ShouldBe(2);

        var itemRow = result.Rows.First(r => string.IsNullOrEmpty(r.GroupByValue));
        itemRow.EntityCode.ShouldBe("ITEM-001");
        itemRow.TotalAmount.ShouldBe(50m);
        itemRow.TotalQty.ShouldBe(5m);

        var custDetailRow = result.Rows.First(r => !string.IsNullOrEmpty(r.GroupByValue));
        custDetailRow.GroupByValue.ShouldBe("Acme Customer");
        custDetailRow.TotalAmount.ShouldBe(50m);
        custDetailRow.TotalQty.ShouldBe(5m);
    }

    [Fact]
    public async Task GetReportAsync_CustomerMasterLabels_PreferredOverHistoricalSnapshot()
    {
        // Verifies PR Gotcha #302: name and territory taken from Customer master
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", new DateTime(2026, 4, 1));
        so.AddItem(_itemId2, "Gadget B", 1m, 1000m, 0m);
        so.Submit();

        _orderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        var appService = new SalesOrderTrendsAppService(
            _orderRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new SalesOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Yearly",
            BasedOn = "Customer",
        });

        result.Rows.Count.ShouldBe(1);
        result.Rows[0].EntityName.ShouldBe("Acme Customer");
        result.Rows[0].SecondaryInfo.ShouldBe("—");
        result.Rows[0].TotalAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_ClosedOrders_ExcludedByDefault()
    {
        var soClosed = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-CLOSED", new DateTime(2026, 5, 1));
        soClosed.AddItem(_itemId1, "Widget A", 10m, 50m, 0m);
        soClosed.Submit();
        soClosed.Close(); // Status = Closed

        _orderRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { soClosed }.AsQueryable()));

        var appService = new SalesOrderTrendsAppService(
            _orderRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Case 1: Exclude closed by default
        var result1 = await appService.GetReportAsync(new SalesOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            IncludeClosedOrders = false,
        });

        result1.Rows.ShouldBeEmpty();
        result1.TotalRow.TotalAmount.ShouldBe(0m);

        // Case 2: Include closed
        var result2 = await appService.GetReportAsync(new SalesOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            IncludeClosedOrders = true,
        });

        result2.Rows.Count.ShouldBe(1);
        result2.Rows[0].TotalAmount.ShouldBe(500m);
    }
}
