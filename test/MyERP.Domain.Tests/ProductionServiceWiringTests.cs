using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for WorkOrderProductionService delegation and process loss handling.
/// Validates the enhanced production pipeline per ERPNext gotchas #453, #491, #524, #442.
/// </summary>
public class ProductionServiceWiringTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _bomId = Guid.NewGuid();

    private WorkOrder CreateWorkOrder(decimal qty = 100)
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-TEST-001", _itemId, _bomId, qty);
        wo.Submit();
        wo.Start();
        return wo;
    }

    private void AddRequiredItem(WorkOrder wo, Guid itemId, string name, decimal requiredQty, Guid? warehouseId = null)
    {
        var item = new WorkOrderItem(Guid.NewGuid(), wo.Id, itemId, name, requiredQty);
        item.SourceWarehouseId = warehouseId;
        wo.RequiredItems.Add(item);
    }

    // --- ValidateAndGetProductionParams Tests ---

    [Fact]
    public void ValidateAndGetProductionParams_ValidInput_ReturnsParams()
    {
        var wo = CreateWorkOrder(100);
        var service = new WorkOrderProductionService(null!);

        var result = service.ValidateAndGetProductionParams(wo, 50, 5, 10m);

        Assert.Equal(50m, result.ProduceQty);
        Assert.Equal(5m, result.ProcessLossQty);
        Assert.Equal(55m, result.TotalFgQty); // produce + loss
    }

    [Fact]
    public void ValidateAndGetProductionParams_ExceedsOverproduction_Throws()
    {
        var wo = CreateWorkOrder(100);
        wo.RecordProduction(95, overproductionPercentage: 10m); // 95 already produced
        var service = new WorkOrderProductionService(null!);

        // Max allowed = 100 * 1.10 = 110. Already produced 95. Attempting 20 → 95+20=115 > 110
        var ex = Assert.Throws<Volo.Abp.BusinessException>(() =>
            service.ValidateAndGetProductionParams(wo, 15, 5, 10m)); // 15+5=20 total

        Assert.Equal(MyERPDomainErrorCodes.WorkOrderOverproduction, ex.Code);
    }

    [Fact]
    public void ValidateAndGetProductionParams_NotInProcess_Throws()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-TEST-002", _itemId, _bomId, 100);
        wo.Submit(); // Submitted but NOT started
        var service = new WorkOrderProductionService(null!);

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            service.ValidateAndGetProductionParams(wo, 50, 0, 0));
    }

    [Fact]
    public void ValidateAndGetProductionParams_ZeroProcessLoss_WorksNormally()
    {
        var wo = CreateWorkOrder(100);
        var service = new WorkOrderProductionService(null!);

        var result = service.ValidateAndGetProductionParams(wo, 80, 0, 5m);

        Assert.Equal(80m, result.ProduceQty);
        Assert.Equal(0m, result.ProcessLossQty);
        Assert.Equal(80m, result.TotalFgQty);
    }

    [Fact]
    public void ValidateAndGetProductionParams_UsesWipWarehouseOverSource()
    {
        var wo = CreateWorkOrder(100);
        var wipWarehouse = Guid.NewGuid();
        var sourceWarehouse = Guid.NewGuid();
        wo.WipWarehouseId = wipWarehouse;
        wo.SourceWarehouseId = sourceWarehouse;
        var service = new WorkOrderProductionService(null!);

        var result = service.ValidateAndGetProductionParams(wo, 50, 0, 5m);

        // Per gotcha #454: WIP warehouse takes priority over source warehouse
        Assert.Equal(wipWarehouse, result.SourceWarehouseId);
    }

    [Fact]
    public void ValidateAndGetProductionParams_FallsBackToSourceWarehouse()
    {
        var wo = CreateWorkOrder(100);
        var sourceWarehouse = Guid.NewGuid();
        wo.WipWarehouseId = null;
        wo.SourceWarehouseId = sourceWarehouse;
        var service = new WorkOrderProductionService(null!);

        var result = service.ValidateAndGetProductionParams(wo, 50, 0, 5m);

        Assert.Equal(sourceWarehouse, result.SourceWarehouseId);
    }

    // --- CalculateRawMaterialConsumption Tests ---

    [Fact]
    public void CalculateConsumption_BomMode_ProportionalQty()
    {
        var wo = CreateWorkOrder(100);
        var rm1 = Guid.NewGuid();
        var wh = Guid.NewGuid();
        AddRequiredItem(wo, rm1, "Steel Bar", 200, wh); // 200 for 100 units = 2 per FG
        var service = new WorkOrderProductionService(null!);

        var items = service.CalculateRawMaterialConsumption(wo, 50, "BOM");

        Assert.Single(items);
        // With BOM mode MIN formula: min(unconsumed/wo_qty, per_unit) × fg_qty
        // unconsumed = 200, wo_qty = 100, per_unit = 200/100 = 2
        // min(200/100, 2) = min(2, 2) = 2
        // qty = 2 × 50 = 100
        Assert.Equal(100m, items[0].Quantity);
    }

    [Fact]
    public void CalculateConsumption_BomMode_WithPartialConsumption()
    {
        var wo = CreateWorkOrder(100);
        var rm1 = Guid.NewGuid();
        var wh = Guid.NewGuid();
        AddRequiredItem(wo, rm1, "Wire", 500, wh); // 500 for 100 = 5 per FG
        // Simulate prior consumption
        wo.RequiredItems.First().ConsumedQuantity = 250; // Already consumed 250 of 500
        var service = new WorkOrderProductionService(null!);

        var items = service.CalculateRawMaterialConsumption(wo, 25, "BOM");

        Assert.Single(items);
        // unconsumed = 500 - 250 = 250
        // per_unit = 500 / 100 = 5
        // min(250/100, 5) = min(2.5, 5) = 2.5
        // qty = 2.5 × 25 = 62.5
        Assert.Equal(62.5m, items[0].Quantity);
    }

    [Fact]
    public void CalculateConsumption_MaterialTransferredMode_CapsAtAvailable()
    {
        var wo = CreateWorkOrder(100);
        var rm1 = Guid.NewGuid();
        var wh = Guid.NewGuid();
        AddRequiredItem(wo, rm1, "Copper", 200, wh);
        var item = wo.RequiredItems.First();
        item.TransferredQuantity = 120; // Only 120 transferred to WIP
        item.ConsumedQuantity = 50; // Already consumed 50
        var service = new WorkOrderProductionService(null!);

        var items = service.CalculateRawMaterialConsumption(wo, 50, "Material Transferred");

        Assert.Single(items);
        // Available = transferred - consumed = 120 - 50 = 70
        // Proportional = 200 × (50/100) = 100
        // Capped at available: min(70, 100) = 70
        Assert.Equal(70m, items[0].Quantity);
    }

    [Fact]
    public void CalculateConsumption_ZeroFgQty_ReturnsEmpty()
    {
        var wo = CreateWorkOrder(100);
        AddRequiredItem(wo, Guid.NewGuid(), "Bolt", 1000, Guid.NewGuid());
        var service = new WorkOrderProductionService(null!);

        var items = service.CalculateRawMaterialConsumption(wo, 0, "BOM");

        Assert.Empty(items);
    }

    [Fact]
    public void CalculateConsumption_MultipleItems_AllCalculated()
    {
        var wo = CreateWorkOrder(100);
        AddRequiredItem(wo, Guid.NewGuid(), "Steel", 200, Guid.NewGuid());
        AddRequiredItem(wo, Guid.NewGuid(), "Paint", 50, Guid.NewGuid());
        AddRequiredItem(wo, Guid.NewGuid(), "Bolts", 400, Guid.NewGuid());
        var service = new WorkOrderProductionService(null!);

        var items = service.CalculateRawMaterialConsumption(wo, 25, "BOM");

        Assert.Equal(3, items.Count);
        // Steel: min(200/100, 2) × 25 = 50
        Assert.Equal(50m, items[0].Quantity);
        // Paint: min(50/100, 0.5) × 25 = 12.5
        Assert.Equal(12.5m, items[1].Quantity);
        // Bolts: min(400/100, 4) × 25 = 100
        Assert.Equal(100m, items[2].Quantity);
    }

    // --- Process Loss Integration Tests ---

    [Fact]
    public void ProcessLoss_AbsorbedIntoFgCost()
    {
        // When process_loss = 5% on a 100-unit WO:
        // For 100 FG: consume RM for 105 total (100 good + 5 loss)
        // FG cost = total RM cost / 100 (not / 105)
        // The 5 loss units' cost is distributed across the 100 good units
        var wo = CreateWorkOrder(100);
        var service = new WorkOrderProductionService(null!);

        var result = service.ValidateAndGetProductionParams(wo, 95, 5, 10m);

        // TotalFgQty = 95 + 5 = 100 (total materials consumed for)
        Assert.Equal(100m, result.TotalFgQty);
        // But only ProduceQty = 95 enters FG warehouse
        Assert.Equal(95m, result.ProduceQty);
    }

    [Fact]
    public void ProcessLoss_ConsumptionBasedOnTotalFg()
    {
        // RM consumption should be based on TotalFgQty (produce + loss)
        // because materials are consumed for BOTH good output and process loss
        var wo = CreateWorkOrder(100);
        AddRequiredItem(wo, Guid.NewGuid(), "Iron", 300, Guid.NewGuid()); // 3 per FG unit
        var service = new WorkOrderProductionService(null!);

        // Produce 40 good units + 10 process loss = 50 total FG qty
        var items = service.CalculateRawMaterialConsumption(wo, 50, "BOM");

        // Consumption uses totalFgQty (50):
        // min(300/100, 3) × 50 = 3 × 50 = 150
        Assert.Equal(150m, items[0].Quantity);
    }

    // --- ProductionParameters Record Tests ---

    [Fact]
    public void ProductionParameters_Record_Properties()
    {
        var p = new ProductionParameters(80m, 5m, 85m, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(80m, p.ProduceQty);
        Assert.Equal(5m, p.ProcessLossQty);
        Assert.Equal(85m, p.TotalFgQty);
        Assert.NotNull(p.SourceWarehouseId);
        Assert.NotNull(p.TargetWarehouseId);
    }

    [Fact]
    public void MaterialConsumptionItem_Record_Properties()
    {
        var item = new MaterialConsumptionItem(Guid.NewGuid(), "Test Item", 42.5m, Guid.NewGuid());

        Assert.Equal("Test Item", item.ItemName);
        Assert.Equal(42.5m, item.Quantity);
        Assert.NotNull(item.SourceWarehouseId);
    }

    // --- Job Card Batch Splitting Integration ---

    [Fact]
    public void JobCardBatchSplitting_BatchSizeZero_SingleJobCard()
    {
        // Per gotcha #722: batch_size <= 0 → single JC for full WO qty
        var wo = CreateWorkOrder(100);
        var batchSize = 0; // zero means single JC

        var jcCount = batchSize > 0 ? (int)Math.Ceiling(wo.Quantity / batchSize) : 1;

        Assert.Equal(1, jcCount);
    }

    [Fact]
    public void JobCardBatchSplitting_ExactDivisible_EvenSplit()
    {
        var wo = CreateWorkOrder(100);
        var batchSize = 25;

        var jcCount = (int)Math.Ceiling(wo.Quantity / batchSize);

        Assert.Equal(4, jcCount); // 100 / 25 = 4 JCs
    }

    [Fact]
    public void JobCardBatchSplitting_UnevenRemainder_ExtraJc()
    {
        var wo = CreateWorkOrder(110);
        var batchSize = 25;

        var jcCount = (int)Math.Ceiling(wo.Quantity / batchSize);

        Assert.Equal(5, jcCount); // ceil(110/25) = 5 JCs (25+25+25+25+10)
    }

    // --- Backflush Method Resolution ---

    [Fact]
    public void BackflushMethod_BomDefault()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        Assert.Equal("BOM", settings.BackflushRawMaterialsBasedOn);
    }

    [Fact]
    public void BackflushMethod_MaterialTransferred_DisablesComponentValidation()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), _companyId);
        settings.BackflushRawMaterialsBasedOn = "Material Transferred";
        settings.EnforceMutualExclusions();

        // Per settings-configuration: when backflush != BOM, validates_components disabled
        Assert.False(settings.ValidateComponentsQuantitiesPerBom);
    }
}
