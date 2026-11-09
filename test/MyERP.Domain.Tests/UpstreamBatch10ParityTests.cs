using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using MyERP.Settings;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 10:
/// - 00a1268b74 / c3f3c96a2c: Recalculate operating cost after draft reloads operations from BOM and reset costs when BOM has no operations.
/// - 710a59f031 / 339d8d6ebd: Deliver product bundle components at the qty the Sales Order packed after a component UOM change.
/// - ea7e451bcf / f0243bace9: Check returned bundle components at the qty the Sales Order packed after a component UOM change.
/// - f2f2d83a7e: Read serial and batch selector setting from boot / client config without requiring Stock Settings permissions.
/// </summary>
public class UpstreamBatch10ParityTests
{
    [Fact]
    public void WorkOrder_CalculateOperatingCost_ResetsOperatingCostWhenBomHasNoOperations_00a1268b74_c3f3c96a2c()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-001", Guid.NewGuid(), Guid.NewGuid(), 1m)
        {
            AdditionalOperatingCost = 25m
        };

        // Initially with operations cost 60m
        wo.CalculateOperatingCost(60m);
        wo.PlannedOperatingCost.ShouldBe(60m);
        wo.TotalOperatingCost.ShouldBe(85m);

        // When updated to BOM without operations (operating cost = 0)
        wo.CalculateOperatingCost(0m);
        wo.PlannedOperatingCost.ShouldBe(0m);
        wo.TotalOperatingCost.ShouldBe(25m); // equals AdditionalOperatingCost
    }

    [Fact]
    public async Task ProductBundleDecompositionService_PreservesSalesOrderPackedRatioAfterComponentUomChange_710a59f031_ea7e451bcf()
    {
        var bundleRepo = Substitute.For<IRepository<ProductBundle, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var uomService = Substitute.For<UomConversionService>(
            Substitute.For<IRepository<UomConversion, Guid>>());

        var parentItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();

        var bundle = new ProductBundle(bundleId, parentItemId);
        bundle.AddItem(componentItemId, 1m, "Component Item", "Box");

        bundleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<ProductBundle> { bundle }.AsQueryable()));

        var compItem = new Item(componentItemId, Guid.NewGuid(), "COMP-001", "Component Item", ItemType.Goods)
        {
            Uom = "Nos"
        };
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { compItem }.AsQueryable()));

        // Current conversion factor changed: 1 Box = 12 Nos
        uomService.GetConversionFactorAsync(componentItemId, "Box", "Nos", null).Returns(Task.FromResult(12m));

        var service = new ProductBundleDecompositionService(bundleRepo, itemRepo, uomService);

        // Case 1: Without Sales Order packed override, uses current conversion factor (1 * 12 * 2 = 24 Nos)
        var freshComponents = await service.DecomposeAsync(parentItemId, transactionQty: 2m, parentRate: 200m);
        freshComponents.Count.ShouldBe(1);
        freshComponents[0].Qty.ShouldBe(24m);

        // Case 2: With Sales Order packed ratio override (was ordered when 1 Box = 10 Nos, so 10 Nos per bundle unit)
        var orderPackedRatio = new Dictionary<Guid, decimal>
        {
            [componentItemId] = 10m
        };
        var orderedComponents = await service.DecomposeAsync(
            parentItemId, transactionQty: 2m, parentRate: 200m, componentStockQtyPerUnit: orderPackedRatio);
        orderedComponents.Count.ShouldBe(1);
        orderedComponents[0].Qty.ShouldBe(20m); // Keeps 20 Nos, NOT 24 Nos!
    }

    [Fact]
    public void StockSettings_DisableSerialNoAndBatchSelector_IsConfiguredWithClientVisibility_f2f2d83a7e()
    {
        var provider = new MyERPSettingDefinitionProvider();
        var dict = new Dictionary<string, SettingDefinition>();
        var context = new SettingDefinitionContext(dict);
        provider.Define(context);

        var setting = context.GetOrNull(MyERPSettings.Stock.DisableSerialNoAndBatchSelector);
        setting.ShouldNotBeNull();
        setting.DefaultValue.ShouldBe("false");
        setting.IsVisibleToClients.ShouldBeTrue();
    }
}
