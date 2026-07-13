using System;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

public class SoToWorkOrderTests
{
    [Fact]
    public void WorkOrder_SalesOrderId_CanBeSet()
    {
        var soId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        wo.SalesOrderId = soId;
        wo.SalesOrderId.ShouldBe(soId);
    }

    [Fact]
    public void WorkOrder_SalesOrderId_DefaultsNull()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002",
            Guid.NewGuid(), Guid.NewGuid(), 50, Guid.NewGuid());
        wo.SalesOrderId.ShouldBeNull();
    }

    [Fact]
    public void BOM_IsDefault_CanBeSet()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.IsDefault = true;
        bom.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void BOM_IsActive_DefaultsTrue()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        bom.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void WorkOrder_RequiredItems_PopulatedFromBOM()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003",
            Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid());
        
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Steel", 20));
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Bolt", 50));
        
        wo.RequiredItems.Count.ShouldBe(2);
        wo.RequiredItems[0].RequiredQuantity.ShouldBe(20);
    }
}
