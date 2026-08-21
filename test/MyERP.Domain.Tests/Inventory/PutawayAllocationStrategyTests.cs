using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for PutawayService allocation strategy (Gotchas #2718, #2719):
/// 1. Prioritizes candidate warehouses by Priority ASC then FreeSpace DESC
/// 2. Whole number UOM uses FLOOR rounding
/// 3. Returns unallocated remaining qty when warehouse capacity is saturated
/// </summary>
public class PutawayAllocationStrategyTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _wh1 = Guid.NewGuid();
    private readonly Guid _wh2 = Guid.NewGuid();

    [Fact]
    public async Task Putaway_AllocatesByPriorityAsc_ThenFreeSpaceDesc()
    {
        var ruleRepo = Substitute.For<IRepository<PutawayRule, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        var rule1 = new PutawayRule(Guid.NewGuid(), _companyId, _wh1)
        {
            ItemId = _itemId,
            StockCapacity = 50,
            Priority = 1
        };
        var rule2 = new PutawayRule(Guid.NewGuid(), _companyId, _wh2)
        {
            ItemId = _itemId,
            StockCapacity = 100,
            Priority = 1 // Same priority, but wh2 has more free space
        };

        var rules = new List<PutawayRule> { rule1, rule2 }.AsQueryable();
        ruleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        var bins = new List<Bin>().AsQueryable();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(bins));

        var service = new PutawayService(ruleRepo, binRepo);

        // Allocate 120 units: Wh2 (100 free space) gets 100, Wh1 (50 free space) gets 20
        var result = await service.AllocateAsync(_companyId, _itemId, 120m);

        Assert.Equal(2, result.Count);
        // Wh2 should be first because it has 100 free space vs 50 for Wh1 at the same priority
        Assert.Equal(_wh2, result[0].WarehouseId);
        Assert.Equal(100m, result[0].Qty);

        Assert.Equal(_wh1, result[1].WarehouseId);
        Assert.Equal(20m, result[1].Qty);
    }

    [Fact]
    public async Task Putaway_FloorRounding_ForWholeNumberUom()
    {
        var ruleRepo = Substitute.For<IRepository<PutawayRule, Guid>>();
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();

        var rule1 = new PutawayRule(Guid.NewGuid(), _companyId, _wh1)
        {
            ItemId = _itemId,
            StockCapacity = 10,
            Priority = 1
        };

        var rules = new List<PutawayRule> { rule1 }.AsQueryable();
        ruleRepo.GetQueryableAsync().Returns(Task.FromResult(rules));

        // Bin has 2.5 units already, capacity is 10 -> available is 7.5
        var bin = new Bin(Guid.NewGuid(), _itemId, _wh1) { ActualQty = 2.5m };
        var bins = new List<Bin> { bin }.AsQueryable();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(bins));

        var service = new PutawayService(ruleRepo, binRepo);

        // Allocate 10 units with mustBeWholeNumber=true
        var result = await service.AllocateAsync(_companyId, _itemId, 10m, mustBeWholeNumber: true);

        Assert.Equal(2, result.Count);
        // Available was 7.5, Math.Floor(7.5) = 7
        Assert.Equal(_wh1, result[0].WarehouseId);
        Assert.Equal(7m, result[0].Qty);

        // Remaining 3 units unallocated
        Assert.True(result[1].IsUnallocated);
        Assert.Equal(3m, result[1].Qty);
    }
}
