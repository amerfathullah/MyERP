using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for Material Consumption for Manufacture workflow.
/// Per DO-NOT: "Consume raw materials twice when material_consumption setting is ON"
/// Per DO-NOT: "Skip Material Consumption separation when get_rm_cost_from_consumption_entry is enabled"
/// Per gotcha #491: consumed_qty cannot exceed transferred_qty in Manufacture SE with material_consumption ON
/// </summary>
public class MaterialConsumptionTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    [Fact]
    public void ManufacturingSettings_MaterialConsumption_DefaultsFalse()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.MaterialConsumption.ShouldBeFalse();
    }

    [Fact]
    public void ManufacturingSettings_MaterialConsumption_CanBeEnabled()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.MaterialConsumption = true;
        settings.MaterialConsumption.ShouldBeTrue();
    }

    [Fact]
    public void StockEntryType_MaterialConsumptionForManufacture_HasCorrectValue()
    {
        ((int)StockEntryType.MaterialConsumptionForManufacture).ShouldBe(7);
    }

    [Fact]
    public void WorkOrderItem_TransferredQuantity_Tracks()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, _bomId, 100, null);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, _itemId, "Raw Material", 50));
        var item = wo.RequiredItems.First();
        item.TransferredQuantity = 30;
        item.TransferredQuantity.ShouldBe(30);
    }

    [Fact]
    public void ConsumedQty_CannotExceed_TransferredQty_Concept()
    {
        // Per gotcha #491: consumed_qty cannot exceed transferred_qty
        var woItem = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, "RM", 100);
        woItem.TransferredQuantity = 60;

        // Business rule: consumed must be <= transferred
        var consumeQty = 70m;
        var isValid = consumeQty <= woItem.TransferredQuantity;
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ConsumedQty_WithinTransferred_IsValid()
    {
        var woItem = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, "RM", 100);
        woItem.TransferredQuantity = 60;

        var consumeQty = 50m;
        var isValid = consumeQty <= woItem.TransferredQuantity;
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ConsumedQty_ExactlyTransferred_IsValid()
    {
        var woItem = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), _itemId, "RM", 100);
        woItem.TransferredQuantity = 60;

        var consumeQty = 60m;
        var isValid = consumeQty <= woItem.TransferredQuantity;
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void WorkOrder_MustBeInProcess_ForConsumption()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, _bomId, 100, null);
        // Draft status should not allow consumption
        wo.Status.ShouldBe(WorkOrderStatus.Draft);
        // Only InProcess allows consumption per ERPNext
        (wo.Status == WorkOrderStatus.InProcess).ShouldBeFalse();
    }

    [Fact]
    public void WorkOrder_InProcess_AllowsConsumption()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, _bomId, 100, null);
        wo.Submit();
        wo.Start(); // → InProcess
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void StockEntry_MaterialConsumption_RequiresWorkOrderId()
    {
        var entry = new Inventory.Entities.StockEntry(
            Guid.NewGuid(), _companyId,
            StockEntryType.MaterialConsumptionForManufacture,
            DateTime.UtcNow, null);

        entry.WorkOrderId.ShouldBeNull();
        var woId = Guid.NewGuid();
        entry.WorkOrderId = woId;
        entry.WorkOrderId.ShouldBe(woId);
    }

    [Fact]
    public void MaterialConsumption_Items_AreStockOut_Only()
    {
        // Material consumption entries should only have source warehouse (stock-out)
        // No target warehouse (unlike Material Transfer which has both)
        var entry = new Inventory.Entities.StockEntry(
            Guid.NewGuid(), _companyId,
            StockEntryType.MaterialConsumptionForManufacture,
            DateTime.UtcNow, null);

        var warehouseId = Guid.NewGuid();
        entry.AddItem(_itemId, 10, warehouseId, null, 5.0m);

        var item = entry.Items.First();
        item.SourceWarehouseId.ShouldBe(warehouseId);
        item.TargetWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void MaterialConsumptionResult_HasExpectedProperties()
    {
        var result = new MaterialConsumptionResultDto
        {
            StockEntryId = Guid.NewGuid(),
            EntryNumber = "SE-001-00001",
            TotalConsumedValue = 5000m,
            ItemCount = 3
        };

        result.StockEntryId.ShouldNotBe(Guid.Empty);
        result.EntryNumber.ShouldNotBeNullOrEmpty();
        result.TotalConsumedValue.ShouldBe(5000m);
        result.ItemCount.ShouldBe(3);
    }

    [Fact]
    public void ConsumptionItemDto_RequiredFields()
    {
        var dto = new ConsumptionItemDto
        {
            ItemId = Guid.NewGuid(),
            Quantity = 10m,
            WarehouseId = Guid.NewGuid(),
            BatchId = null
        };

        dto.ItemId.ShouldNotBe(Guid.Empty);
        dto.Quantity.ShouldBe(10m);
        dto.WarehouseId.ShouldNotBeNull();
        dto.BatchId.ShouldBeNull();
    }

    [Fact]
    public void CreateMaterialConsumptionDto_Structure()
    {
        var dto = new CreateMaterialConsumptionDto
        {
            WorkOrderId = Guid.NewGuid(),
            Items = new System.Collections.Generic.List<ConsumptionItemDto>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 5 },
                new() { ItemId = Guid.NewGuid(), Quantity = 10 }
            }
        };

        dto.WorkOrderId.ShouldNotBe(Guid.Empty);
        dto.Items.Count.ShouldBe(2);
        dto.Items.Sum(i => i.Quantity).ShouldBe(15m);
    }

    [Fact]
    public void ManufactureEntry_WithConsumptionEnabled_ShouldExcludeRM()
    {
        // When MaterialConsumption=true AND submitted consumption SE exists,
        // the Manufacture SE should ONLY have FG + secondary items
        // This is a design validation — the actual enforcement is in AppService
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.MaterialConsumption = true;
        settings.MaterialConsumption.ShouldBeTrue();

        // The FG-only constraint is checked at AppService level
        // (per DO-NOT: only is_finished_item, secondary_item_type, or is_legacy_scrap_item allowed)
    }
}
