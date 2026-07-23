using System;
using System.Linq;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for BOM Secondary Items (Co-Product/By-Product/Scrap) system.
/// Per ERPNext v16 gotcha #85: "BOM Scrap Item renamed to Secondary Item"
/// Per DO-NOT: FG cannot be secondary, process_loss < 100%, cost allocation = 100%
/// </summary>
public class BomSecondaryItemTests
{
    private static BillOfMaterials CreateBom(Guid? itemId = null)
    {
        return new BillOfMaterials(
            Guid.NewGuid(), Guid.NewGuid(), "BOM-TEST-001",
            itemId ?? Guid.NewGuid());
    }

    private static BomSecondaryItem CreateSecondaryItem(
        Guid bomId, Guid? itemId = null,
        SecondaryItemType type = SecondaryItemType.Scrap,
        decimal qty = 10, decimal costAllocation = 0, decimal processLoss = 0)
    {
        var item = new BomSecondaryItem(
            Guid.NewGuid(), bomId, itemId ?? Guid.NewGuid(), type, qty);
        item.CostAllocationPercentage = costAllocation;
        item.ProcessLossPercentage = processLoss;
        return item;
    }

    // === SecondaryItemType Enum ===

    [Fact]
    public void SecondaryItemType_HasFourValues()
    {
        Enum.GetValues<SecondaryItemType>().Length.ShouldBe(4);
    }

    [Fact]
    public void SecondaryItemType_CoProduct_IsZero()
    {
        ((int)SecondaryItemType.CoProduct).ShouldBe(0);
    }

    [Fact]
    public void SecondaryItemType_AdditionalFinishedGood_IsThree()
    {
        // Per gotcha #175: SE Detail has 4th type not available at BOM level
        ((int)SecondaryItemType.AdditionalFinishedGood).ShouldBe(3);
    }

    // === BomSecondaryItem Entity ===

    [Fact]
    public void BomSecondaryItem_Defaults()
    {
        var item = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SecondaryItemType.ByProduct, 5);

        item.SecondaryItemType.ShouldBe(SecondaryItemType.ByProduct);
        item.Quantity.ShouldBe(5);
        item.Rate.ShouldBe(0);
        item.CostAllocationPercentage.ShouldBe(0);
        item.ProcessLossPercentage.ShouldBe(0);
        item.IsLegacy.ShouldBeFalse();
        item.WarehouseId.ShouldBeNull();
    }

    [Fact]
    public void BomSecondaryItem_Amount_IsQuantityTimesRate()
    {
        var item = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SecondaryItemType.CoProduct, 10);
        item.Rate = 25m;
        item.Amount.ShouldBe(250m); // 10 × 25
    }

    [Fact]
    public void BomSecondaryItem_EffectiveQuantity_WithNoLoss_EqualsFull()
    {
        var item = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SecondaryItemType.CoProduct, 100);
        item.EffectiveQuantity.ShouldBe(100m);
    }

    [Fact]
    public void BomSecondaryItem_EffectiveQuantity_WithProcessLoss_Reduces()
    {
        // Per gotcha #442: per-secondary process_loss
        var item = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SecondaryItemType.Scrap, 100);
        item.ProcessLossPercentage = 20m;
        item.EffectiveQuantity.ShouldBe(80m); // 100 × (1 - 20/100)
    }

    [Fact]
    public void BomSecondaryItem_Amount_UsesEffectiveQuantity()
    {
        var item = new BomSecondaryItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            SecondaryItemType.ByProduct, 50);
        item.ProcessLossPercentage = 10m;
        item.Rate = 20m;
        // EffectiveQty = 50 × (1 - 10/100) = 45
        // Amount = 45 × 20 = 900
        item.Amount.ShouldBe(900m);
    }

    // === BOM — AddSecondaryItem ===

    [Fact]
    public void BOM_AddSecondaryItem_Success()
    {
        var bom = CreateBom();
        var si = CreateSecondaryItem(bom.Id);
        bom.AddSecondaryItem(si);
        bom.SecondaryItems.Count.ShouldBe(1);
    }

    [Fact]
    public void BOM_AddSecondaryItem_FgItem_Throws()
    {
        // Per DO-NOT: "FG item cannot appear in secondary_items table"
        var fgItemId = Guid.NewGuid();
        var bom = CreateBom(itemId: fgItemId);
        var si = CreateSecondaryItem(bom.Id, itemId: fgItemId);

        var ex = Should.Throw<BusinessException>(() => bom.AddSecondaryItem(si));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.BomFgCannotBeSecondaryItem);
    }

    [Fact]
    public void BOM_AddSecondaryItem_ProcessLoss100_Throws()
    {
        // Per DO-NOT: "process_loss_per >= 100 per secondary item → hard throw"
        var bom = CreateBom();
        var si = CreateSecondaryItem(bom.Id, processLoss: 100m);

        var ex = Should.Throw<BusinessException>(() => bom.AddSecondaryItem(si));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidProcessLossPercentage);
    }

    [Fact]
    public void BOM_AddSecondaryItem_ProcessLoss99_Succeeds()
    {
        var bom = CreateBom();
        var si = CreateSecondaryItem(bom.Id, processLoss: 99.99m);
        bom.AddSecondaryItem(si);
        bom.SecondaryItems.Count.ShouldBe(1);
    }

    // === Cost Allocation ===

    [Fact]
    public void BOM_FgCostAllocationPercentage_NoSecondaryItems_Is100()
    {
        var bom = CreateBom();
        bom.FgCostAllocationPercentage.ShouldBe(100m);
    }

    [Fact]
    public void BOM_FgCostAllocationPercentage_WithSecondaryItems_AutoReduces()
    {
        // Per gotcha #518: FG auto-reduces = 100 - total_secondary_pct
        var bom = CreateBom();
        var si1 = CreateSecondaryItem(bom.Id, costAllocation: 20m);
        var si2 = CreateSecondaryItem(bom.Id, costAllocation: 10m);
        bom.AddSecondaryItem(si1);
        bom.AddSecondaryItem(si2);

        bom.FgCostAllocationPercentage.ShouldBe(70m); // 100 - 20 - 10
    }

    [Fact]
    public void BOM_ValidateCostAllocation_Valid_Returns_True()
    {
        var bom = CreateBom();
        var si = CreateSecondaryItem(bom.Id, costAllocation: 30m);
        bom.AddSecondaryItem(si);
        bom.ValidateCostAllocation().ShouldBeTrue(); // FG=70%, SI=30% → total 100%
    }

    [Fact]
    public void BOM_ValidateCostAllocation_ExceedsTotal_Returns_False()
    {
        var bom = CreateBom();
        var si1 = CreateSecondaryItem(bom.Id, costAllocation: 60m);
        var si2 = CreateSecondaryItem(bom.Id, costAllocation: 50m);
        bom.AddSecondaryItem(si1);
        bom.AddSecondaryItem(si2);
        bom.ValidateCostAllocation().ShouldBeFalse(); // 60+50=110% > 100%
    }

    [Fact]
    public void BOM_ValidateCostAllocation_NoAllocation_Returns_True()
    {
        var bom = CreateBom();
        var si = CreateSecondaryItem(bom.Id, costAllocation: 0m);
        bom.AddSecondaryItem(si);
        bom.ValidateCostAllocation().ShouldBeTrue(); // No allocation = FG gets 100% implicitly
    }

    // === RecalculateCost with Secondary Items ===

    [Fact]
    public void BOM_RecalculateCost_DistributesCostToSecondaryItems()
    {
        var bom = CreateBom();
        // Add raw material items
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 10, 50)); // 500
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Plastic", 5, 20)); // 100
        // Total material cost = 600

        // Add secondary item with 20% cost allocation, qty=10
        var si = CreateSecondaryItem(bom.Id, costAllocation: 20m, qty: 10);
        bom.AddSecondaryItem(si);

        bom.RecalculateCost();

        bom.TotalMaterialCost.ShouldBe(600m);
        // SI rate = 600 × 20% / 10 = 12
        si.Rate.ShouldBe(12m);
    }

    [Fact]
    public void BOM_RecalculateCost_ZeroCostAllocation_NoRateAssignment()
    {
        var bom = CreateBom();
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel", 10, 50));

        var si = CreateSecondaryItem(bom.Id, costAllocation: 0m, qty: 10);
        si.Rate = 5m; // preset rate for scrap
        bom.AddSecondaryItem(si);

        bom.RecalculateCost();

        // Rate should NOT be overwritten when cost_allocation = 0
        si.Rate.ShouldBe(5m);
    }

    // === BOM Process Loss ===

    [Fact]
    public void BOM_ProcessLossPercentage_DefaultsToZero()
    {
        var bom = CreateBom();
        bom.ProcessLossPercentage.ShouldBe(0);
        bom.ProcessLossQty.ShouldBe(0);
    }

    [Fact]
    public void BOM_ProcessLossQty_CalculatedFromPercentage()
    {
        var bom = CreateBom();
        bom.Quantity = 100;
        bom.ProcessLossPercentage = 5m;
        bom.ProcessLossQty.ShouldBe(5m); // 100 × 5/100
    }

    [Fact]
    public void BOM_ScrapWarehouseId_DefaultsNull()
    {
        var bom = CreateBom();
        bom.ScrapWarehouseId.ShouldBeNull();
    }

    [Fact]
    public void BOM_ScrapWarehouseId_CanBeSet()
    {
        var bom = CreateBom();
        var whId = Guid.NewGuid();
        bom.ScrapWarehouseId = whId;
        bom.ScrapWarehouseId.ShouldBe(whId);
    }

    // === Work Order Process Loss Integration ===

    [Fact]
    public void WorkOrder_ProcessLossQty_PriorityOverPercentage()
    {
        // Per gotcha #1778: process_loss 3-level priority: WO Operation MAX > BOM percentage > bidirectional
        var wo = new WorkOrder(
            Guid.NewGuid(), Guid.NewGuid(), "WO-001",
            Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.ProcessLossQty = 7; // Explicit qty takes priority
        wo.ProcessLossPercentage = 10; // Would give 10, but qty override wins
        wo.EffectiveFgQuantity.ShouldBe(93m); // 100 - 7 (uses qty, not percentage)
    }
}
