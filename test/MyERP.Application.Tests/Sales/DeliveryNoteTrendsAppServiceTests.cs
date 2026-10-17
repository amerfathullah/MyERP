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

public class DeliveryNoteTrendsAppServiceTests
{
    private readonly IRepository<DeliveryNote, Guid> _deliveryNoteRepo = Substitute.For<IRepository<DeliveryNote, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    private readonly Customer _customer;
    private readonly Item _item1;
    private readonly Item _item2;
    private readonly Company _company;

    public DeliveryNoteTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Logistics") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer = new Customer(_customerId, _companyId, "Global Traders")
        {
            CustomerCode = "CUST-GT"
        };
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
        // Arrange
        // Delivery 1: 15 Jan 2026, 2x ITEM-001 @ 100 = 200
        var dn1 = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-001", new DateTime(2026, 1, 15));
        dn1.AddItem(_itemId1, "Industrial Valve", 2m, 100m, 0m);
        dn1.Submit();

        // Delivery 2: 20 Feb 2026, 3x ITEM-001 @ 100 = 300
        var dn2 = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-002", new DateTime(2026, 2, 20));
        dn2.AddItem(_itemId1, "Industrial Valve", 3m, 100m, 0m);
        dn2.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn1, dn2 }.AsQueryable()));

        var appService = new DeliveryNoteTrendsAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new DeliveryNoteTrendsRequestDto
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
        result.TotalRow.TotalQty.ShouldBe(5m);
        result.Chart.Datapoints.Count.ShouldBe(3);
        result.Chart.Datapoints[0].ShouldBe(200m);
        result.Chart.Datapoints[1].ShouldBe(300m);
        result.Chart.Datapoints[2].ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_GroupsByCustomer()
    {
        // Arrange
        var dn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-001", new DateTime(2026, 1, 10));
        dn.AddItem(_itemId1, "Industrial Valve", 4m, 50m, 0m);
        dn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn }.AsQueryable()));

        var appService = new DeliveryNoteTrendsAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new DeliveryNoteTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Item",
            GroupBy = "Customer",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(2); // Header row + detail group row
        var headerRow = result.Rows.FirstOrDefault(r => string.IsNullOrEmpty(r.GroupByValue));
        headerRow.ShouldNotBeNull();
        headerRow.EntityCode.ShouldBe("ITEM-001");
        headerRow.TotalQty.ShouldBe(4m);
        headerRow.TotalAmount.ShouldBe(200m);

        var detailRow = result.Rows.FirstOrDefault(r => !string.IsNullOrEmpty(r.GroupByValue));
        detailRow.ShouldNotBeNull();
        detailRow.GroupByValue.ShouldBe("Global Traders");
        detailRow.TotalQty.ShouldBe(4m);
        detailRow.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    public async Task GetReportAsync_IncludeReturns_SubtractsReturnedQuantitiesAndAmounts()
    {
        // Arrange
        var dn1 = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-001", new DateTime(2026, 1, 10));
        dn1.AddItem(_itemId1, "Industrial Valve", 10m, 50m, 0m);
        dn1.Submit();

        var dnReturn = new DeliveryNote(Guid.NewGuid(), _companyId, _customerId, _warehouseId, "DN-RET-001", new DateTime(2026, 1, 20))
        {
            IsReturn = true
        };
        dnReturn.AddItem(_itemId1, "Industrial Valve Return", -2m, 50m, 0m);
        dnReturn.Submit();

        _deliveryNoteRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<DeliveryNote, object>>[]>())
            .Returns(Task.FromResult(new List<DeliveryNote> { dn1, dnReturn }.AsQueryable()));

        var appService = new DeliveryNoteTrendsAppService(
            _deliveryNoteRepo, _customerRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act 1: IncludeReturns = false
        var resultWithoutReturns = await appService.GetReportAsync(new DeliveryNoteTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Customer",
            IncludeReturns = false,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Act 2: IncludeReturns = true
        var resultWithReturns = await appService.GetReportAsync(new DeliveryNoteTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Customer",
            IncludeReturns = true,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        resultWithoutReturns.TotalRow.TotalQty.ShouldBe(10m);
        resultWithoutReturns.TotalRow.TotalAmount.ShouldBe(500m);

        resultWithReturns.TotalRow.TotalQty.ShouldBe(8m);
        resultWithReturns.TotalRow.TotalAmount.ShouldBe(400m);
    }
}
