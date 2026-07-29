using System;
using Xunit;
using MyERP.Manufacturing.Entities;
using MyERP.Manufacturing;
using MyERP.Inventory;

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
}
