using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
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

public class PurchaseInvoiceTrendsAppServiceTests
{
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    private readonly Supplier _supplier;
    private readonly Item _item1;
    private readonly Item _item2;
    private readonly Company _company;

    public PurchaseInvoiceTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Purchasing") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier = new Supplier(_supplierId, _companyId, "Apex Industrial Supplies") { SupplierCode = "SUPP-AIS" };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "RAW-001", "Steel Pipe", ItemType.Goods) { ItemGroup = "Raw Material" };
        _item2 = new Item(_itemId2, _companyId, "RAW-002", "Copper Wire", ItemType.Goods) { ItemGroup = "Electrical" };
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
        // Invoice 1: 15 Jan 2026, 2x RAW-001 @ 150 = 300
        var inv1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-001", new DateTime(2026, 1, 15));
        inv1.AddItem(_itemId1, "Steel Pipe", 2m, 150m, 0m);
        inv1.Submit();

        // Invoice 2: 20 Feb 2026, 3x RAW-001 @ 150 = 450
        var inv2 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-002", new DateTime(2026, 2, 20));
        inv2.AddItem(_itemId1, "Steel Pipe", 3m, 150m, 0m);
        inv2.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv1, inv2 }.AsQueryable()));

        var appService = new PurchaseInvoiceTrendsAppService(
            _invoiceRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new PurchaseInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 3, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.CompanyCurrency.ShouldBe("MYR");
        result.PeriodColumns.Count.ShouldBe(3); // Jan, Feb, Mar

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.EntityCode.ShouldBe("SUPP-AIS");
        row.EntityName.ShouldBe("Apex Industrial Supplies");
        row.TotalQty.ShouldBe(5m);
        row.TotalAmount.ShouldBe(750m);

        row.PeriodAmounts["P1"].ShouldBe(300m);
        row.PeriodAmounts["P2"].ShouldBe(450m);
        row.PeriodAmounts["P3"].ShouldBe(0m);

        result.TotalRow.TotalAmount.ShouldBe(750m);
        result.TotalRow.TotalQty.ShouldBe(5m);
        result.Chart.Datapoints.Count.ShouldBe(3);
        result.Chart.Datapoints[0].ShouldBe(300m);
        result.Chart.Datapoints[1].ShouldBe(450m);
        result.Chart.Datapoints[2].ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_Quarterly_AggregatesCorrectly()
    {
        // Invoice 1: Jan 2026, 5x RAW-001 @ 100 = 500
        var inv1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-003", new DateTime(2026, 1, 10));
        inv1.AddItem(_itemId1, "Steel Pipe", 5m, 100m, 0m);
        inv1.Submit();

        // Invoice 2: May 2026, 2x RAW-002 @ 250 = 500
        var inv2 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-004", new DateTime(2026, 5, 12));
        inv2.AddItem(_itemId2, "Copper Wire", 2m, 250m, 0m);
        inv2.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv1, inv2 }.AsQueryable()));

        var appService = new PurchaseInvoiceTrendsAppService(
            _invoiceRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new PurchaseInvoiceTrendsRequestDto
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
        result.Rows.Count.ShouldBe(2); // RAW-001 and RAW-002

        var rowItem1 = result.Rows.First(r => r.EntityCode == "RAW-001");
        rowItem1.TotalQty.ShouldBe(5m);
        rowItem1.TotalAmount.ShouldBe(500m);
        rowItem1.PeriodAmounts["P1"].ShouldBe(500m);
        rowItem1.PeriodAmounts["P2"].ShouldBe(0m);

        var rowItem2 = result.Rows.First(r => r.EntityCode == "RAW-002");
        rowItem2.TotalQty.ShouldBe(2m);
        rowItem2.TotalAmount.ShouldBe(500m);
        rowItem2.PeriodAmounts["P1"].ShouldBe(0m);
        rowItem2.PeriodAmounts["P2"].ShouldBe(500m);

        result.TotalRow.TotalAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_IncludeReturns_NetQuantitiesAndAmounts()
    {
        // Invoice 1: Jan 2026, 10x RAW-001 @ 100 = 1000
        var inv1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-005", new DateTime(2026, 1, 10));
        inv1.AddItem(_itemId1, "Steel Pipe", 10m, 100m, 0m);
        inv1.Submit();

        // Return: Feb 2026, -2x RAW-001 @ 100 = -200
        var ret = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PRET-001", new DateTime(2026, 2, 5))
        {
            IsReturn = true
        };
        ret.AddItem(_itemId1, "Steel Pipe Return", -2m, 100m, 0m);
        ret.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv1, ret }.AsQueryable()));

        var appService = new PurchaseInvoiceTrendsAppService(
            _invoiceRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Case 1: Exclude returns
        var resultNoReturns = await appService.GetReportAsync(new PurchaseInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            IncludeReturns = false,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 2, 28)
        });

        resultNoReturns.Rows[0].TotalQty.ShouldBe(10m);
        resultNoReturns.Rows[0].TotalAmount.ShouldBe(1000m);

        // Case 2: Include returns
        var resultWithReturns = await appService.GetReportAsync(new PurchaseInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            IncludeReturns = true,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 2, 28)
        });

        resultWithReturns.Rows[0].TotalQty.ShouldBe(8m);
        resultWithReturns.Rows[0].TotalAmount.ShouldBe(800m);
        resultWithReturns.Rows[0].PeriodAmounts["P1"].ShouldBe(1000m);
        resultWithReturns.Rows[0].PeriodAmounts["P2"].ShouldBe(-200m);
    }

    [Fact]
    public async Task GetReportAsync_SupplierWithItemGroupBy_ProducesSubRows()
    {
        // Invoice with 2 items
        var inv = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "PINV-006", new DateTime(2026, 1, 15));
        inv.AddItem(_itemId1, "Steel Pipe", 4m, 50m, 0m);
        inv.AddItem(_itemId2, "Copper Wire", 2m, 100m, 0m);
        inv.Submit();

        _invoiceRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseInvoice, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseInvoice> { inv }.AsQueryable()));

        var appService = new PurchaseInvoiceTrendsAppService(
            _invoiceRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new PurchaseInvoiceTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            GroupBy = "Item",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        // 1 Header row (Apex Industrial Supplies) + 2 Sub-rows (Steel Pipe, Copper Wire)
        result.Rows.Count.ShouldBe(3);

        var header = result.Rows.First(r => string.IsNullOrEmpty(r.GroupByValue));
        header.EntityName.ShouldBe("Apex Industrial Supplies");
        header.TotalAmount.ShouldBe(400m); // (4*50) + (2*100) = 200 + 200

        var subRows = result.Rows.Where(r => !string.IsNullOrEmpty(r.GroupByValue)).ToList();
        subRows.Count.ShouldBe(2);

        result.TotalRow.TotalAmount.ShouldBe(400m);
    }
}
