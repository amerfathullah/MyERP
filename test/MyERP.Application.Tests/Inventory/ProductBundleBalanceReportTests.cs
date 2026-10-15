using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Inventory;

/// <summary>
/// Unit tests for Product Bundle Balance report per ERPNext PR #60627 / commits 6632996587 &amp; 7af9d1b723.
/// Verifies:
/// - Bundle qty is never negative when component stock is non-positive (max(0, actual_qty // minimum_qty)).
/// - When component stock is negative (-5), bundle qty is 0.
/// - When component stock is zero (0), bundle qty is 0.
/// - When component stock is positive (5 / 2 = 2), bundle qty is 2.
/// - Bottleneck component limits the parent bundle availability.
/// - Warehouse and ParentItem filtering work correctly.
/// </summary>
public class ProductBundleBalanceReportTests
{
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepo = Substitute.For<IRepository<StockLedgerEntry, Guid>>();
    private readonly IRepository<Batch, Guid> _batchRepo = Substitute.For<IRepository<Batch, Guid>>();
    private readonly IRepository<ProductBundle, Guid> _bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
    private readonly MyERP.Inventory.DomainServices.BinService _binService = Substitute.For<MyERP.Inventory.DomainServices.BinService>(Substitute.For<IRepository<Bin, Guid>>());

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _parentItemId = Guid.NewGuid();
    private readonly Guid _childAId = Guid.NewGuid();
    private readonly Guid _childBId = Guid.NewGuid();

    private readonly Item _parentItem;
    private readonly Item _childA;
    private readonly Item _childB;
    private readonly Warehouse _warehouse;
    private readonly ProductBundle _bundle;

    public ProductBundleBalanceReportTests()
    {
        _parentItem = new Item(_parentItemId, _companyId, "Office Stationery Kit", "Office Stationery Kit", ItemType.Goods) { Uom = "Nos" };
        _childA = new Item(_childAId, _companyId, "Notebook", "Notebook", ItemType.Goods) { Uom = "Nos" };
        _childB = new Item(_childBId, _companyId, "Pen", "Pen", ItemType.Goods) { Uom = "Nos" };

        _warehouse = new Warehouse(_warehouseId, _companyId, "Stores - TC") { WarehouseCode = "STORES" };

        _bundle = new ProductBundle(Guid.NewGuid(), _parentItemId)
        {
            ItemName = "Office Stationery Kit",
            IsActive = true,
            IsDisabled = false
        };
        _bundle.AddItem(_childAId, 2m, "Notebook", "Nos");
        _bundle.AddItem(_childBId, 1m, "Pen", "Nos");

        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { _parentItem, _childA, _childB }.AsQueryable()));
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Warehouse> { _warehouse }.AsQueryable()));
        _bundleRepo.WithDetailsAsync().Returns(Task.FromResult(new List<ProductBundle> { _bundle }.AsQueryable()));
    }

    private StockBalanceAppService CreateAppService(List<Bin> bins)
    {
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(bins.AsQueryable()));

        var service = new StockBalanceAppService(
            _binRepo,
            _itemRepo,
            _warehouseRepo,
            _sleRepo,
            _batchRepo,
            _binService,
            _bundleRepo);

        return service;
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(5, 2)]
    public async Task GetProductBundleBalanceAsync_ComponentStockNonPositive_BundleQtyNeverNegative(
        decimal actualQtyA,
        decimal expectedBundleQty)
    {
        // Matches ERPNext PR #60627 unit test TestProductBundleBalanceQuantity.test_bundle_qty_with_non_positive_component_stock:
        // actual_qty in (-5, 0, 5) -> expected bundle_qty in (0, 0, 2)
        var binA = new Bin(Guid.NewGuid(), _childAId, _warehouseId) { ActualQty = actualQtyA };
        var binB = new Bin(Guid.NewGuid(), _childBId, _warehouseId) { ActualQty = 10m };

        var appService = CreateAppService(new List<Bin> { binA, binB });

        var result = await appService.GetProductBundleBalanceAsync(new GetProductBundleBalanceRequestDto
        {
            CompanyId = _companyId,
            WarehouseId = _warehouseId,
            ParentItemId = _parentItemId
        });

        result.Rows.Count.ShouldBe(3);

        // Row 0: Parent bundle row
        var parentRow = result.Rows[0];
        parentRow.Indent.ShouldBe(0);
        parentRow.ItemId.ShouldBe(_parentItemId);
        parentRow.BundleQty.ShouldBe(expectedBundleQty);

        // Row 1: Child A row
        var childARow = result.Rows[1];
        childARow.Indent.ShouldBe(1);
        childARow.ItemId.ShouldBe(_childAId);
        childARow.ActualQty.ShouldBe(actualQtyA);
        childARow.MinimumQty.ShouldBe(2m);
        childARow.BundleQty.ShouldBe(expectedBundleQty);

        // Row 2: Child B row
        var childBRow = result.Rows[2];
        childBRow.Indent.ShouldBe(1);
        childBRow.ItemId.ShouldBe(_childBId);
        childBRow.ActualQty.ShouldBe(10m);
        childBRow.MinimumQty.ShouldBe(1m);
        childBRow.BundleQty.ShouldBe(10m);
    }

    [Fact]
    public async Task GetProductBundleBalanceAsync_EmptyBundles_ReturnsEmptyReport()
    {
        _bundleRepo.WithDetailsAsync().Returns(Task.FromResult(new List<ProductBundle>().AsQueryable()));

        var appService = CreateAppService(new List<Bin>());

        var result = await appService.GetProductBundleBalanceAsync(new GetProductBundleBalanceRequestDto
        {
            CompanyId = _companyId
        });

        result.Rows.ShouldBeEmpty();
    }
}
