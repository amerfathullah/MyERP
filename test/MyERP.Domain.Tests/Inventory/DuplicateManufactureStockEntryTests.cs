using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Inventory;

public class DuplicateManufactureStockEntryTests
{
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<WorkOrder, Guid> _woRepository;
    private readonly IRepository<StockEntry, Guid> _seRepository;
    private readonly StockEntryManager _manager;

    public DuplicateManufactureStockEntryTests()
    {
        _warehouseRepository = Substitute.For<IRepository<Warehouse, Guid>>();
        _woRepository = Substitute.For<IRepository<WorkOrder, Guid>>();
        _seRepository = Substitute.For<IRepository<StockEntry, Guid>>();
        _manager = new StockEntryManager(_warehouseRepository, null!, null!);
    }

    [Fact]
    public async Task ValidateDuplicateManufactureEntry_WhenUnderQty_Succeeds()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var targetWarehouseId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-001", itemId, Guid.NewGuid(), 10m);

        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var existingSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-001",
            WorkOrderId = woId,
        };
        existingSe.AddItem(itemId, 5m, null, targetWarehouseId, 100m, isFinishedItem: true);

        var queryable = new List<StockEntry> { existingSe }.AsQueryable();
        _seRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var newSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-002",
            WorkOrderId = woId,
        };
        newSe.AddItem(itemId, 5m, null, targetWarehouseId, 100m, isFinishedItem: true);

        await Should.NotThrowAsync(() => _manager.ValidateDuplicateManufactureEntryAsync(newSe, _woRepository, _seRepository, overproductionPercentage: 0m));
    }

    [Fact]
    public async Task ValidateDuplicateManufactureEntry_WhenAlreadyFullyManufactured_ThrowsDuplicateRecord()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var targetWarehouseId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-002", itemId, Guid.NewGuid(), 10m);

        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var existingSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-001",
            WorkOrderId = woId,
        };
        existingSe.AddItem(itemId, 10m, null, targetWarehouseId, 100m, isFinishedItem: true);

        var queryable = new List<StockEntry> { existingSe }.AsQueryable();
        _seRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var newSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-002",
            WorkOrderId = woId,
        };
        newSe.AddItem(itemId, 2m, null, targetWarehouseId, 100m, isFinishedItem: true);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _manager.ValidateDuplicateManufactureEntryAsync(newSe, _woRepository, _seRepository, overproductionPercentage: 0m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.DuplicateRecord);
    }

    [Fact]
    public async Task ValidateDuplicateManufactureEntry_WithOverproductionAllowance_PermitsExtraWithinAllowance()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var targetWarehouseId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-003", itemId, Guid.NewGuid(), 100m);

        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var existingSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-001",
            WorkOrderId = woId,
        };
        existingSe.AddItem(itemId, 100m, null, targetWarehouseId, 100m, isFinishedItem: true);

        var queryable = new List<StockEntry> { existingSe }.AsQueryable();
        _seRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var newSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-002",
            WorkOrderId = woId,
        };
        newSe.AddItem(itemId, 5m, null, targetWarehouseId, 100m, isFinishedItem: true);

        // 10% allowance on 100m = 110m allowed. Existing is 100m < 110m allowed, so second entry is permitted.
        await Should.NotThrowAsync(() => _manager.ValidateDuplicateManufactureEntryAsync(newSe, _woRepository, _seRepository, overproductionPercentage: 10m));
    }

    [Fact]
    public async Task ValidateDuplicateManufactureEntry_WhenTrackSemiFinishedGoods_SkipsValidation()
    {
        var woId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var targetWarehouseId = Guid.NewGuid();
        var wo = new WorkOrder(woId, Guid.NewGuid(), "WO-004", itemId, Guid.NewGuid(), 10m)
        {
            TrackSemiFinishedGoods = true
        };

        _woRepository.FindAsync(woId).Returns(Task.FromResult<WorkOrder?>(wo));

        var existingSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-001",
            WorkOrderId = woId,
        };
        existingSe.AddItem(itemId, 10m, null, targetWarehouseId, 100m, isFinishedItem: true);

        var queryable = new List<StockEntry> { existingSe }.AsQueryable();
        _seRepository.GetQueryableAsync().Returns(Task.FromResult(queryable));

        var newSe = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.Manufacture, DateTime.UtcNow)
        {
            EntryNumber = "MAT-STE-002",
            WorkOrderId = woId,
        };

        await Should.NotThrowAsync(() => _manager.ValidateDuplicateManufactureEntryAsync(newSe, _woRepository, _seRepository, overproductionPercentage: 0m));
    }
}
