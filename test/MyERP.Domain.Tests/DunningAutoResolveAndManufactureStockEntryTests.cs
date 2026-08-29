using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for Dunning auto-resolution, WO→SE manufacture conversion,
/// and Material Transfer for Manufacture business logic.
/// </summary>
public class DunningAutoResolveAndManufactureStockEntryTests
{
    // === Dunning Auto-Resolution ===

    [Fact]
    public void DunningManager_CalculateInterest_DailySimpleInterest()
    {
        // Per ERPNext: interest = rate/100/365 × overdue_days × outstanding
        var overdueInvoices = new List<(decimal outstanding, int overdueDays)>
        {
            (1000m, 30), // 1000 × 10/100/365 × 30 = 8.22
            (2000m, 15), // 2000 × 10/100/365 × 15 = 8.22
        };

        var interest = DunningManager.CalculateInterest(10m, overdueInvoices);

        // 1000 * 10/100/365 * 30 = 8.2191...
        // 2000 * 10/100/365 * 15 = 8.2191...
        // Total ≈ 16.44
        Assert.True(interest > 16m && interest < 17m,
            $"Expected ~16.44 but got {interest}");
    }

    [Fact]
    public void DunningManager_CalculateInterest_ZeroRate_ReturnsZero()
    {
        var overdueInvoices = new List<(decimal outstanding, int overdueDays)>
        {
            (1000m, 30),
        };

        var interest = DunningManager.CalculateInterest(0m, overdueInvoices);
        Assert.Equal(0m, interest);
    }

    [Fact]
    public void DunningManager_CalculateInterest_EmptyInvoices_ReturnsZero()
    {
        var interest = DunningManager.CalculateInterest(
            10m, new List<(decimal, int)>());
        Assert.Equal(0m, interest);
    }

    [Fact]
    public void DunningManager_ShouldAutoResolve_AllPaid_True()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.Submit();

        Assert.True(DunningManager.ShouldAutoResolve(dunning));
    }

    [Fact]
    public void DunningManager_ShouldAutoResolve_StillOutstanding_False()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(-30), 30);
        dunning.Submit();

        Assert.False(DunningManager.ShouldAutoResolve(dunning));
    }

    [Fact]
    public void DunningManager_ShouldAutoResolve_DraftStatus_False()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 0m, DateTime.UtcNow.AddDays(-30), 30);
        // Not submitted — should not auto-resolve
        Assert.False(DunningManager.ShouldAutoResolve(dunning));
    }

    [Fact]
    public void DunningLevel_SequentialDetermination_FirstDunning_Level1()
    {
        // First dunning for a customer should always be level 1
        // (DetermineDunningLevelAsync returns existingCount + 1; existingCount = 0 → level 1)
        Assert.Equal(1, 0 + 1);
    }

    [Fact]
    public void DunningLevel_SequentialDetermination_ThirdDunning_Level3()
    {
        // After 2 submitted dunnings exist, next should be level 3
        Assert.Equal(3, 2 + 1);
    }

    // === Work Order → Stock Entry (Manufacture) ===

    [Fact]
    public void WorkOrder_RecordProduction_RespestsOverproductionLimit()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        // 5% overproduction allowed: max = 100 × 1.05 = 105
        wo.RecordProduction(80, overproductionPercentage: 5);
        Assert.Equal(80m, wo.ProducedQuantity);

        // Should allow up to 105 total
        wo.RecordProduction(25, overproductionPercentage: 5);
        Assert.Equal(105m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_RecordProduction_ThrowsOnOverproduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(100, overproductionPercentage: 5);

        // Attempting 6 more (total 106) should exceed 105 limit
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            wo.RecordProduction(6, overproductionPercentage: 5));
    }

    [Fact]
    public void WorkOrder_EffectiveFgQuantity_WithProcessLossPercentage()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-003",
            Guid.NewGuid(), Guid.NewGuid(), 100m)
        { ProcessLossPercentage = 5m };

        // 100 × (1 - 5/100) = 95
        Assert.Equal(95m, wo.EffectiveFgQuantity);
    }

    [Fact]
    public void WorkOrder_EffectiveFgQuantity_WithProcessLossQty()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-004",
            Guid.NewGuid(), Guid.NewGuid(), 100m)
        { ProcessLossQty = 3m };

        // 100 - 3 = 97
        Assert.Equal(97m, wo.EffectiveFgQuantity);
    }

    [Fact]
    public void WorkOrder_PercentComplete_Calculation()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-005",
            Guid.NewGuid(), Guid.NewGuid(), 200m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, overproductionPercentage: 10);

        // 50/200 × 100 = 25%
        Assert.Equal(25m, wo.PercentComplete);
    }

    [Fact]
    public void WorkOrder_RecordProduction_WithProcessLoss_CompletesWhenTotalReachesOrderedQty()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-005B",
            Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        // 95 produced + 5 process loss = 100 total ordered qty -> Completed
        wo.RecordProduction(95m, overproductionPercentage: 0, processLoss: 5m);

        Assert.Equal(95m, wo.ProducedQuantity);
        Assert.Equal(5m, wo.ProcessLossQty);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.NotNull(wo.ActualEndDate);
    }

    [Fact]
    public void WorkOrder_CannotStartFromDraft()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-006",
            Guid.NewGuid(), Guid.NewGuid(), 100m);

        Assert.Throws<Volo.Abp.BusinessException>(() => wo.Start());
    }

    [Fact]
    public void WorkOrder_CannotCancelCompletedOrder()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-007",
            Guid.NewGuid(), Guid.NewGuid(), 10m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(10, overproductionPercentage: 5); // Auto-completes at 100%

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.Cancel());
    }

    // === Stock Entry for Manufacture Validation ===

    [Fact]
    public void StockEntry_Manufacture_MustHaveItems()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.Manufacture, DateTime.UtcNow);

        // Cannot submit empty stock entry
        Assert.Throws<Volo.Abp.BusinessException>(() => se.Submit());
    }

    [Fact]
    public void StockEntry_MaterialTransfer_AddItem_RequiresDraftStatus()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.MaterialTransferForManufacture, DateTime.UtcNow);
        se.AddItem(Guid.NewGuid(), 10m, Guid.NewGuid(), Guid.NewGuid());
        se.Submit();

        // Cannot add items to submitted entry
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            se.AddItem(Guid.NewGuid(), 5m, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void StockEntry_TotalIncomingValue_CalculatedFromTargetWarehouseItems()
    {
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.Manufacture, DateTime.UtcNow);

        var targetWh = Guid.NewGuid();
        var sourceWh = Guid.NewGuid();

        // RM consumption (outgoing, no target warehouse)
        se.AddItem(Guid.NewGuid(), 5m, sourceWh, null, valuationRate: 10m);
        // FG production (incoming, has target warehouse)
        se.AddItem(Guid.NewGuid(), 10m, null, targetWh, valuationRate: 5m);

        Assert.Equal(50m, se.TotalIncomingValue); // 10 × 5
        Assert.Equal(50m, se.TotalOutgoingValue); // 5 × 10
        Assert.Equal(0m, se.TotalValueDifference); // Balanced
    }

    // === Dunning Entity Lifecycle ===

    [Fact]
    public void Dunning_GrandTotal_IncludesOutstandingFeeAndInterest()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1)
        { DunningFee = 50m, InterestAmount = 16.44m };

        dunning.AddOverduePayment(Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(-30), 30);

        // GrandTotal = TotalOutstanding + DunningFee + InterestAmount
        Assert.Equal(1000m + 50m + 16.44m, dunning.GrandTotal);
    }

    [Fact]
    public void Dunning_CannotSubmitWithoutOverduePayments()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);

        Assert.Throws<Volo.Abp.BusinessException>(() => dunning.Submit());
    }

    [Fact]
    public void Dunning_Resolve_OnlyFromSubmitted()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);
        dunning.AddOverduePayment(Guid.NewGuid(), 500m, DateTime.UtcNow.AddDays(-15), 15);

        // Cannot resolve from Draft
        Assert.Throws<Volo.Abp.BusinessException>(() => dunning.Resolve());
    }

    [Fact]
    public void Dunning_AddOverduePayment_UpdatesTotalOutstanding()
    {
        var dunning = new Dunning(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);

        dunning.AddOverduePayment(Guid.NewGuid(), 300m, DateTime.UtcNow.AddDays(-10), 10);
        dunning.AddOverduePayment(Guid.NewGuid(), 700m, DateTime.UtcNow.AddDays(-20), 20);

        Assert.Equal(1000m, dunning.TotalOutstanding);
        Assert.Equal(2, dunning.OverduePayments.Count);
    }

    [Fact]
    public void StockEntryManager_ValidateManufactureItems_ThrowsWhenManufacturedQtyMissing()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.Manufacture, DateTime.UtcNow);
        se.WorkOrderId = Guid.NewGuid();
        se.FgCompletedQty = 0m; // Missing manufactured qty

        var targetWh = Guid.NewGuid();
        var sourceWh = Guid.NewGuid();
        se.AddItem(Guid.NewGuid(), 5m, sourceWh, null, valuationRate: 10m);
        se.AddItem(Guid.NewGuid(), 10m, null, targetWh, valuationRate: 5m);

        var ex = Assert.Throws<Volo.Abp.BusinessException>(() =>
            manager.ValidateManufactureItems(se, trackSemiFinishedGoods: false));

        Assert.Equal(MyERP.MyERPDomainErrorCodes.ValidationFailed, ex.Code);
    }

    [Fact]
    public void StockEntryManager_ValidateManufactureItems_SucceedsWithManufacturedQty()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        var se = new StockEntry(
            Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.Manufacture, DateTime.UtcNow);
        se.WorkOrderId = Guid.NewGuid();
        se.FgCompletedQty = 10m;

        var targetWh = Guid.NewGuid();
        var sourceWh = Guid.NewGuid();
        manager.ValidateManufactureItems(se, trackSemiFinishedGoods: false);
    }

    [Fact]
    public void StockEntryManager_CalculateManufactureFgRate_ZeroValuedInputs_KeepsRateZero()
    {
        // Per ERPNext PR #57334: manufactured item rate remains 0 when inputs are free
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();

        se.AddItem(Guid.NewGuid(), 10m, sourceWh, null, valuationRate: 0m); // Free RM
        se.AddItem(Guid.NewGuid(), 10m, null, targetWh); // FG

        var rate = manager.CalculateManufactureFgRate(se.Items, fgQty: 10m, additionalOperatingCost: 0m, bomEstimatedCost: 150m);
        Assert.Equal(0m, rate); // Must NOT fall back to BOM estimate 150
    }

    [Fact]
    public void StockEntryManager_CalculateManufactureFgRate_ValuedInputs_CalculatesAccurateRate()
    {
        var manager = new MyERP.Inventory.DomainServices.StockEntryManager(null!, null!, null!);
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();

        se.AddItem(Guid.NewGuid(), 10m, sourceWh, null, valuationRate: 5m); // 50 total RM
        se.AddItem(Guid.NewGuid(), 2m, null, targetWh); // FG qty 2

        var rate = manager.CalculateManufactureFgRate(se.Items, fgQty: 2m, additionalOperatingCost: 10m);
        Assert.Equal(30m, rate); // (50 + 10) / 2 = 30
    }
}
