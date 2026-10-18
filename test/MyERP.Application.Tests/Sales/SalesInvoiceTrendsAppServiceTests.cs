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

public class SalesInvoiceTrendsAppServiceTests
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
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

    public SalesInvoiceTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Sales") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer = new Customer(_customerId, _companyId, "Global Traders") { CustomerCode = "CUST-GT" };
        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "ITEM-001", "Industrial Valve", ItemType.Goods) { ItemGroup = "Valves" };
        _item2 = new Item(_itemId2, _companyId, "ITEM-002", "Pressure Sensor", ItemType.Goods) { ItemGroup = "Sensors" };
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
        // Invoice 1: 15 Jan 2026, 2x ITEM-001 @ 100 = 200
        var inv1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-001", new DateTime(2026, 1, 15));
        inv1.AddItem(_itemId1, "Industrial Valve", 2m, 100m, 0m);
        inv1.Submit();

        // Invoice 2: 20 Feb 2026, 3x ITEM-001 @ 100 = 300
        var inv2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-002", new DateTime(2026, 2, 20));
        inv2.AddItem(_itemId1, "Industrial Valve", 3m, 100m, 0m);
        inv2.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { inv1, inv2 }.AsQueryable()));

        var appService = new SalesInvoiceTrendsAppService(
            _invoiceRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SalesInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Customer",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 3, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.CompanyCurrency.ShouldBe("MYR");
        result.PeriodColumns.Count.ShouldBe(3); // Jan, Feb, Mar

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.EntityCode.ShouldBe("CUST-GT");
        row.EntityName.ShouldBe("Global Traders");
        row.TotalQty.ShouldBe(5m);
        row.TotalAmount.ShouldBe(500m);

        row.PeriodAmounts["P1"].ShouldBe(200m);
        row.PeriodAmounts["P2"].ShouldBe(300m);
        row.PeriodAmounts["P3"].ShouldBe(0m);

        result.TotalRow.TotalAmount.ShouldBe(500m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_CalculatesItemTotals()
    {
        // Invoice 1: 10 Jan 2026, 4x ITEM-001 @ 50 = 200
        var inv1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-003", new DateTime(2026, 1, 10));
        inv1.AddItem(_itemId1, "Industrial Valve", 4m, 50m, 0m);
        inv1.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { inv1 }.AsQueryable()));

        var appService = new SalesInvoiceTrendsAppService(
            _invoiceRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SalesInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Yearly",
            BasedOn = "Item",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.EntityCode.ShouldBe("ITEM-001");
        row.EntityName.ShouldBe("Industrial Valve");
        row.TotalQty.ShouldBe(4m);
        row.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    public async Task GetReportAsync_Quarterly_LandsInExpectedQuarter()
    {
        // Invoice dated 15 May 2026 -> Q2 (Apr-Jun)
        var inv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-004", new DateTime(2026, 5, 15));
        inv.AddItem(_itemId2, "Pressure Sensor", 2m, 250m, 0m);
        inv.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { inv }.AsQueryable()));

        var appService = new SalesInvoiceTrendsAppService(
            _invoiceRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SalesInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Quarterly",
            BasedOn = "Item",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 6, 30)
        });

        // Assert
        result.ShouldNotBeNull();
        result.PeriodColumns.Count.ShouldBe(2); // Q1, Q2
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.PeriodAmounts["P1"].ShouldBe(0m);
        row.PeriodAmounts["P2"].ShouldBe(500m);
        row.TotalAmount.ShouldBe(500m);
    }

    [Fact]
    public async Task GetReportAsync_ExcludeReturns_OmitsCreditNotes()
    {
        // Invoice return (credit note)
        var creditNote = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "CR-001", new DateTime(2026, 2, 10))
        {
            IsReturn = true
        };
        creditNote.AddItem(_itemId1, "Industrial Valve", -1m, 100m, 0m);
        creditNote.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SalesInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<SalesInvoice> { creditNote }.AsQueryable()));

        var appService = new SalesInvoiceTrendsAppService(
            _invoiceRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SalesInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Customer",
            IncludeReturns = false,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 3, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.ShouldBeEmpty();
    }
}
