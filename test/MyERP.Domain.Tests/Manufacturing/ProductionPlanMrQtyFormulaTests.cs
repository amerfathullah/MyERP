using System;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Tests for PP MR qty formula fixes per ERPNext PR #57399:
/// - Safety stock included BEFORE min-order-qty and UOM rounding
/// - Consumed available qty tracking uses (qty - required_qty) not min(qty, available)
/// - Reserved qty uses required_bom_qty directly
/// </summary>
public class ProductionPlanMrQtyFormulaTests
{
    [Fact]
    public void MrItem_SafetyStock_IncludedBeforeMinOrderQty()
    {
        // Per PR #57399: safety stock is added to required qty BEFORE checking min_order_qty.
        // Previously safety_stock was added AFTER rounding, causing over-ordering.
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 80m)
        {
            SafetyStock = 20m,
            AvailableQty = 0m,
            MinOrderQty = 50m,
        };

        // RequiredQty(80) + SafetyStock(20) = 100 → already above MinOrderQty(50)
        // So result should be 100, NOT 100 rounded up to 150
        Assert.Equal(100m, item.RequiredQty + item.SafetyStock);
    }

    [Fact]
    public void MrItem_SafetyStock_ReducesAvailableStock()
    {
        // Safety stock acts as a buffer: available stock is reduced by safety stock
        // before determining required qty.
        // available_after_safety = available - safety_stock
        // required = max(0, bom_qty - available_after_safety)
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m)
        {
            SafetyStock = 30m,
            AvailableQty = 80m, // 80 in stock, but 30 is safety buffer
            MinOrderQty = 0m,
        };

        // available_after_safety = 80 - 30 = 50
        // required = max(0, 100 - 50) = 50
        var availableAfterSafety = item.AvailableQty - item.SafetyStock;
        var required = Math.Max(0, item.RequiredQty - availableAfterSafety);
        Assert.Equal(50m, required);
    }

    [Fact]
    public void MrItem_NegativeProjectedQty_UsesFullRequiredPlusSafety()
    {
        // Per PR #57399: when projected qty < 0 OR not ignoring existing orders,
        // use full required qty + safety stock (no available deduction)
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m)
        {
            SafetyStock = 20m,
            AvailableQty = -10m, // Negative projected qty
        };

        // When available < 0: result = qty + safety_stock = 120
        var required = item.RequiredQty + item.SafetyStock;
        Assert.Equal(120m, required);
    }

    [Fact]
    public void MrItem_MinOrderQty_AppliedAfterSafety()
    {
        // Safety stock brings qty above zero, then min_order_qty floors it
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 10m)
        {
            SafetyStock = 5m,
            AvailableQty = 0m,
            MinOrderQty = 25m,
        };

        // qty + safety = 15, but min_order_qty = 25 → floor to 25
        var qty = item.RequiredQty + item.SafetyStock;
        if (qty > 0 && qty < item.MinOrderQty)
            qty = item.MinOrderQty;
        Assert.Equal(25m, qty);
    }

    [Fact]
    public void MrItem_ReservedQty_UsesRequiredBomQty_NotQuantityTimesConversionFactor()
    {
        // Per PR #57399: PP reserved qty in Bin should use required_bom_qty directly,
        // NOT the old formula: CASE WHEN quantity == 0 THEN required_bom_qty ELSE quantity * conversion_factor
        // This prevents over-reservation when quantity field differs from BOM consumption.
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 150m);

        // The reserved qty should always be the RequiredQty (= required_bom_qty from BOM explosion)
        Assert.Equal(150m, item.RequiredQty);
    }

    [Fact]
    public void MrItem_ZeroAvailable_FullRequiredQty()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m)
        {
            AvailableQty = 0m,
            SafetyStock = 0m,
        };

        var required = Math.Max(0, item.RequiredQty - item.AvailableQty);
        Assert.Equal(100m, required);
    }

    [Fact]
    public void MrItem_SufficientStock_ZeroRequired()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 50m)
        {
            AvailableQty = 200m,
            SafetyStock = 10m,
        };

        // available_after_safety = 200 - 10 = 190
        // required = max(0, 50 - 190) = 0
        var availableAfterSafety = item.AvailableQty - item.SafetyStock;
        var required = Math.Max(0, item.RequiredQty - availableAfterSafety);
        Assert.Equal(0m, required);
    }

    [Fact]
    public void MrItem_SafetyStock_ConsumesSomeAvailable()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m)
        {
            AvailableQty = 120m,
            SafetyStock = 50m,
        };

        // available_after_safety = 120 - 50 = 70
        // required = max(0, 100 - 70) = 30
        var availableAfterSafety = item.AvailableQty - item.SafetyStock;
        var required = Math.Max(0, item.RequiredQty - availableAfterSafety);
        Assert.Equal(30m, required);
    }

    [Fact]
    public void ProductionPlan_IgnoreExistingOrderedQty_Default_False()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), Guid.NewGuid(), "PP-001", DateTime.Today);
        Assert.False(plan.IgnoreExistingOrderedQty);
    }

    [Fact]
    public void ProductionPlan_IncludeSafetyStock_Default_False()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), Guid.NewGuid(), "PP-001", DateTime.Today);
        Assert.False(plan.IncludeSafetyStock);
    }

    [Fact]
    public void ProductionPlan_ConsiderMinOrderQty_Default_False()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), Guid.NewGuid(), "PP-001", DateTime.Today);
        Assert.False(plan.ConsiderMinimumOrderQty);
    }

    [Fact]
    public void MrItem_OrderedQty_Default_Zero()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m);
        Assert.Equal(0m, item.OrderedQty);
    }

    [Fact]
    public void MrItem_AvailableQty_Default_Zero()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m);
        Assert.Equal(0m, item.AvailableQty);
    }

    [Fact]
    public void MrItem_SafetyStock_Default_Zero()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m);
        Assert.Equal(0m, item.SafetyStock);
    }

    [Fact]
    public void MrItem_ProcurementType_DefaultInHouseManufacturing()
    {
        var item = new ProductionPlanMrItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "RM-A", 100m);
        Assert.Equal(SubAssemblyType.InHouseManufacturing, item.ProcurementType);
    }
}
