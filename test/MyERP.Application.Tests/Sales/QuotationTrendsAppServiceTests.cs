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

public class QuotationTrendsAppServiceTests
{
    private readonly IRepository<Quotation, Guid> _quotationRepo = Substitute.For<IRepository<Quotation, Guid>>();
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

    public QuotationTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer = new Customer(_customerId, _companyId, "Trends Customer")
        {
            CustomerCode = "CUST-TRN",
        };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "ITEM-Q1", "Quoted Item 1", ItemType.Goods) { ItemGroup = "Hardware" };
        _item2 = new Item(_itemId2, _companyId, "ITEM-Q2", "Quoted Item 2", ItemType.Goods) { ItemGroup = "Software" };
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
        // Quotation 1: 15 Jun 2026, 4x ITEM-Q1 @ 200 = 800
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", new DateTime(2026, 6, 15));
        q1.AddItem(_itemId1, "Quoted Item 1", 4m, 200m, 0m);
        q1.Submit();

        // Quotation 2: 20 Jul 2026, 2x ITEM-Q1 @ 200 = 400
        var q2 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-002", new DateTime(2026, 7, 20));
        q2.AddItem(_itemId1, "Quoted Item 1", 2m, 200m, 0m);
        q2.Submit();

        _quotationRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Quotation, object>>[]>())
            .Returns(Task.FromResult(new List<Quotation> { q1, q2 }.AsQueryable()));

        var appService = new QuotationTrendsAppService(
            _quotationRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new QuotationTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Monthly",
            BasedOn = "Item",
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.EntityCode.ShouldBe("ITEM-Q1");

        // Jun (P6): 4 qty, 800 amt
        row.PeriodQuantities["P6"].ShouldBe(4m);
        row.PeriodAmounts["P6"].ShouldBe(800m);

        // Jul (P7): 2 qty, 400 amt
        row.PeriodQuantities["P7"].ShouldBe(2m);
        row.PeriodAmounts["P7"].ShouldBe(400m);

        // Overall Totals
        row.TotalQty.ShouldBe(6m);
        row.TotalAmount.ShouldBe(1200m);

        result.TotalRow.TotalAmount.ShouldBe(1200m);
        result.Chart.Datapoints.Sum().ShouldBe(1200m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_GroupByCustomer_SlicesCorrectly()
    {
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", new DateTime(2026, 2, 10));
        q1.AddItem(_itemId1, "Quoted Item 1", 3m, 100m, 0m);
        q1.Submit();

        _quotationRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Quotation, object>>[]>())
            .Returns(Task.FromResult(new List<Quotation> { q1 }.AsQueryable()));

        var appService = new QuotationTrendsAppService(
            _quotationRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new QuotationTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Monthly",
            BasedOn = "Item",
            GroupBy = "Customer",
        });

        result.Rows.Count.ShouldBe(2);

        var headerRow = result.Rows.First(r => string.IsNullOrEmpty(r.GroupByValue));
        headerRow.EntityCode.ShouldBe("ITEM-Q1");
        headerRow.TotalAmount.ShouldBe(300m);

        var detailRow = result.Rows.First(r => !string.IsNullOrEmpty(r.GroupByValue));
        detailRow.GroupByValue.ShouldBe("Trends Customer");
        detailRow.TotalAmount.ShouldBe(300m);
    }

    [Fact]
    public async Task GetReportAsync_Yearly_AggregatesToSingleBucket()
    {
        var q1 = new Quotation(Guid.NewGuid(), _companyId, _customerId, "QTN-001", new DateTime(2026, 3, 1));
        q1.AddItem(_itemId2, "Quoted Item 2", 1m, 500m, 0m);
        q1.Submit();

        _quotationRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Quotation, object>>[]>())
            .Returns(Task.FromResult(new List<Quotation> { q1 }.AsQueryable()));

        var appService = new QuotationTrendsAppService(
            _quotationRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new QuotationTrendsRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            Period = "Yearly",
            BasedOn = "Customer",
        });

        result.PeriodColumns.Count.ShouldBe(1);
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].EntityName.ShouldBe("Trends Customer");
        result.Rows[0].TotalAmount.ShouldBe(500m);
        result.TotalRow.TotalAmount.ShouldBe(500m);
    }
}
