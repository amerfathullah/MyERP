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

public class PurchaseReceiptTrendsAppServiceTests
{
    private readonly IRepository<PurchaseReceipt, Guid> _receiptRepo = Substitute.For<IRepository<PurchaseReceipt, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _itemId1 = Guid.NewGuid();
    private readonly Guid _itemId2 = Guid.NewGuid();

    private readonly Supplier _supplier;
    private readonly Item _item1;
    private readonly Item _item2;
    private readonly Company _company;

    public PurchaseReceiptTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Apex Manufacturing") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier = new Supplier(_supplierId, _companyId, "Global Supplies Co")
        {
            SupplierCode = "SUPP-001"
        };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "RAW-001", "Steel Plate", ItemType.Goods) { ItemGroup = "Raw Materials" };
        _item2 = new Item(_itemId2, _companyId, "RAW-002", "Copper Wire", ItemType.Goods) { ItemGroup = "Raw Materials" };
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
        // Receipt 1: 15 Jan 2026, 4x RAW-001 @ 25 = 100
        var pr1 = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", new DateTime(2026, 1, 15));
        pr1.AddItem(_itemId1, "Steel Plate", 4m, 25m, 0m);
        pr1.Submit();

        // Receipt 2: 20 Feb 2026, 6x RAW-001 @ 25 = 150
        var pr2 = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-002", new DateTime(2026, 2, 20));
        pr2.AddItem(_itemId1, "Steel Plate", 6m, 25m, 0m);
        pr2.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr1, pr2 }.AsQueryable()));

        var appService = new PurchaseReceiptTrendsAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new PurchaseReceiptTrendsRequestDto
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
        row.EntityCode.ShouldBe("SUPP-001");
        row.EntityName.ShouldBe("Global Supplies Co");
        row.TotalQty.ShouldBe(10m);
        row.TotalAmount.ShouldBe(250m);

        row.PeriodAmounts["P1"].ShouldBe(100m);
        row.PeriodAmounts["P2"].ShouldBe(150m);
        row.PeriodAmounts["P3"].ShouldBe(0m);

        result.TotalRow.TotalAmount.ShouldBe(250m);
        result.TotalRow.TotalQty.ShouldBe(10m);
        result.Chart.Datapoints.Count.ShouldBe(3);
        result.Chart.Datapoints[0].ShouldBe(100m);
        result.Chart.Datapoints[1].ShouldBe(150m);
        result.Chart.Datapoints[2].ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItem_GroupsBySupplier()
    {
        // Arrange
        var pr = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", new DateTime(2026, 1, 10));
        pr.AddItem(_itemId1, "Steel Plate", 8m, 50m, 0m);
        pr.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr }.AsQueryable()));

        var appService = new PurchaseReceiptTrendsAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new PurchaseReceiptTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Item",
            GroupBy = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(2); // Header row + detail group row
        var headerRow = result.Rows.FirstOrDefault(r => string.IsNullOrEmpty(r.GroupByValue));
        headerRow.ShouldNotBeNull();
        headerRow.EntityCode.ShouldBe("RAW-001");
        headerRow.TotalQty.ShouldBe(8m);
        headerRow.TotalAmount.ShouldBe(400m);

        var detailRow = result.Rows.FirstOrDefault(r => !string.IsNullOrEmpty(r.GroupByValue));
        detailRow.ShouldNotBeNull();
        detailRow.GroupByValue.ShouldBe("Global Supplies Co");
        detailRow.TotalQty.ShouldBe(8m);
        detailRow.TotalAmount.ShouldBe(400m);
    }

    [Fact]
    public async Task GetReportAsync_IncludeReturns_SubtractsReturnedQuantitiesAndAmounts()
    {
        // Arrange
        var pr1 = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-001", new DateTime(2026, 1, 10));
        pr1.AddItem(_itemId1, "Steel Plate", 10m, 50m, 0m);
        pr1.Submit();

        var prReturn = new PurchaseReceipt(Guid.NewGuid(), _companyId, _supplierId, _warehouseId, "PR-RET-001", new DateTime(2026, 1, 20))
        {
            IsReturn = true
        };
        prReturn.AddItem(_itemId1, "Steel Plate Return", -3m, 50m, 0m);
        prReturn.Submit();

        _receiptRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseReceipt, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseReceipt> { pr1, prReturn }.AsQueryable()));

        var appService = new PurchaseReceiptTrendsAppService(
            _receiptRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Act 1: IncludeReturns = false
        var resultWithoutReturns = await appService.GetReportAsync(new PurchaseReceiptTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            IncludeReturns = false,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Act 2: IncludeReturns = true
        var resultWithReturns = await appService.GetReportAsync(new PurchaseReceiptTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            IncludeReturns = true,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        resultWithoutReturns.TotalRow.TotalQty.ShouldBe(10m);
        resultWithoutReturns.TotalRow.TotalAmount.ShouldBe(500m);

        resultWithReturns.TotalRow.TotalQty.ShouldBe(7m);
        resultWithReturns.TotalRow.TotalAmount.ShouldBe(350m);
    }
}
