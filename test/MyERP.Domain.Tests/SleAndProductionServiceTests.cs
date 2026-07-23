using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing.Services;
using Xunit;

namespace MyERP.Tests;

/// <summary>
/// Tests for SLE PostingDateTime tie-breaking fields (gotcha #649),
/// WorkOrderProductionService material consumption calculation (gotcha #453),
/// and WorkOrderItem operation-level material tracking.
/// </summary>
public class SleAndProductionServiceTests
{
    // ─── SLE PostingTime + PostingDateTime ───

    [Fact]
    public void SLE_OldConstructor_SetsPostingTimeFromDate()
    {
        var date = new DateTime(2026, 7, 23, 14, 30, 45);
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), date, 10, 5.5m, 10, 55m);

        Assert.Equal(new TimeSpan(14, 30, 45), sle.PostingTime);
        Assert.Equal(date, sle.PostingDateTime);
    }

    [Fact]
    public void SLE_NewConstructor_SeparatePostingTime()
    {
        var date = new DateTime(2026, 7, 23);
        var time = new TimeSpan(9, 15, 30);
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), date, time, 5, 10m, 5, 50m,
            "StockEntry", Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(time, sle.PostingTime);
        Assert.Equal(new DateTime(2026, 7, 23, 9, 15, 30), sle.PostingDateTime);
    }

    [Fact]
    public void SLE_VoucherDetailNo_Defaults_Null()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.Null(sle.VoucherDetailNo);
    }

    [Fact]
    public void SLE_IsCancelled_Defaults_False()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.False(sle.IsCancelled);
    }

    [Fact]
    public void SLE_IsAdjustmentEntry_Defaults_False()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.False(sle.IsAdjustmentEntry);
    }

    [Fact]
    public void SLE_RecalculateRate_Defaults_False()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.False(sle.RecalculateRate);
    }

    [Fact]
    public void SLE_StockValueDifference_SetInNewConstructor()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, TimeSpan.Zero, 5, 10m, 5, 50m);
        Assert.Equal(50m, sle.StockValueDifference); // qty × rate
    }

    [Fact]
    public void SLE_IncomingOutgoingRate_Defaults_Zero()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.Equal(0m, sle.IncomingRate);
        Assert.Equal(0m, sle.OutgoingRate);
    }

    [Fact]
    public void SLE_HasBatchNo_HasSerialNo_Defaults_False()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow, 1, 10m, 1, 10m);
        Assert.False(sle.HasBatchNo);
        Assert.False(sle.HasSerialNo);
    }

    [Fact]
    public void SLE_PostingDateTimeOrdering_MidnightFirst()
    {
        var date = new DateTime(2026, 7, 23);
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), date, new TimeSpan(0, 0, 0), 5, 10m, 5, 50m);
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), date, new TimeSpan(23, 59, 59), 5, 10m, 10, 100m);

        Assert.True(sle1.PostingDateTime < sle2.PostingDateTime);
    }

    // ─── WorkOrderProductionService — Material Consumption ───

    private WorkOrder CreateTestWorkOrder(decimal qty = 100)
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), qty);
        wo.Submit();
        wo.Start();
        return wo;
    }

    [Fact]
    public void Production_BOMBased_ProportionalConsumption()
    {
        var wo = CreateTestWorkOrder(100);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Steel", 200));
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Bolts", 400));

        var service = new WorkOrderProductionService(null!);
        var consumption = service.CalculateRawMaterialConsumption(wo, 25, "BOM");

        // 25/100 = 25%, so Steel=50, Bolts=100
        Assert.Equal(2, consumption.Count);
        Assert.Equal(50m, consumption.First(c => c.ItemName == "Steel").Quantity);
        Assert.Equal(100m, consumption.First(c => c.ItemName == "Bolts").Quantity);
    }

    [Fact]
    public void Production_MaterialTransferred_CapsAtTransferred()
    {
        var wo = CreateTestWorkOrder(100);
        var steelItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Steel", 200);
        steelItem.TransferredQuantity = 80; // Only 80 transferred out of 200 required
        steelItem.ConsumedQuantity = 20;    // Already consumed 20
        wo.RequiredItems.Add(steelItem);

        var service = new WorkOrderProductionService(null!);
        var consumption = service.CalculateRawMaterialConsumption(wo, 50, "Material Transferred");

        // Proportional = 200 × (50/100) = 100
        // Available = 80 - 20 = 60
        // MIN(60, 100) = 60
        Assert.Single(consumption);
        Assert.Equal(60m, consumption[0].Quantity);
    }

    [Fact]
    public void Production_ZeroFgQty_ReturnsEmpty()
    {
        var wo = CreateTestWorkOrder(100);
        wo.RequiredItems.Add(new WorkOrderItem(Guid.NewGuid(), wo.Id, Guid.NewGuid(), "Steel", 200));

        var service = new WorkOrderProductionService(null!);
        var consumption = service.CalculateRawMaterialConsumption(wo, 0, "BOM");

        Assert.Empty(consumption);
    }

    [Fact]
    public void Production_ValidateParams_OverproductionBlocked()
    {
        var wo = CreateTestWorkOrder(100);
        wo.RecordProduction(50, 10); // Produced 50 (within 10% = 110 max)

        var service = new WorkOrderProductionService(null!);
        var ex = Assert.Throws<Volo.Abp.BusinessException>(() =>
            service.ValidateAndGetProductionParams(wo, 65, 0, 10)); // 50 + 65 = 115 > 110 (10% of 100)

        Assert.Equal(MyERPDomainErrorCodes.WorkOrderOverproduction, ex.Code);
    }

    [Fact]
    public void Production_ValidateParams_WithinAllowance_Succeeds()
    {
        var wo = CreateTestWorkOrder(100);
        wo.RecordProduction(50, 10); // Produced 50 (within 10% allowance)

        var service = new WorkOrderProductionService(null!);
        var result = service.ValidateAndGetProductionParams(wo, 40, 5, 10); // 50+40+5=95 ≤ 110 (10%)

        Assert.Equal(40m, result.ProduceQty);
        Assert.Equal(5m, result.ProcessLossQty);
        Assert.Equal(45m, result.TotalFgQty);
    }

    [Fact]
    public void Production_ValidateParams_ReturnsWarehouses()
    {
        var wipId = Guid.NewGuid();
        var fgId = Guid.NewGuid();
        var wo = CreateTestWorkOrder(100);
        wo.WipWarehouseId = wipId;
        wo.FgWarehouseId = fgId;

        var service = new WorkOrderProductionService(null!);
        var result = service.ValidateAndGetProductionParams(wo, 10, 0, 0);

        Assert.Equal(wipId, result.SourceWarehouseId);
        Assert.Equal(fgId, result.TargetWarehouseId);
    }

    [Fact]
    public void Production_ValidateParams_FallsBackToSourceWarehouse()
    {
        var sourceId = Guid.NewGuid();
        var wo = CreateTestWorkOrder(100);
        wo.SourceWarehouseId = sourceId;
        // WipWarehouseId is null — should fallback

        var service = new WorkOrderProductionService(null!);
        var result = service.ValidateAndGetProductionParams(wo, 10, 0, 0);

        Assert.Equal(sourceId, result.SourceWarehouseId);
    }

    // ─── WorkOrderItem Enhanced Fields ───

    [Fact]
    public void WorkOrderItem_BomOperationId_DefaultsNull()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 100);
        Assert.Null(item.BomOperationId);
    }

    [Fact]
    public void WorkOrderItem_StockQty_CalculatedFromConversionFactor()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 10);
        item.ConversionFactor = 12m; // Dozen → Unit
        Assert.Equal(120m, item.StockQty);
    }

    [Fact]
    public void WorkOrderItem_PendingTransferQty_Calculated()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 200);
        item.TransferredQuantity = 120;
        Assert.Equal(80m, item.PendingTransferQty);
    }

    [Fact]
    public void WorkOrderItem_PendingTransferQty_NeverNegative()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 200);
        item.TransferredQuantity = 300; // Over-transferred
        Assert.Equal(0m, item.PendingTransferQty);
    }

    [Fact]
    public void WorkOrderItem_AvailableForConsumption_Calculated()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 200);
        item.TransferredQuantity = 150;
        item.ConsumedQuantity = 80;
        Assert.Equal(70m, item.AvailableForConsumption);
    }

    [Fact]
    public void WorkOrderItem_AvailableForConsumption_NeverNegative()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 200);
        item.TransferredQuantity = 50;
        item.ConsumedQuantity = 80; // Over-consumed (shouldn't happen but guard)
        Assert.Equal(0m, item.AvailableForConsumption);
    }

    [Fact]
    public void WorkOrderItem_AlternativeItem_Defaults()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 100);
        Assert.False(item.IsAlternativeItem);
        Assert.Null(item.OriginalItemId);
    }

    [Fact]
    public void WorkOrderItem_DefaultConversionFactor_IsOne()
    {
        var item = new WorkOrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Steel", 100);
        Assert.Equal(1m, item.ConversionFactor);
        Assert.Equal(100m, item.StockQty); // Same as RequiredQuantity
    }

    // ─── MaterialConsumptionItem Record ───

    [Fact]
    public void MaterialConsumptionItem_RecordProperties()
    {
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var item = new MaterialConsumptionItem(itemId, "Steel Bar", 50m, whId);

        Assert.Equal(itemId, item.ItemId);
        Assert.Equal("Steel Bar", item.ItemName);
        Assert.Equal(50m, item.Quantity);
        Assert.Equal(whId, item.SourceWarehouseId);
    }

    // ─── ProductionParameters Record ───

    [Fact]
    public void ProductionParameters_RecordProperties()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var p = new ProductionParameters(10m, 2m, 12m, sourceId, targetId);

        Assert.Equal(10m, p.ProduceQty);
        Assert.Equal(2m, p.ProcessLossQty);
        Assert.Equal(12m, p.TotalFgQty);
        Assert.Equal(sourceId, p.SourceWarehouseId);
        Assert.Equal(targetId, p.TargetWarehouseId);
    }
}
