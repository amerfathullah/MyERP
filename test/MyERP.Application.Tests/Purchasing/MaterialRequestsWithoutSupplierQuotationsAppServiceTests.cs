using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Purchasing;

public class MaterialRequestsWithoutSupplierQuotationsAppServiceTests
{
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IRepository<SupplierQuotation, Guid> _sqRepo = Substitute.For<IRepository<SupplierQuotation, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly MaterialRequestsWithoutSupplierQuotationsAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public MaterialRequestsWithoutSupplierQuotationsAppServiceTests()
    {
        _appService = new MaterialRequestsWithoutSupplierQuotationsAppService(
            _mrRepo,
            _sqRepo,
            _itemRepo,
            _warehouseRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetReportAsync_ReturnsSubmittedPurchaseMrItems_WithoutSupplierQuotations()
    {
        // Arrange
        var company = new Company(_companyId, "Acme Logistics");
        _companyRepo.GetQueryableAsync().Returns(new List<Company> { company }.AsQueryable());

        var whId = Guid.NewGuid();
        var warehouse = new Warehouse(whId, _companyId, "Main Warehouse");
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { warehouse }.AsQueryable());

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "RAW-001", "Raw Copper Wire", ItemType.Goods) { Description = "Copper Coil 1mm" };
        var item2 = new Item(item2Id, _companyId, "RAW-002", "Aluminium Rod", ItemType.Goods) { Description = "Alu Rod 10mm" };
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item1, item2 }.AsQueryable());

        // MR 1: Submitted, Purchase, Item 1 (qty 100) — NOT quoted
        var mr1 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0001", MaterialRequestType.Purchase, DateTime.UtcNow.Date.AddDays(-2));
        mr1.AddItem(item1Id, "Raw Copper Wire", 100m, "Meters", whId);
        mr1.Submit();

        // MR 2: Submitted, Purchase, Item 2 (qty 50) — Quoted in SQ1
        var mr2 = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-0002", MaterialRequestType.Purchase, DateTime.UtcNow.Date.AddDays(-1));
        mr2.AddItem(item2Id, "Aluminium Rod", 50m, "Nos", whId);
        mr2.Submit();

        var mr2ItemId = mr2.Items[0].Id;

        // Supplier Quotation linking to MR 2's item
        var sq1 = new SupplierQuotation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow.Date);
        sq1.AddItem(item2Id, 50m, 12m, "Aluminium Rod", "Nos");
        sq1.Items[0].MaterialRequestItemId = mr2ItemId;
        sq1.Submit();

        _sqRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SupplierQuotation, object>>[]>())
            .Returns(new List<SupplierQuotation> { sq1 }.AsQueryable());

        _mrRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<MaterialRequest, object>>[]>())
            .Returns(new List<MaterialRequest> { mr1, mr2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new MaterialRequestsWithoutSupplierQuotationsFilterDto
        {
            CompanyId = _companyId
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalPendingRequests.ShouldBe(1);
        result.TotalPendingItems.ShouldBe(1);
        result.TotalPendingQuantity.ShouldBe(100m);

        var row = result.Items.Single();
        row.RequestNumber.ShouldBe("MAT-REQ-0001");
        row.ItemCode.ShouldBe("RAW-001");
        row.ItemName.ShouldBe("Raw Copper Wire");
        row.Description.ShouldBe("Copper Coil 1mm");
        row.Quantity.ShouldBe(100m);
        row.WarehouseName.ShouldBe("Main Warehouse");
        row.CompanyName.ShouldBe("Acme Logistics");
    }

    [Fact]
    public async Task GetReportAsync_ExcludesDraftCancelledOrNonPurchaseMaterialRequests()
    {
        // Arrange
        _sqRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SupplierQuotation, object>>[]>())
            .Returns(new List<SupplierQuotation>().AsQueryable());

        var itemId = Guid.NewGuid();
        // Draft Purchase MR
        var draftMr = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-DRAFT", MaterialRequestType.Purchase, DateTime.UtcNow.Date);
        draftMr.AddItem(itemId, "Item 1", 10m, "Nos");

        // Cancelled Purchase MR
        var cancelledMr = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-CANCEL", MaterialRequestType.Purchase, DateTime.UtcNow.Date);
        cancelledMr.AddItem(itemId, "Item 1", 10m, "Nos");
        cancelledMr.Submit();
        cancelledMr.Cancel();

        // Submitted Transfer MR (not Purchase)
        var transferMr = new MaterialRequest(Guid.NewGuid(), _companyId, "MAT-REQ-TRANS", MaterialRequestType.MaterialTransfer, DateTime.UtcNow.Date);
        transferMr.AddItem(itemId, "Item 1", 10m, "Nos");
        transferMr.Submit();

        _mrRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<MaterialRequest, object>>[]>())
            .Returns(new List<MaterialRequest> { draftMr, cancelledMr, transferMr }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new MaterialRequestsWithoutSupplierQuotationsFilterDto());

        // Assert
        result.ShouldNotBeNull();
        result.TotalPendingRequests.ShouldBe(0);
        result.TotalPendingItems.ShouldBe(0);
        result.TotalPendingQuantity.ShouldBe(0m);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_FiltersByWarehouseAndDateRange()
    {
        // Arrange
        var company = new Company(_companyId, "Acme");
        _companyRepo.GetQueryableAsync().Returns(new List<Company> { company }.AsQueryable());

        var wh1Id = Guid.NewGuid();
        var wh2Id = Guid.NewGuid();
        var wh1 = new Warehouse(wh1Id, _companyId, "WH 1");
        var wh2 = new Warehouse(wh2Id, _companyId, "WH 2");
        _warehouseRepo.GetQueryableAsync().Returns(new List<Warehouse> { wh1, wh2 }.AsQueryable());

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITM-1", "Test Item", ItemType.Goods);
        _itemRepo.GetQueryableAsync().Returns(new List<Item> { item }.AsQueryable());

        _sqRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<SupplierQuotation, object>>[]>())
            .Returns(new List<SupplierQuotation>().AsQueryable());

        var today = DateTime.UtcNow.Date;

        var mrWh1 = new MaterialRequest(Guid.NewGuid(), _companyId, "REQ-WH1", MaterialRequestType.Purchase, today);
        mrWh1.AddItem(itemId, "Test Item", 15m, "Nos", wh1Id);
        mrWh1.Submit();

        var mrWh2 = new MaterialRequest(Guid.NewGuid(), _companyId, "REQ-WH2", MaterialRequestType.Purchase, today);
        mrWh2.AddItem(itemId, "Test Item", 25m, "Nos", wh2Id);
        mrWh2.Submit();

        _mrRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<MaterialRequest, object>>[]>())
            .Returns(new List<MaterialRequest> { mrWh1, mrWh2 }.AsQueryable());

        // Act
        var result = await _appService.GetReportAsync(new MaterialRequestsWithoutSupplierQuotationsFilterDto
        {
            WarehouseId = wh1Id,
            FromDate = today.AddDays(-1),
            ToDate = today.AddDays(1)
        });

        // Assert
        result.ShouldNotBeNull();
        result.TotalPendingRequests.ShouldBe(1);
        result.Items.Single().RequestNumber.ShouldBe("REQ-WH1");
        result.Items.Single().Quantity.ShouldBe(15m);
    }
}
