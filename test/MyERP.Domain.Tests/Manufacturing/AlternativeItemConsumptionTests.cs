using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Unit tests for consuming alternative items in the ratio they were transferred
/// per ERPNext PR #60090 / commit 00efe4e233.
/// </summary>
public class AlternativeItemConsumptionTests
{
    private readonly WorkOrderProductionService _productionService;
    private readonly StockEntryManager _stockEntryManager;
    private readonly IRepository<Warehouse, Guid> _warehouseRepo = Substitute.For<IRepository<Warehouse, Guid>>();
    private readonly IRepository<Item, Guid> _itemRepo = Substitute.For<IRepository<Item, Guid>>();

    public AlternativeItemConsumptionTests()
    {
        _productionService = new WorkOrderProductionService(Substitute.For<IRepository<WorkOrder, Guid>>(), null);
        _stockEntryManager = new StockEntryManager(_warehouseRepo, _itemRepo, null!);
    }

    [Fact]
    public void CalculateAlternativeItemConsumption_NoAlternativeTransferred_ReturnsSingleBaseItem()
    {
        var baseItemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 100, null, Guid.NewGuid()) { OriginalItemId = null }
        };

        var splits = _productionService.CalculateAlternativeItemConsumption(
            baseItemId,
            requiredQty: 50,
            transferredItems: transferred,
            priorConsumedItems: Enumerable.Empty<StockEntryItem>());

        splits.Count.ShouldBe(1);
        splits[0].ItemId.ShouldBe(baseItemId);
        splits[0].OriginalItemId.ShouldBeNull();
        splits[0].Quantity.ShouldBe(50);
    }

    [Fact]
    public void CalculateAlternativeItemConsumption_OnlyAlternativeTransferred_ReturnsAlternativeItem()
    {
        var baseItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, altItemId, 100, null, Guid.NewGuid()) { OriginalItemId = baseItemId }
        };

        var splits = _productionService.CalculateAlternativeItemConsumption(
            baseItemId,
            requiredQty: 40,
            transferredItems: transferred,
            priorConsumedItems: Enumerable.Empty<StockEntryItem>());

        splits.Count.ShouldBe(1);
        splits[0].ItemId.ShouldBe(altItemId);
        splits[0].OriginalItemId.ShouldBe(baseItemId);
        splits[0].Quantity.ShouldBe(40);
    }

    [Fact]
    public void CalculateAlternativeItemConsumption_SplitTransfer_SplitsInTransferredRatio()
    {
        var baseItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Transferred: 60 base, 40 alt
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 60, null, Guid.NewGuid()) { OriginalItemId = null },
            new StockEntryItem(Guid.NewGuid(), entryId, altItemId, 40, null, Guid.NewGuid()) { OriginalItemId = baseItemId }
        };

        var splits = _productionService.CalculateAlternativeItemConsumption(
            baseItemId,
            requiredQty: 50,
            transferredItems: transferred,
            priorConsumedItems: Enumerable.Empty<StockEntryItem>());

        splits.Count.ShouldBe(2);

        var baseSplit = splits.Single(s => s.ItemId == baseItemId);
        baseSplit.OriginalItemId.ShouldBeNull();
        baseSplit.Quantity.ShouldBe(30); // 60 / 100 * 50 = 30

        var altSplit = splits.Single(s => s.ItemId == altItemId);
        altSplit.OriginalItemId.ShouldBe(baseItemId);
        altSplit.Quantity.ShouldBe(20); // 40 / 100 * 50 = 20
    }

    [Fact]
    public void CalculateAlternativeItemConsumption_PartialPriorConsumption_SplitsByAvailableRatio()
    {
        var baseItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Transferred: 60 base, 40 alt
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 60, null, Guid.NewGuid()) { OriginalItemId = null },
            new StockEntryItem(Guid.NewGuid(), entryId, altItemId, 40, null, Guid.NewGuid()) { OriginalItemId = baseItemId }
        };

        // Prior consumption: 40 base consumed, 0 alt consumed
        // Available: base = 20, alt = 40 (total available = 60)
        var priorConsumed = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 40, Guid.NewGuid(), null) { OriginalItemId = null }
        };

        var splits = _productionService.CalculateAlternativeItemConsumption(
            baseItemId,
            requiredQty: 30,
            transferredItems: transferred,
            priorConsumedItems: priorConsumed);

        splits.Count.ShouldBe(2);

        var baseSplit = splits.Single(s => s.ItemId == baseItemId);
        baseSplit.OriginalItemId.ShouldBeNull();
        baseSplit.Quantity.ShouldBe(10); // 20 / 60 * 30 = 10

        var altSplit = splits.Single(s => s.ItemId == altItemId);
        altSplit.OriginalItemId.ShouldBe(baseItemId);
        altSplit.Quantity.ShouldBe(20); // 40 / 60 * 30 = 20
    }

    [Fact]
    public void CalculateAlternativeItemConsumption_OverproductionAllAvailableUsed_FallsBackToTransferredRatio()
    {
        var baseItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        // Transferred: 50 base, 50 alt
        var transferred = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 50, null, Guid.NewGuid()) { OriginalItemId = null },
            new StockEntryItem(Guid.NewGuid(), entryId, altItemId, 50, null, Guid.NewGuid()) { OriginalItemId = baseItemId }
        };

        // Prior consumed: 50 base, 50 alt (available = 0 for both)
        var priorConsumed = new List<StockEntryItem>
        {
            new StockEntryItem(Guid.NewGuid(), entryId, baseItemId, 50, Guid.NewGuid(), null) { OriginalItemId = null },
            new StockEntryItem(Guid.NewGuid(), entryId, altItemId, 50, Guid.NewGuid(), null) { OriginalItemId = baseItemId }
        };

        var splits = _productionService.CalculateAlternativeItemConsumption(
            baseItemId,
            requiredQty: 20,
            transferredItems: transferred,
            priorConsumedItems: priorConsumed);

        splits.Count.ShouldBe(2);

        var baseSplit = splits.Single(s => s.ItemId == baseItemId);
        baseSplit.Quantity.ShouldBe(10);

        var altSplit = splits.Single(s => s.ItemId == altItemId);
        altSplit.Quantity.ShouldBe(10);
    }

    [Fact]
    public void ValidateManufactureBomComponentQty_MatchesBomWithAlternativeItem_Passes()
    {
        var companyId = Guid.NewGuid();
        var baseItemId = Guid.NewGuid();
        var altItemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            FgCompletedQty = 10
        };

        // BOM requires 5 units of base item per unit of FG -> 50 units total for 10 FG
        // Consumed: 30 base + 20 alternative (with OriginalItemId set to baseItemId)
        entry.AddItem(baseItemId, quantity: 30, sourceWarehouseId: whId, targetWarehouseId: null);
        entry.AddItem(altItemId, quantity: 20, sourceWarehouseId: whId, targetWarehouseId: null, originalItemId: baseItemId);

        var bomItems = new List<(Guid ItemId, string ItemCode, decimal Qty)>
        {
            (baseItemId, "RM-001", 5m)
        };

        Should.NotThrow(() =>
            _stockEntryManager.ValidateManufactureBomComponentQty(entry, bomItems, "BOM-001"));
    }

    [Fact]
    public void ValidateManufactureBomComponentQty_Mismatch_ThrowsBusinessException()
    {
        var companyId = Guid.NewGuid();
        var baseItemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            FgCompletedQty = 10
        };

        // BOM requires 50 units, but only 45 consumed
        entry.AddItem(baseItemId, quantity: 45, sourceWarehouseId: whId, targetWarehouseId: null);

        var bomItems = new List<(Guid ItemId, string ItemCode, decimal Qty)>
        {
            (baseItemId, "RM-001", 5m)
        };

        var ex = Should.Throw<BusinessException>(() =>
            _stockEntryManager.ValidateManufactureBomComponentQty(entry, bomItems, "BOM-001"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SetAllowAlternativeItemAsync_BothWoAndItemAllow_SetsTrue()
    {
        var companyId = Guid.NewGuid();
        var baseItemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10)
        {
            AllowAlternativeItem = true
        };

        var item = new Item(baseItemId, companyId, "RM-001", "Raw Material 1", ItemType.Goods)
        {
            AllowAlternativeItem = true
        };

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.FindAsync(wo.Id).Returns(wo);

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        itemRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Item> { item }.AsQueryable()));

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id
        };
        var line = entry.AddItem(baseItemId, quantity: 10, sourceWarehouseId: whId, targetWarehouseId: null);

        await _stockEntryManager.SetAllowAlternativeItemAsync(entry, woRepo, itemRepo);

        line.AllowAlternativeItem.ShouldBeTrue();
    }

    [Fact]
    public async Task SetAllowAlternativeItemAsync_WoDoesNotAllow_SetsFalse()
    {
        var companyId = Guid.NewGuid();
        var baseItemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        var wo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10)
        {
            AllowAlternativeItem = false
        };

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.FindAsync(wo.Id).Returns(wo);

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();

        var entry = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id
        };
        var line = entry.AddItem(baseItemId, quantity: 10, sourceWarehouseId: whId, targetWarehouseId: null, allowAlternativeItem: true);

        await _stockEntryManager.SetAllowAlternativeItemAsync(entry, woRepo, itemRepo);

        line.AllowAlternativeItem.ShouldBeFalse();
    }
}
