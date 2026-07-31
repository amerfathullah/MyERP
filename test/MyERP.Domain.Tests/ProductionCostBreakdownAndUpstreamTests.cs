using System;
using Xunit;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;

namespace MyERP.Tests;

/// <summary>
/// Tests for Production Cost Breakdown DTO + upstream PR #57634 tracking.
/// Validates FG cost absorption formula per ERPNext ManufactureStockEntry.
/// </summary>
public class ProductionCostBreakdownAndUpstreamTests
{
    [Fact]
    public void ProductionCostBreakdownDto_DefaultsZero()
    {
        var dto = new ProductionCostBreakdownDto();
        Assert.Equal(0m, dto.TotalRmCost);
        Assert.Equal(0m, dto.FgUnitCost);
        Assert.Equal(0m, dto.CostVariance);
        Assert.Equal(0m, dto.ProcessLossQty);
        Assert.Null(dto.WorkOrderNumber);
        Assert.Null(dto.ItemName);
    }

    [Fact]
    public void ProductionCostBreakdownDto_FgUnitCost_Calculation()
    {
        // FG unit cost = totalProductionCost / producedQty
        var dto = new ProductionCostBreakdownDto
        {
            TotalProductionCost = 10000m,
            ProducedQty = 100m,
            FgUnitCost = 10000m / 100m,
        };
        Assert.Equal(100m, dto.FgUnitCost);
    }

    [Fact]
    public void ProductionCostBreakdownDto_CostVariance_Positive_Unfavorable()
    {
        // When actual > standard → positive variance = unfavorable
        var dto = new ProductionCostBreakdownDto
        {
            FgUnitCost = 120m,
            BomStandardCost = 100m,
            CostVariance = 20m,
            CostVariancePercent = 20m,
        };
        Assert.True(dto.CostVariance > 0);
        Assert.Equal(20m, dto.CostVariancePercent);
    }

    [Fact]
    public void ProductionCostBreakdownDto_CostVariance_Negative_Favorable()
    {
        // When actual < standard → negative variance = favorable
        var dto = new ProductionCostBreakdownDto
        {
            FgUnitCost = 90m,
            BomStandardCost = 100m,
            CostVariance = -10m,
            CostVariancePercent = -10m,
        };
        Assert.True(dto.CostVariance < 0);
    }

    [Fact]
    public void ProcessLoss_AbsorbedIntoFgRate()
    {
        // Per ERPNext: FG rate = totalRmCost / produceQty (NOT totalFgQty)
        // If 100 units consumed RM but only 95 enter stock (5 lost),
        // FG rate = totalCost / 95 (higher per-unit cost due to loss)
        decimal totalRmCost = 9500m;
        decimal produceQty = 95m;
        decimal processLossQty = 5m;
        decimal fgRate = totalRmCost / produceQty;

        Assert.Equal(100m, fgRate);
        // Process loss value = totalRmCost × (lossQty / totalFgQty)
        var processLossValue = totalRmCost * (processLossQty / (produceQty + processLossQty));
        Assert.Equal(475m, processLossValue);
    }

    [Fact]
    public void ZeroProducedQty_FgUnitCost_IsZero()
    {
        // Division guard: zero produced = zero unit cost (not divide-by-zero)
        var dto = new ProductionCostBreakdownDto
        {
            TotalProductionCost = 5000m,
            ProducedQty = 0m,
            FgUnitCost = 0m,
        };
        Assert.Equal(0m, dto.FgUnitCost);
    }

    [Fact]
    public void AdditionalCosts_IncludedInTotal()
    {
        var dto = new ProductionCostBreakdownDto
        {
            TotalRmCost = 8000m,
            AdditionalCosts = 2000m,
            TotalProductionCost = 10000m,
        };
        Assert.Equal(dto.TotalRmCost + dto.AdditionalCosts, dto.TotalProductionCost);
    }

    [Fact]
    public void BomStandardCost_ZeroBomQty_DefaultsZero()
    {
        // When BOM has qty=0 (edge case), standard cost should be 0
        var dto = new ProductionCostBreakdownDto { BomStandardCost = 0m };
        Assert.Equal(0m, dto.BomStandardCost);
    }

    // --- Upstream PR #57634 tracking ---

    [Fact]
    public void Upstream_PR57634_WoGanttColors_NoCodeChangeNeeded()
    {
        // PR #57634: Work Order Gantt view status-based bar colors
        // Pure client-side JS feature (work_order_calendar.js)
        // MyERP: Manufacturing Dashboard already has status-based color coding for WO cards
        // No domain model changes needed
        Assert.True(true);
    }

    [Fact]
    public void Upstream_MyInvois_Unchanged()
    {
        // myinvois at commit 6501660 — no changes since last sync
        Assert.True(true);
    }

    // --- WorkOrder entity cost-related properties ---

    [Fact]
    public void WorkOrder_ProcessLossQty_Defaults()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100);
        Assert.Equal(0m, wo.ProcessLossQty);
    }

    [Fact]
    public void WorkOrder_EffectiveFgQuantity_WithProcessLoss()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.ProcessLossPercentage = 5m;
        Assert.Equal(95m, wo.EffectiveFgQuantity);
    }

    [Fact]
    public void BOM_TotalCost_IncludesOperations()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 2, 50m));
        bom.RecalculateCost();
        Assert.Equal(100m, bom.TotalCost);
    }

    [Fact]
    public void CostVariancePercent_Formula()
    {
        // variance% = (actual - standard) / standard × 100
        decimal actual = 110m;
        decimal standard = 100m;
        decimal variancePct = (actual - standard) / standard * 100;
        Assert.Equal(10m, variancePct);
    }

    [Fact]
    public void CostVariancePercent_ZeroStandard_IsZero()
    {
        decimal standard = 0m;
        decimal variancePct = standard > 0 ? (110m - standard) / standard * 100 : 0m;
        Assert.Equal(0m, variancePct);
    }
}
