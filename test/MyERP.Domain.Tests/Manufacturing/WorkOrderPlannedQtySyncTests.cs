using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Unit tests verifying ERPNext PR #59419 (commit d687024b88):
/// Refresh planned quantity from active Work Orders after status update.
/// </summary>
public class WorkOrderPlannedQtySyncTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _fgWarehouseId = Guid.NewGuid();

    [Fact]
    public async Task RefreshPlannedQtyAsync_CalculatesSumOfRemainingQtyFromActiveWorkOrders()
    {
        // Setup:
        // WO 1: 50 qty, 0 produced, Submitted -> remaining 50
        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, Guid.NewGuid(), 50m)
        {
            FgWarehouseId = _fgWarehouseId
        };
        wo1.Submit();

        // WO 2: 30 qty, 10 produced, InProcess -> remaining 20
        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", _itemId, Guid.NewGuid(), 30m)
        {
            FgWarehouseId = _fgWarehouseId
        };
        wo2.Submit();
        wo2.Start();
        wo2.RecordProduction(10m, overproductionPercentage: 0m);

        // WO 3: 40 qty, 40 produced, Completed -> excluded (0)
        var wo3 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-003", _itemId, Guid.NewGuid(), 40m)
        {
            FgWarehouseId = _fgWarehouseId
        };
        wo3.Submit();
        wo3.Start();
        wo3.RecordProduction(40m, overproductionPercentage: 0m);

        // WO 4: 20 qty, Cancelled -> excluded (0)
        var wo4 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-004", _itemId, Guid.NewGuid(), 20m)
        {
            FgWarehouseId = _fgWarehouseId
        };
        wo4.Cancel();

        var workOrders = new List<WorkOrder> { wo1, wo2, wo3, wo4 };

        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders.AsQueryable()));

        var bin = new Bin(Guid.NewGuid(), _itemId, _fgWarehouseId) { PlannedQty = 0m };
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        var binService = new BinService(binRepo);
        var woManager = new WorkOrderManager(
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IRepository<BillOfMaterials, Guid>>(),
            Substitute.For<IRepository<ManufacturingSettings, Guid>>());

        await woManager.RefreshPlannedQtyAsync(woRepo, binService, _itemId, _fgWarehouseId);

        // Expected: 50 (wo1) + 20 (wo2) = 70
        bin.PlannedQty.ShouldBe(70m);
    }

    [Fact]
    public async Task UpdatePlannedQtyAsync_ClampsNegativePlannedQtyToZero()
    {
        var bin = new Bin(Guid.NewGuid(), _itemId, _fgWarehouseId) { PlannedQty = 10m };
        var binRepo = Substitute.For<IRepository<Bin, Guid>>();
        binRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Bin> { bin }.AsQueryable()));

        var binService = new BinService(binRepo);

        await binService.UpdatePlannedQtyAsync(_itemId, _fgWarehouseId, -25m);

        bin.PlannedQty.ShouldBe(0m);
    }
}
