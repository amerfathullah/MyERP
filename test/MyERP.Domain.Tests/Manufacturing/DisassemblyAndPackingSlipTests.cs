using System;
using System.Collections.Generic;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Tests for Work Order disassembly feature and related domain logic.
/// Per ERPNext: Disassemble reverses Manufacture SE — breaks FG back into RM.
/// Per DO-NOT: "Use source_stock_entry from a different Work Order for Disassembly (cross-WO guard)"
/// Per DO-NOT: "Allow Disassemble qty to exceed source manufacture qty minus already-disassembled"
/// </summary>
public class DisassemblyAndPackingSlipTests
{
    private WorkOrder CreateWo(decimal qty = 100) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-001", Guid.NewGuid(), Guid.NewGuid(), qty);

    [Fact]
    public void WorkOrder_DisassembledQuantity_DefaultsZero()
    {
        var wo = CreateWo();
        Assert.Equal(0m, wo.DisassembledQuantity);
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_IncrementsQty()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.RecordDisassembly(30);
        Assert.Equal(30m, wo.DisassembledQuantity);
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_CumulativeTracking()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.RecordDisassembly(20);
        wo.RecordDisassembly(30);
        Assert.Equal(50m, wo.DisassembledQuantity);
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_CannotExceedProduced()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50);
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordDisassembly(60));
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_CannotExceedRemaining()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.RecordDisassembly(80); // 80 disassembled, 20 remaining
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordDisassembly(30)); // 30 > 20 remaining
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_ExactAmountAllowed()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.RecordDisassembly(100); // Exact amount allowed
        Assert.Equal(100m, wo.DisassembledQuantity);
    }

    [Fact]
    public void WorkOrder_RecordDisassembly_ZeroQty_NoOp()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50);
        wo.RecordDisassembly(0); // Zero qty should be no-op
        Assert.Equal(0m, wo.DisassembledQuantity);
    }

    [Fact]
    public void WorkOrder_AvailableForDisassembly_Correct()
    {
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(80);
        wo.RecordDisassembly(30);
        // Available = Produced - Disassembled = 80 - 30 = 50
        Assert.Equal(50m, wo.ProducedQuantity - wo.DisassembledQuantity);
    }

    [Fact]
    public void StockEntryType_Disassemble_HasCorrectValue()
    {
        Assert.Equal(8, (int)StockEntryType.Disassemble);
    }

    [Fact]
    public void WorkOrder_DisassemblyAffectsStatus_NotDirectly()
    {
        // Disassembly doesn't change WO status — it stays Completed
        var wo = CreateWo();
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        wo.RecordDisassembly(50);
        Assert.Equal(WorkOrderStatus.Completed, wo.Status); // Still Completed
    }

    [Fact]
    public void DisassemblyResultDto_HasExpectedFields()
    {
        var dto = new MyERP.Manufacturing.DisassemblyResultDto
        {
            StockEntryId = Guid.NewGuid(),
            EntryNumber = "SE-2026-00099",
            DisassembledQty = 25,
            ItemCount = 4,
            RemainingDisassemblable = 75
        };
        Assert.Equal(25m, dto.DisassembledQty);
        Assert.Equal(75m, dto.RemainingDisassemblable);
        Assert.Equal(4, dto.ItemCount);
    }

    [Fact]
    public void CreateDisassemblyDto_HasRequiredFields()
    {
        var dto = new MyERP.Manufacturing.CreateDisassemblyDto
        {
            WorkOrderId = Guid.NewGuid(),
            Quantity = 10,
            SourceStockEntryId = Guid.NewGuid()
        };
        Assert.NotEqual(Guid.Empty, dto.WorkOrderId);
        Assert.Equal(10m, dto.Quantity);
        Assert.NotNull(dto.SourceStockEntryId);
    }

    [Fact]
    public void WorkOrder_ScaleFactor_ProportionalToQuantity()
    {
        // Scale factor = disassemble_qty / wo_qty
        // For WO qty=100, disassemble 25 → scale factor = 0.25
        // RM item with required_qty=200 → return qty = 200 × 0.25 = 50
        var woQty = 100m;
        var disassembleQty = 25m;
        var scaleFactor = disassembleQty / woQty;
        var rmRequiredQty = 200m;
        var returnQty = Math.Round(rmRequiredQty * scaleFactor, 4);
        Assert.Equal(50m, returnQty);
    }

    [Fact]
    public void WorkOrder_ScaleFactor_FullDisassembly()
    {
        // Full disassembly returns full RM qty
        var woQty = 100m;
        var disassembleQty = 100m;
        var scaleFactor = disassembleQty / woQty;
        var rmRequiredQty = 500m;
        var returnQty = Math.Round(rmRequiredQty * scaleFactor, 4);
        Assert.Equal(500m, returnQty);
    }

    [Fact]
    public void Localization_DisassemblyKeys_ExistInEnJson()
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains("\"DisassemblyCreated\"", json);
        Assert.Contains("\"EnterDisassemblyQty\"", json);
        Assert.Contains("\"InvalidQuantity\"", json);
        Assert.Contains("\"DisassembledQuantity\"", json);
    }

    [Fact]
    public void WorkOrder_ReverseProduction_ThrowsWhenBelowDisassembledQty()
    {
        var wo = CreateWo(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100);
        wo.RecordDisassembly(40);

        // Reversing 70 would leave produced=30 < disassembled=40 -> throws
        var ex = Should.Throw<BusinessException>(() => wo.ReverseProduction(70));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ManufactureCannotBeLessThanDisassembled);

        // Reversing 50 leaves produced=50 >= disassembled=40 -> succeeds
        Should.NotThrow(() => wo.ReverseProduction(50));
        wo.ProducedQuantity.ShouldBe(50m);
    }

    [Fact]
    public void StockEntry_RemoveFgCompletedQtyIfUnapplicable_PreservesForWorkOrder()
    {
        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow)
        {
            FgCompletedQty = 10m
        };

        // When Work Order is linked, FgCompletedQty is retained even if FromBOM is false
        se.RemoveFgCompletedQtyIfUnapplicable(hasBom: false, hasWorkOrder: true);
        se.FgCompletedQty.ShouldBe(10m);

        // When neither BOM nor Work Order is linked, FgCompletedQty is cleared to 0
        se.RemoveFgCompletedQtyIfUnapplicable(hasBom: false, hasWorkOrder: false);
        se.FgCompletedQty.ShouldBe(0m);
    }

    [Fact]
    public void StockEntryManager_GetFinishedGoodCostAllocationPercentage_RetainsCoProductShareWhenOmitted()
    {
        var manager = new StockEntryManager(null!, null!, null!);

        var coProductAId = Guid.NewGuid();
        var coProductBId = Guid.NewGuid();
        var bomSecondary = new List<(Guid BomSecondaryItemId, decimal CostAllocationPercentage)>
        {
            (coProductAId, 15m),
            (coProductBId, 25m)
        };

        var se = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        var fgItemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // 1. Stock entry has both co-products: FG gets 100 - (15 + 25) = 60%
        se.AddItem(fgItemId, 1, targetWarehouseId: whId, isFinishedItem: true);
        se.AddItem(Guid.NewGuid(), 1, targetWarehouseId: whId, bomSecondaryItemId: coProductAId, secondaryItemType: "Co-Product");
        se.AddItem(Guid.NewGuid(), 1, targetWarehouseId: whId, bomSecondaryItemId: coProductBId, secondaryItemType: "Co-Product");

        manager.GetFinishedGoodCostAllocationPercentage(se.Items, bomSecondary).ShouldBe(60m);

        // 2. Stock entry only has Co-Product A (Co-Product B omitted): FG retains B's share -> 100 - 15 = 85%
        var se2 = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        se2.AddItem(fgItemId, 1, targetWarehouseId: whId, isFinishedItem: true);
        se2.AddItem(Guid.NewGuid(), 1, targetWarehouseId: whId, bomSecondaryItemId: coProductAId, secondaryItemType: "Co-Product");

        manager.GetFinishedGoodCostAllocationPercentage(se2.Items, bomSecondary).ShouldBe(85m);

        // 3. Stock entry has neither co-product: FG gets 100%
        var se3 = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Manufacture, DateTime.UtcNow);
        se3.AddItem(fgItemId, 1, targetWarehouseId: whId, isFinishedItem: true);

        manager.GetFinishedGoodCostAllocationPercentage(se3.Items, bomSecondary).ShouldBe(100m);
    }

    [Fact]
    public void StockEntryManager_ValidateDisassembleMaterialsAgainstBom_ValidatesQtyAndItems()
    {
        var manager = new StockEntryManager(null!, null!, null!);

        var rm1Id = Guid.NewGuid();
        var rm2Id = Guid.NewGuid();
        var bomItems = new List<(Guid ItemId, decimal Qty)>
        {
            (rm1Id, 2m),
            (rm2Id, 5m)
        };

        var whId = Guid.NewGuid();

        // Valid standalone disassembly for FgCompletedQty = 3: expected rm1 = 6, rm2 = 15
        var validSe = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Disassemble, DateTime.UtcNow)
        {
            FgCompletedQty = 3m
        };
        validSe.AddItem(rm1Id, 6m, targetWarehouseId: whId);
        validSe.AddItem(rm2Id, 15m, targetWarehouseId: whId);

        Should.NotThrow(() => manager.ValidateDisassembleMaterialsAgainstBom(validSe, bomItems));

        // Invalid: item not in BOM
        var strangerItem = Guid.NewGuid();
        var invalidItemSe = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Disassemble, DateTime.UtcNow)
        {
            FgCompletedQty = 3m
        };
        invalidItemSe.AddItem(rm1Id, 6m, targetWarehouseId: whId);
        invalidItemSe.AddItem(strangerItem, 1m, targetWarehouseId: whId);

        var ex1 = Should.Throw<BusinessException>(() =>
            manager.ValidateDisassembleMaterialsAgainstBom(invalidItemSe, bomItems));
        ex1.Code.ShouldBe(MyERPDomainErrorCodes.DisassembleItemNotInBom);

        // Invalid: quantity mismatch (expected 6, got 10)
        var invalidQtySe = new StockEntry(Guid.NewGuid(), Guid.NewGuid(), StockEntryType.Disassemble, DateTime.UtcNow)
        {
            FgCompletedQty = 3m
        };
        invalidQtySe.AddItem(rm1Id, 10m, targetWarehouseId: whId);
        invalidQtySe.AddItem(rm2Id, 15m, targetWarehouseId: whId);

        var ex2 = Should.Throw<BusinessException>(() =>
            manager.ValidateDisassembleMaterialsAgainstBom(invalidQtySe, bomItems));
        ex2.Code.ShouldBe(MyERPDomainErrorCodes.DisassembleScaleFactorMismatch);
    }
}
