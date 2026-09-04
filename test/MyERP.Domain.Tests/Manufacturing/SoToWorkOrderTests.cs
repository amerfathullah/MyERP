using System;
using System.Linq;
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

    [Fact]
    public void WorkOrder_SalesOrderItemId_CanBeSet()
    {
        var soId = Guid.NewGuid();
        var soItemId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-004",
            Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid())
        {
            SalesOrderId = soId,
            SalesOrderItemId = soItemId
        };

        wo.SalesOrderId.ShouldBe(soId);
        wo.SalesOrderItemId.ShouldBe(soItemId);
    }

    [Fact]
    public void WorkOrder_PackedRowDeliveryDate_PropagationFromParentBundle()
    {
        // Per ERPNext PR #58568 (commit db56080285): Work Order created for a product bundle component
        // inherits the planned end date from the parent bundle item row in the Sales Order.
        var soId = Guid.NewGuid();
        var bundleItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();
        var bundleDeliveryDate = DateTime.UtcNow.Date.AddDays(10);

        var so = new MyERP.Sales.Entities.SalesOrder(soId, Guid.NewGuid(), Guid.NewGuid(), "SO-100", DateTime.UtcNow);
        so.AddItem(bundleItemId, "Test Bundle", 1, 500, 0, "Unit", bundleDeliveryDate);

        var matchingBundleItem = so.Items.FirstOrDefault(i => i.ItemId == bundleItemId);
        matchingBundleItem.ShouldNotBeNull();
        matchingBundleItem.DeliveryDate.ShouldBe(bundleDeliveryDate);

        var wo = new WorkOrder(Guid.NewGuid(), so.CompanyId, "WO-006",
            componentItemId, Guid.NewGuid(), 2, Guid.NewGuid())
        {
            SalesOrderId = soId,
            SalesOrderItemId = matchingBundleItem.Id,
            PlannedEndDate = matchingBundleItem.DeliveryDate
        };

        wo.SalesOrderId.ShouldBe(soId);
        wo.SalesOrderItemId.ShouldBe(matchingBundleItem.Id);
        wo.PlannedEndDate.ShouldBe(bundleDeliveryDate);
    }

    [Fact]
    public void Batch_ExpiryDateBoundary_NotExpiredOnExpiryDay()
    {
        // Per ERPNext PR #58736 (commit 00f04fc084): show Expired status only after expiry date has passed.
        // On the day of expiry, the batch remains valid for transactions until that day concludes.
        var batch = new MyERP.Inventory.Entities.Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-EXP-001")
        {
            ExpiryDate = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)
        };

        // Same date: not expired
        batch.IsExpired(new DateTime(2026, 9, 4, 15, 30, 0, DateTimeKind.Utc)).ShouldBeFalse();

        // Day after: expired
        batch.IsExpired(new DateTime(2026, 9, 5, 0, 0, 1, DateTimeKind.Utc)).ShouldBeTrue();

        // Day before: not expired
        batch.IsExpired(new DateTime(2026, 9, 3, 23, 59, 59, DateTimeKind.Utc)).ShouldBeFalse();
    }
}

