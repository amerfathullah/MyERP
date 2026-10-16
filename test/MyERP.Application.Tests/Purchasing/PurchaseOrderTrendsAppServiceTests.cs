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

public class PurchaseOrderTrendsAppServiceTests
{
    private readonly IRepository<PurchaseOrder, Guid> _poRepo = Substitute.For<IRepository<PurchaseOrder, Guid>>();
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

    public PurchaseOrderTrendsAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Corp") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier = new Supplier(_supplierId, _companyId, "Premier Supplier")
        {
            SupplierCode = "SUP-PRM",
        };
        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier }.AsQueryable()));

        _item1 = new Item(_itemId1, _companyId, "ITEM-P1", "Purchased Item 1", ItemType.Goods) { ItemGroup = "Hardware" };
        _item2 = new Item(_itemId2, _companyId, "ITEM-P2", "Purchased Item 2", ItemType.Goods) { ItemGroup = "Consumables" };
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
        // PO 1: 10 May 2026, 5x ITEM-P1 @ 100 = 500
        var po1 = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-001", new DateTime(2026, 5, 10));
        po1.AddItem(_itemId1, "Purchased Item 1", 5m, 100m, 0m, "Nos");
        po1.Submit();

        // PO 2: 25 Jun 2026, 3x ITEM-P1 @ 100 = 300
        var po2 = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-002", new DateTime(2026, 6, 25));
        po2.AddItem(_itemId1, "Purchased Item 1", 3m, 100m, 0m, "Nos");
        po2.Submit();

        _poRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po1, po2 }.AsQueryable()));

        var appService = new PurchaseOrderTrendsAppService(
            _poRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new PurchaseOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });

        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.EntityCode.ShouldBe("SUP-PRM");
        row.EntityName.ShouldBe("Premier Supplier");
        row.TotalQty.ShouldBe(8m);
        row.TotalAmount.ShouldBe(800m);

        result.TotalRow.TotalQty.ShouldBe(8m);
        result.TotalRow.TotalAmount.ShouldBe(800m);

        result.Chart.Labels.Count.ShouldBe(12);
        result.Chart.Datapoints.Sum().ShouldBe(800m);
    }

    [Fact]
    public async Task GetReportAsync_Quarterly_BasedOnItem_WithSupplierGrouping()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-003", new DateTime(2026, 2, 10));
        po.AddItem(_itemId1, "Purchased Item 1", 10m, 50m, 0m, "Nos");
        po.Submit();

        _poRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po }.AsQueryable()));

        var appService = new PurchaseOrderTrendsAppService(
            _poRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new PurchaseOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Quarterly",
            BasedOn = "Item",
            GroupBy = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });

        result.Rows.Count.ShouldBe(2); // 1 header item + 1 detail supplier
        var itemHeader = result.Rows[0];
        itemHeader.EntityCode.ShouldBe("ITEM-P1");
        itemHeader.GroupByValue.ShouldBeNull();
        itemHeader.TotalQty.ShouldBe(10m);
        itemHeader.TotalAmount.ShouldBe(500m);

        var supplierDetail = result.Rows[1];
        supplierDetail.GroupByValue.ShouldBe("Premier Supplier");
        supplierDetail.TotalQty.ShouldBe(10m);
        supplierDetail.TotalAmount.ShouldBe(500m);
    }

    [Fact]
    public async Task GetReportAsync_ClosedOrders_FilteredByDefault()
    {
        var poOpen = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-OPEN", new DateTime(2026, 3, 1));
        poOpen.AddItem(_itemId1, "Purchased Item 1", 2m, 100m, 0m, "Nos");
        poOpen.Submit();

        var poClosed = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-CLOSED", new DateTime(2026, 3, 1));
        poClosed.AddItem(_itemId1, "Purchased Item 1", 5m, 100m, 0m, "Nos");
        poClosed.Submit();
        poClosed.Close();

        _poRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { poOpen, poClosed }.AsQueryable()));

        var appService = new PurchaseOrderTrendsAppService(
            _poRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        // Without IncludeClosedOrders
        var result1 = await appService.GetReportAsync(new PurchaseOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            IncludeClosedOrders = false,
        });

        result1.TotalRow.TotalAmount.ShouldBe(200m);

        // With IncludeClosedOrders
        var result2 = await appService.GetReportAsync(new PurchaseOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Monthly",
            BasedOn = "Supplier",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
            IncludeClosedOrders = true,
        });

        result2.TotalRow.TotalAmount.ShouldBe(700m);
    }

    [Fact]
    public async Task GetReportAsync_BasedOnItemGroup_CalculatesGroupSums()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _companyId, _supplierId, "PO-004", new DateTime(2026, 4, 15));
        po.AddItem(_itemId1, "Purchased Item 1", 2m, 100m, 0m, "Nos"); // Hardware -> 200
        po.AddItem(_itemId2, "Purchased Item 2", 4m, 50m, 0m, "Nos");  // Consumables -> 200
        po.Submit();

        _poRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<PurchaseOrder, object>>[]>())
            .Returns(Task.FromResult(new List<PurchaseOrder> { po }.AsQueryable()));

        var appService = new PurchaseOrderTrendsAppService(
            _poRepo, _supplierRepo, _itemRepo, _fiscalYearRepo, _companyRepo);

        var result = await appService.GetReportAsync(new PurchaseOrderTrendsRequestDto
        {
            CompanyId = _companyId,
            Period = "Yearly",
            BasedOn = "ItemGroup",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 12, 31),
        });

        result.Rows.Count.ShouldBe(2);
        var hardwareRow = result.Rows.First(r => r.EntityName == "Hardware");
        hardwareRow.TotalQty.ShouldBe(2m);
        hardwareRow.TotalAmount.ShouldBe(200m);

        var consumablesRow = result.Rows.First(r => r.EntityName == "Consumables");
        consumablesRow.TotalQty.ShouldBe(4m);
        consumablesRow.TotalAmount.ShouldBe(200m);

        result.TotalRow.TotalQty.ShouldBe(6m);
        result.TotalRow.TotalAmount.ShouldBe(400m);
    }
}
