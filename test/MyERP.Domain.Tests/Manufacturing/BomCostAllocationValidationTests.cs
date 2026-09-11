using System;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class BomCostAllocationValidationTests
{
    [Fact]
    public void SetFgCostAllocation_AutoReducesFg_WhenSecondaryItemsExist()
    {
        // Per ERPNext PR #58979 / PR #58939:
        // When FG is 100% and secondary items have cost allocation, auto-reduce FG allocation.
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-001", Guid.NewGuid());
        var scrapItem = new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 2m)
        {
            CostAllocationPercentage = 25m,
        };
        bom.AddSecondaryItem(scrapItem);

        bom.SetFgCostAllocation();

        Assert.Equal(75m, bom.FgCostAllocationPercentage);
        Assert.True(bom.ValidateCostAllocation());
    }

    [Fact]
    public void ValidateCostAllocation_ReturnsFalse_WhenTotalDoesNotEqual100()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-002", Guid.NewGuid());
        var secondaryItem = new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.CoProduct, 1m)
        {
            CostAllocationPercentage = 20m,
        };
        bom.AddSecondaryItem(secondaryItem);

        // Manually force FG to 70% (total = 90% != 100%)
        bom.FgCostAllocationPercentage = 70m;

        Assert.False(bom.ValidateCostAllocation());
    }

    [Fact]
    public void LegacySecondaryItems_AreIgnoredInCostAllocation()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-003", Guid.NewGuid());
        var legacyScrap = new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.Scrap, 5m)
        {
            IsLegacy = true,
            CostAllocationPercentage = 15m,
        };
        bom.AddSecondaryItem(legacyScrap);

        bom.SetFgCostAllocation();

        // Legacy scrap does not reduce FG allocation
        Assert.Equal(100m, bom.FgCostAllocationPercentage);
        Assert.True(bom.ValidateCostAllocation());
        Assert.Equal(0m, bom.SecondaryItemsCost);
    }

    [Fact]
    public void RecalculateCost_DeductsSecondaryCostAllocationFromFg()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-004", Guid.NewGuid());
        var rawMaterial = new BomItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM-01", 10m, 10m); // Amount = 100
        bom.AddItem(rawMaterial);

        var byProduct = new BomSecondaryItem(
            Guid.NewGuid(), bom.Id, Guid.NewGuid(), SecondaryItemType.ByProduct, 1m)
        {
            CostAllocationPercentage = 30m, // 30% of RM cost
        };
        bom.AddSecondaryItem(byProduct);

        bom.RecalculateCost();

        Assert.Equal(100m, bom.TotalMaterialCost);
        Assert.Equal(30m, bom.SecondaryItemsCost);
        Assert.Equal(70m, bom.TotalCost); // 100 - 30 = 70
        Assert.Equal(30m, byProduct.Rate);
    }
}
