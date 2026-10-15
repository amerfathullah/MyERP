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

namespace MyERP.Sales;

/// <summary>
/// Unit tests for Available Stock for Packing Items report per ERPNext PR #60058 / commits bbd6f880a7 &amp; 8d9300a549.
/// Verifies:
/// - Packable bundles = min(floor(round(projected_qty / component_qty, 9))) across all bundle components.
/// - Fractional bundles are floored (13 / 2 = 6.5 -> 6).
/// - Float division noise is eliminated (0.3 / 0.1 = 3, not 2).
/// - Genuine fraction is not rounded up (8.99 / 3 = 2.996... -> 2).
/// - Negative or zero projected stock drops the warehouse row.
/// - Bottleneck component limits whole bundle availability.
/// - Company filter scopes warehouses to that company.
/// </summary>
public class AvailableStockForPackingItemsReportTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();
    private readonly IRepository<ProductBundle, Guid> _bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
    private readonly IRepository<Bin, Guid> _binRepo = Substitute.For<IRepository<Bin, Guid>>();
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();

    private readonly SalesAnalyticsAppService _appService;

    private readonly Guid _companyAId = Guid.NewGuid();
    private readonly Guid _companyBId = Guid.NewGuid();
    private readonly Guid _warehouse1Id = Guid.NewGuid();
    private readonly Guid _warehouse2Id = Guid.NewGuid();
    private readonly Guid _bundleParentItemId = Guid.NewGuid();
    private readonly Guid _component1Id = Guid.NewGuid();
    private readonly Guid _component2Id = Guid.NewGuid();

    public AvailableStockForPackingItemsReportTests()
    {
        _appService = new SalesAnalyticsAppService(_siRepo, _customerRepo, _itemRepo);

        var lazySp = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        lazySp.LazyGetRequiredService<IRepository<ProductBundle, Guid>>().Returns(_bundleRepo);
        lazySp.LazyGetRequiredService<IRepository<Bin, Guid>>().Returns(_binRepo);
        lazySp.LazyGetRequiredService<IRepository<Warehouse, Guid>>().Returns(_warehouseRepo);

        _appService.LazyServiceProvider = lazySp;

        // Default warehouse mock: Warehouse 1 in Company A, Warehouse 2 in Company B
        var warehouses = new List<Warehouse>
        {
            new Warehouse(_warehouse1Id, _companyAId, "Warehouse A-1") { WarehouseCode = "WH-A1" },
            new Warehouse(_warehouse2Id, _companyBId, "Warehouse B-1") { WarehouseCode = "WH-B1" }
        };
        _warehouseRepo.GetQueryableAsync().Returns(Task.FromResult(warehouses.AsQueryable()));

        // Default item mock
        var items = new List<Item>
        {
            new Item(_bundleParentItemId, _companyAId, "BUNDLE-01", "Gaming PC Bundle", ItemType.Goods),
            new Item(_component1Id, _companyAId, "COMP-CPU", "CPU Component", ItemType.Goods),
            new Item(_component2Id, _companyAId, "COMP-RAM", "RAM Component", ItemType.Goods),
        };
        _itemRepo.GetQueryableAsync().Returns(Task.FromResult(items.AsQueryable()));
    }

    [Fact]
    public async Task FractionalBundles_AreFloored()
    {
        // Arrange: Bundle requires 2 units of component 1. Warehouse has 13 units.
        // 13 / 2 = 6.5 -> must report exactly 6 whole bundles.
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 2m, "CPU Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = 13m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].BundleItemId.ShouldBe(_bundleParentItemId);
        result[0].WarehouseId.ShouldBe(_warehouse1Id);
        result[0].AvailablePackableBundles.ShouldBe(6m);
    }

    [Fact]
    public async Task FractionalStock_NotLostToFloatDivisionNoise()
    {
        // Arrange: Bundle requires 0.1 units of component 1. Warehouse has 0.3 units.
        // Float 0.3 / 0.1 is 2.9999999999999996; must report 3 whole bundles.
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 0.1m, "CPU Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = 0.3m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].AvailablePackableBundles.ShouldBe(3m);
    }

    [Fact]
    public async Task GenuineFraction_IsNotRoundedUp()
    {
        // Arrange: Bundle requires 3 units. Warehouse has 8.99 units.
        // 8.99 / 3 = 2.9966...; must report 2 whole bundles (must not round up to 3).
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 3m, "CPU Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = 8.99m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].AvailablePackableBundles.ShouldBe(2m);
    }

    [Fact]
    public async Task NegativeOrZeroProjected_DropsRow()
    {
        // Arrange: Bundle requires 3 units. Warehouse has negative projected qty (-30).
        // Negative projected stock cannot yield any bundles -> row dropped.
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 3m, "CPU Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = -30m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task MultiComponentBundle_BottleneckComponentGoverns()
    {
        // Arrange: Bundle requires:
        // - 1 CPU (warehouse has 10 -> yields 10)
        // - 2 RAM (warehouse has 5 -> yields 5 / 2 = 2.5 -> 2)
        // Bottleneck component 2 governs -> yields 2 complete bundles.
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 1m, "CPU Component");
        bundle.AddItem(_component2Id, 2m, "RAM Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var binCpu = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = 10m };
        var binRam = new Bin(Guid.NewGuid(), _component2Id, _warehouse1Id) { ActualQty = 5m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { binCpu, binRam }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].AvailablePackableBundles.ShouldBe(2m);
    }

    [Fact]
    public async Task CompanyFilter_ScopesWarehouses()
    {
        // Arrange: Warehouse 1 in Company A has 10 units (yields 5 bundles of 2).
        // Warehouse 2 in Company B has 8 units (yields 4 bundles of 2).
        // Filtering by Company A must only return Warehouse 1.
        var bundle = new ProductBundle(Guid.NewGuid(), _bundleParentItemId) { ItemName = "Gaming PC Bundle" };
        bundle.AddItem(_component1Id, 2m, "CPU Component");
        _bundleRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<ProductBundle, object>>[]>())
            .Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var binWh1 = new Bin(Guid.NewGuid(), _component1Id, _warehouse1Id) { ActualQty = 10m };
        var binWh2 = new Bin(Guid.NewGuid(), _component1Id, _warehouse2Id) { ActualQty = 8m };
        _binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { binWh1, binWh2 }.AsQueryable()));

        // Act
        var result = await _appService.GetAvailableStockForPackingItemsReportAsync(new AvailableStockForPackingItemsInputDto
        {
            CompanyId = _companyAId,
            BundleItemId = _bundleParentItemId
        });

        // Assert
        result.Count.ShouldBe(1);
        result[0].WarehouseId.ShouldBe(_warehouse1Id);
        result[0].AvailablePackableBundles.ShouldBe(5m);
    }
}
