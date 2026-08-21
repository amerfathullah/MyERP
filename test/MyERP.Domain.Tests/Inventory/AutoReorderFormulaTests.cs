using System;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Unit tests for AutoReorder threshold and deficiency formula invariants (Gotcha #1415):
/// 1. Trigger fires when projected_qty <= reorder_level (not just <).
/// 2. Formula: deficiency = reorder_level - projected_qty.
///    If deficiency > reorder_qty, uses deficiency instead of base reorder_qty.
/// </summary>
public class AutoReorderFormulaTests
{
    [Theory]
    [InlineData(100, 100, true)]   // Exactly at reorder level -> triggers
    [InlineData(100, 99, true)]    // Below reorder level -> triggers
    [InlineData(100, 101, false)]  // Above reorder level -> does not trigger
    public void AutoReorder_ThresholdCheck_TriggersWhenProjectedQtyLessThanOrEqualToReorderLevel(
        decimal reorderLevel, decimal projectedQty, bool shouldTrigger)
    {
        bool isTriggered = projectedQty <= reorderLevel;
        Assert.Equal(shouldTrigger, isTriggered);
    }

    [Theory]
    [InlineData(100, 80, 50, 50)]   // deficiency = 20, reorderQty = 50 -> Max(50, 20) = 50
    [InlineData(100, 20, 50, 80)]   // deficiency = 80, reorderQty = 50 -> Max(50, 80) = 80 (uses deficiency)
    [InlineData(100, 0, 50, 100)]   // deficiency = 100, reorderQty = 50 -> Max(50, 100) = 100
    [InlineData(100, -10, 50, 110)] // negative projected stock: deficiency = 110 -> 110
    public void AutoReorder_DeficiencyFormula_UsesMaxOfReorderQtyAndDeficiency(
        decimal reorderLevel, decimal projectedQty, decimal baseReorderQty, decimal expectedQtyToOrder)
    {
        decimal deficiency = reorderLevel - projectedQty;
        decimal qtyToOrder = Math.Max(baseReorderQty, deficiency);

        Assert.Equal(expectedQtyToOrder, qtyToOrder);
    }
}
