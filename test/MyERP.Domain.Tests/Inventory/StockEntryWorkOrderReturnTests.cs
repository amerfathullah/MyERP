using System;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Inventory;

public class StockEntryWorkOrderReturnTests
{
    private readonly StockEntryManager _manager;

    public StockEntryWorkOrderReturnTests()
    {
        _manager = new StockEntryManager(null!, null!, null!);
    }

    [Fact]
    public void ValidateWorkOrderStatusForReturn_WhenNotReturn_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        // Status is Draft
        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = false
        };

        Should.NotThrow(() => _manager.ValidateWorkOrderStatusForReturn(se, wo));
    }

    [Fact]
    public void ValidateWorkOrderStatusForReturn_WhenWorkOrderCompleted_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(10m); // Marks completed

        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };

        Should.NotThrow(() => _manager.ValidateWorkOrderStatusForReturn(se, wo));
    }

    [Fact]
    public void ValidateWorkOrderStatusForReturn_WhenWorkOrderClosed_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Close();

        wo.Status.ShouldBe(WorkOrderStatus.Closed);

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };

        Should.NotThrow(() => _manager.ValidateWorkOrderStatusForReturn(se, wo));
    }

    [Fact]
    public void ValidateWorkOrderStatusForReturn_WhenWorkOrderInProcess_ThrowsInvalidStatusTransition()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();

        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };

        var ex = Should.Throw<BusinessException>(() => _manager.ValidateWorkOrderStatusForReturn(se, wo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Components can be returned only after Work Order WO-001 is Completed or Closed");
    }

    [Fact]
    public void ValidateWorkOrderReturnItems_WhenQtyWithinReturnable_Succeeds()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        var rawItemId = Guid.NewGuid();
        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rawItemId, "Steel", 5m)
        {
            TransferredQuantity = 5m,
            ConsumedQuantity = 2m // returnable = 3m
        };
        wo.RequiredItems.Add(woItem);

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };
        se.AddItem(rawItemId, 3m, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: Guid.NewGuid(), valuationRate: 10m);

        Should.NotThrow(() => _manager.ValidateWorkOrderReturnItems(se, wo));
    }

    [Fact]
    public void ValidateWorkOrderReturnItems_WhenQtyExceedsReturnable_ThrowsValidationFailed()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m);
        var rawItemId = Guid.NewGuid();
        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, rawItemId, "Steel", 5m)
        {
            TransferredQuantity = 5m,
            ConsumedQuantity = 2m // returnable = 3m
        };
        wo.RequiredItems.Add(woItem);

        var se = new StockEntry(Guid.NewGuid(), wo.CompanyId, StockEntryType.MaterialTransfer, DateTime.UtcNow)
        {
            WorkOrderId = wo.Id,
            IsReturn = true
        };
        se.AddItem(rawItemId, 4m, sourceWarehouseId: Guid.NewGuid(), targetWarehouseId: Guid.NewGuid(), valuationRate: 10m);

        var ex = Should.Throw<BusinessException>(() => _manager.ValidateWorkOrderReturnItems(se, wo));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("exceeds unconsumed transferred quantity");
    }
}
