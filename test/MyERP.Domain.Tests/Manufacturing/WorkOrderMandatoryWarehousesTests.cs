using System;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

public class WorkOrderMandatoryWarehousesTests
{
    private readonly WorkOrderManager _manager;

    public WorkOrderMandatoryWarehousesTests()
    {
        _manager = new WorkOrderManager(null!, null!, null!);
    }

    [Fact]
    public void ValidateMandatoryWarehouses_WhenWipWarehouseMissing_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            FgWarehouseId = Guid.NewGuid()
        };

        var ex = Should.Throw<BusinessException>(() => _manager.ValidateMandatoryWarehouses(wo, skipTransfer: false));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateMandatoryWarehouses_WhenSkipTransfer_AllowsMissingWipWarehouse()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            FgWarehouseId = Guid.NewGuid()
        };

        Should.NotThrow(() => _manager.ValidateMandatoryWarehouses(wo, skipTransfer: true));
    }

    [Fact]
    public void ValidateMandatoryWarehouses_WhenFgWarehouseMissingAndNotSemiFG_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            WipWarehouseId = Guid.NewGuid(),
            TrackSemiFinishedGoods = false
        };

        var ex = Should.Throw<BusinessException>(() => _manager.ValidateMandatoryWarehouses(wo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void ValidateMandatoryWarehouses_WhenFgWarehouseMissingAndTrackSemiFinishedGoods_Passes()
    {
        // Per ERPNext PR #9df527bf3f: Target Warehouse is optional for semi-FG work orders
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-004", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            WipWarehouseId = Guid.NewGuid(),
            TrackSemiFinishedGoods = true
        };

        Should.NotThrow(() => _manager.ValidateMandatoryWarehouses(wo));
    }

    [Fact]
    public void ValidateMandatoryWarehouses_WhenAllWarehousesProvided_Passes()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-005", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            WipWarehouseId = Guid.NewGuid(),
            FgWarehouseId = Guid.NewGuid()
        };

        Should.NotThrow(() => _manager.ValidateMandatoryWarehouses(wo));
    }
}
