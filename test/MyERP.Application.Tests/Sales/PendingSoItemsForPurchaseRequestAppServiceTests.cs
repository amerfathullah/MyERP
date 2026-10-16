using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class PendingSoItemsForPurchaseRequestAppServiceTests
{
    private readonly IRepository<SalesOrder, Guid> _soRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<MaterialRequest, Guid> _mrRepo = Substitute.For<IRepository<MaterialRequest, Guid>>();
    private readonly IRepository<ProductBundle, Guid> _bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
    private readonly ProductBundleDecompositionService _bundleService;

    private readonly PendingSoItemsForPurchaseRequestAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();

    public PendingSoItemsForPurchaseRequestAppServiceTests()
    {
        _bundleService = new ProductBundleDecompositionService(_bundleRepo);
        _appService = new PendingSoItemsForPurchaseRequestAppService(
            _soRepo,
            _customerRepo,
            _itemRepo,
            _mrRepo,
            _bundleService);

        // Default empty bundle repository
        _bundleRepo.GetQueryableAsync()
            .Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_WithNoMaterialRequests_ReturnsAllItemsAsPending()
    {
        var custId = Guid.NewGuid();
        var customer = new Customer(custId, _companyId, "Delta Corp");

        var item1Id = Guid.NewGuid();
        var item1 = new Item(item1Id, _companyId, "ITEM-1", "Gear", ItemType.Goods);

        var item2Id = Guid.NewGuid();
        var item2 = new Item(item2Id, _companyId, "ITEM-2", "Shaft", ItemType.Goods);

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-DELTA-1", DateTime.UtcNow);
        so.AddItem(item1Id, "Gear", 10m, 50m, 0m);
        so.AddItem(item2Id, "Shaft", 5m, 100m, 0m);
        so.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item1, item2 }));

        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest>().AsQueryable()));

        var result = await _appService.GetReportAsync(new PendingSoItemsForPurchaseRequestRequestDto
        {
            CompanyId = _companyId
        });

        result.TotalSalesOrders.ShouldBe(1);
        result.TotalItems.ShouldBe(2);
        result.TotalSoQuantity.ShouldBe(15m);
        result.TotalRequestedQuantity.ShouldBe(0m);
        result.TotalPendingQuantity.ShouldBe(15m);
        result.Rows.Count.ShouldBe(2);

        var r1 = result.Rows.First(r => r.ItemId == item1Id);
        r1.SoQuantity.ShouldBe(10m);
        r1.PendingQuantity.ShouldBe(10m);
        r1.RequestedQuantity.ShouldBe(0m);

        var r2 = result.Rows.First(r => r.ItemId == item2Id);
        r2.SoQuantity.ShouldBe(5m);
        r2.PendingQuantity.ShouldBe(5m);
        r2.RequestedQuantity.ShouldBe(0m);
    }

    [Fact]
    public async Task GetReportAsync_WithPartialMaterialRequest_ReducesPendingQty()
    {
        var custId = Guid.NewGuid();
        var customer = new Customer(custId, _companyId, "Echo Corp");

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-VALVE", "Steel Valve", ItemType.Goods);

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-ECHO-1", DateTime.UtcNow);
        so.AddItem(itemId, "Steel Valve", 10m, 100m, 0m);
        so.Submit();
        var soItemId = so.Items[0].Id;

        // Material Request for 6 units against this SO
        var mrId = Guid.NewGuid();
        var mr = new MaterialRequest(mrId, _companyId, "MR-2026-001", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(itemId, "Steel Valve", 6m, "Unit", salesOrderId: soId, salesOrderItemId: soItemId);
        mr.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var result = await _appService.GetReportAsync(new PendingSoItemsForPurchaseRequestRequestDto
        {
            CompanyId = _companyId
        });

        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SoQuantity.ShouldBe(10m);
        row.RequestedQuantity.ShouldBe(6m);
        row.PendingQuantity.ShouldBe(4m);
        row.MaterialRequests.ShouldContain("MR-2026-001");
    }

    [Fact]
    public async Task GetReportAsync_WhenFullyRequested_ExcludesItemFromReport()
    {
        var custId = Guid.NewGuid();
        var customer = new Customer(custId, _companyId, "Foxtrot Corp");

        var itemId = Guid.NewGuid();
        var item = new Item(itemId, _companyId, "ITEM-FULL", "Bearing", ItemType.Goods);

        var soId = Guid.NewGuid();
        var so = new SalesOrder(soId, _companyId, custId, "SO-FOX-1", DateTime.UtcNow);
        so.AddItem(itemId, "Bearing", 10m, 100m, 0m);
        so.Submit();
        var soItemId = so.Items[0].Id;

        // Material Request for full 10 units
        var mrId = Guid.NewGuid();
        var mr = new MaterialRequest(mrId, _companyId, "MR-2026-FULL", MaterialRequestType.Purchase, DateTime.UtcNow);
        mr.AddItem(itemId, "Bearing", 10m, "Unit", salesOrderId: soId, salesOrderItemId: soItemId);
        mr.Submit();

        _soRepo.WithDetailsAsync(Arg.Any<Expression<Func<SalesOrder, object>>[]>())
            .Returns(Task.FromResult(new List<SalesOrder> { so }.AsQueryable()));

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));

        _itemRepo.GetListAsync(Arg.Any<Expression<Func<Item, bool>>>())
            .Returns(Task.FromResult(new List<Item> { item }));

        _mrRepo.WithDetailsAsync(Arg.Any<Expression<Func<MaterialRequest, object>>[]>())
            .Returns(Task.FromResult(new List<MaterialRequest> { mr }.AsQueryable()));

        var result = await _appService.GetReportAsync(new PendingSoItemsForPurchaseRequestRequestDto
        {
            CompanyId = _companyId
        });

        result.Rows.Count.ShouldBe(0);
        result.TotalPendingQuantity.ShouldBe(0m);
    }
}
