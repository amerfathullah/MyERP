using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for business logic migration (July 24 session):
/// - Cost Center Allocation GL distribution
/// - Deferred Expense recognition
/// - Repack/Disassemble Stock Entry purpose validation
/// - SCR RM consumption tracking
/// </summary>
public class BusinessLogicMigrationTests
{
    // ═══════════════════════════════════════════════
    // Cost Center Allocation GL Distribution
    // ═══════════════════════════════════════════════

    [Fact]
    public void CostCenterAllocation_EvenSplit_DistributesProperly()
    {
        var alloc = CreateTestAllocation(new[] { (Guid.NewGuid(), 50m), (Guid.NewGuid(), 50m) });
        var result = alloc.Distribute(1000m);
        result.Count.ShouldBe(2);
        result.Sum(r => r.Amount).ShouldBe(1000m);
        result[0].Amount.ShouldBe(500m);
        result[1].Amount.ShouldBe(500m);
    }

    [Fact]
    public void CostCenterAllocation_UnevenSplit_RemainderToFirst()
    {
        var alloc = CreateTestAllocation(new[] { (Guid.NewGuid(), 33.33m), (Guid.NewGuid(), 33.33m), (Guid.NewGuid(), 33.34m) });
        var result = alloc.Distribute(100m);
        result.Count.ShouldBe(3);
        // Total must exactly equal input amount (rounding remainder to first entry)
        result.Sum(r => r.Amount).ShouldBe(100m);
    }

    [Fact]
    public void CostCenterAllocation_SelfReference_Throws()
    {
        var ccId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), ccId, DateTime.UtcNow);
        Should.Throw<BusinessException>(() => alloc.AddEntry(ccId, 100m));
    }

    [Fact]
    public void CostCenterAllocation_PercentageMustSumTo100()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 30m);
        // Sum = 90%, not 100% — should fail validation
        Should.Throw<BusinessException>(() => alloc.ValidatePercentages());
    }

    [Fact]
    public void CostCenterAllocation_Valid100Percent_Passes()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 40m);
        Should.NotThrow(() => alloc.ValidatePercentages());
    }

    // ═══════════════════════════════════════════════
    // Deferred Expense Recognition
    // ═══════════════════════════════════════════════

    [Fact]
    public void PurchaseInvoiceItem_DeferredExpenseFields_DefaultFalse()
    {
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Annual Maintenance Contract", 1, 12000, 0);
        item.EnableDeferredExpense.ShouldBeFalse();
        item.DeferredExpenseAccountId.ShouldBeNull();
        item.ServiceStartDate.ShouldBeNull();
        item.ServiceEndDate.ShouldBeNull();
    }

    [Fact]
    public void PurchaseInvoiceItem_DeferredExpenseFields_CanBeSet()
    {
        var item = new PurchaseInvoiceItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Annual Maintenance Contract", 1, 12000, 0);
        var deferredAccountId = Guid.NewGuid();

        item.EnableDeferredExpense = true;
        item.DeferredExpenseAccountId = deferredAccountId;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);

        item.EnableDeferredExpense.ShouldBeTrue();
        item.DeferredExpenseAccountId.ShouldBe(deferredAccountId);
        item.ServiceStartDate.ShouldBe(new DateTime(2026, 1, 1));
        item.ServiceEndDate.ShouldBe(new DateTime(2026, 12, 31));
    }

    [Fact]
    public void DeferredExpenseSchedule_12Month_ProportionalAmounts()
    {
        // Simulate a 12-month schedule (same logic as revenue, mirrored for expense)
        var totalAmount = 12000m;
        var totalMonths = 12;
        var monthlyAmount = Math.Round(totalAmount / totalMonths, 2);

        monthlyAmount.ShouldBe(1000m);

        // Final period absorbs rounding (exact match)
        decimal bookedSoFar = monthlyAmount * 11;
        var finalAmount = totalAmount - bookedSoFar;
        finalAmount.ShouldBe(1000m); // No rounding error for even division
    }

    [Fact]
    public void DeferredExpenseSchedule_7Month_FinalAbsorbsRounding()
    {
        var totalAmount = 10000m;
        var totalMonths = 7;
        var monthlyAmount = Math.Round(totalAmount / totalMonths, 2);

        monthlyAmount.ShouldBe(1428.57m);

        decimal bookedSoFar = monthlyAmount * 6;
        var finalAmount = totalAmount - bookedSoFar;
        // 10000 - (1428.57 × 6) = 10000 - 8571.42 = 1428.58
        finalAmount.ShouldBe(1428.58m);
    }

    // ═══════════════════════════════════════════════
    // Repack Stock Entry Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void Repack_RequiresOutgoingAndIncomingItems()
    {
        var entry = CreateStockEntry(StockEntryType.Repack);
        // Only incoming items (no outgoing) — should fail
        entry.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());

        var manager = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => manager.ValidateRepackItems(entry));
    }

    [Fact]
    public void Repack_SingleFG_AutoRateCalculation()
    {
        var manager = new StockEntryManager(null!, null!);

        var items = new List<StockEntryItem>
        {
            CreateSEItem(10, 50, isFinished: false),  // 10 × 50 = 500 outgoing
            CreateSEItem(5, 0, isFinished: false),     // 5 × 0 = 0 outgoing
        };

        var fgRate = manager.CalculateRepackFgRate(items, 2m);
        // Total outgoing cost = 500, FG qty = 2 → rate = 250
        fgRate.ShouldBe(250m);
    }

    [Fact]
    public void Repack_MultiFG_RequiresManualRate()
    {
        var entry = CreateStockEntry(StockEntryType.Repack);
        var sourceWh = Guid.NewGuid();
        var targetWh = Guid.NewGuid();

        // Add outgoing item
        entry.AddItem(Guid.NewGuid(), 10, sourceWh, null);

        // Add 2 incoming FG items without SetBasicRateManually
        entry.AddItem(Guid.NewGuid(), 5, null, targetWh);
        entry.AddItem(Guid.NewGuid(), 5, null, targetWh);

        var manager = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => manager.ValidateRepackItems(entry));
    }

    [Fact]
    public void Repack_NotAppliedToOtherTypes()
    {
        var entry = CreateStockEntry(StockEntryType.MaterialReceipt);
        entry.AddItem(Guid.NewGuid(), 10, null, Guid.NewGuid());

        var manager = new StockEntryManager(null!, null!);
        // Should not throw for non-Repack types
        Should.NotThrow(() => manager.ValidateRepackItems(entry));
    }

    // ═══════════════════════════════════════════════
    // Disassemble Stock Entry Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void Disassemble_CrossWorkOrder_Throws()
    {
        var entry = CreateStockEntry(StockEntryType.Disassemble);
        entry.GetType().GetProperty("WorkOrderId")!.SetValue(entry, (Guid?)Guid.NewGuid());

        var sourceEntry = CreateStockEntry(StockEntryType.Manufacture);
        typeof(StockEntry).GetProperty("WorkOrderId")!.SetValue(sourceEntry, (Guid?)Guid.NewGuid());

        entry.FgCompletedQty = 5;
        sourceEntry.FgCompletedQty = 10;
        entry.GetType().GetProperty("SourceStockEntryId")!.SetValue(entry, (Guid?)sourceEntry.Id);

        var manager = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => manager.ValidateDisassembleItems(entry, sourceEntry));
    }

    [Fact]
    public void Disassemble_QtyExceedsSource_Throws()
    {
        var entry = CreateStockEntry(StockEntryType.Disassemble);
        var sourceEntry = CreateStockEntry(StockEntryType.Manufacture);

        entry.FgCompletedQty = 15;
        sourceEntry.FgCompletedQty = 10;

        var manager = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => manager.ValidateDisassembleItems(entry, sourceEntry));
    }

    [Fact]
    public void Disassemble_WithinSourceQty_Succeeds()
    {
        var entry = CreateStockEntry(StockEntryType.Disassemble);
        var sourceEntry = CreateStockEntry(StockEntryType.Manufacture);

        entry.FgCompletedQty = 5;
        sourceEntry.FgCompletedQty = 10;

        var manager = new StockEntryManager(null!, null!);
        Should.NotThrow(() => manager.ValidateDisassembleItems(entry, sourceEntry));
    }

    [Fact]
    public void Disassemble_ScaleFactorValidation_MatchingQty_Passes()
    {
        var sourceFgQty = 10m;
        var disassembleQty = 5m;
        var scaleFactor = disassembleQty / sourceFgQty; // 0.5

        var sourceItems = new List<StockEntryItem>
        {
            CreateSEItem(20, 10, isFinished: false), // 20 units of RM
        };

        var disassemblyItems = new List<StockEntryItem>
        {
            CreateSEItem(10, 10, isFinished: false), // 20 × 0.5 = 10 (correct)
        };
        disassemblyItems[0].ItemId = sourceItems[0].ItemId;

        var manager = new StockEntryManager(null!, null!);
        Should.NotThrow(() => manager.ValidateDisassembleScaleFactor(
            disassemblyItems, sourceItems, disassembleQty, sourceFgQty));
    }

    [Fact]
    public void Disassemble_ScaleFactorValidation_MismatchedQty_Throws()
    {
        var sourceFgQty = 10m;
        var disassembleQty = 5m;

        var sourceItems = new List<StockEntryItem>
        {
            CreateSEItem(20, 10, isFinished: false),
        };

        var disassemblyItems = new List<StockEntryItem>
        {
            CreateSEItem(15, 10, isFinished: false), // Expected 10, got 15 — scale factor violation
        };
        disassemblyItems[0].ItemId = sourceItems[0].ItemId;

        var manager = new StockEntryManager(null!, null!);
        Should.Throw<BusinessException>(() => manager.ValidateDisassembleScaleFactor(
            disassemblyItems, sourceItems, disassembleQty, sourceFgQty));
    }

    // ═══════════════════════════════════════════════
    // Stock Entry Item Fields
    // ═══════════════════════════════════════════════

    [Fact]
    public void StockEntryItem_IsFinishedItem_DefaultsFalse()
    {
        var item = new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, null, Guid.NewGuid());
        item.IsFinishedItem.ShouldBeFalse();
        item.SetBasicRateManually.ShouldBeFalse();
        item.SecondaryItemType.ShouldBeNull();
        item.ProcessLossPercentage.ShouldBe(0);
        item.SourceStockEntryDetailId.ShouldBeNull();
    }

    [Fact]
    public void StockEntryItem_IsFinishedItem_CanBeSet()
    {
        var item = new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, null, Guid.NewGuid());
        item.IsFinishedItem = true;
        item.SetBasicRateManually = true;
        item.SecondaryItemType = "Scrap";
        item.ProcessLossPercentage = 5m;
        item.SourceStockEntryDetailId = Guid.NewGuid();

        item.IsFinishedItem.ShouldBeTrue();
        item.SetBasicRateManually.ShouldBeTrue();
        item.SecondaryItemType.ShouldBe("Scrap");
        item.ProcessLossPercentage.ShouldBe(5m);
        item.SourceStockEntryDetailId.ShouldNotBeNull();
    }

    [Fact]
    public void StockEntry_DisassembleFields_DefaultNull()
    {
        var entry = CreateStockEntry(StockEntryType.Disassemble);
        entry.SourceStockEntryId.ShouldBeNull();
        entry.FgCompletedQty.ShouldBe(0);
        entry.ProcessLossQty.ShouldBe(0);
        entry.ProcessLossPercentage.ShouldBe(0);
    }

    // ═══════════════════════════════════════════════
    // SCR RM Consumption
    // ═══════════════════════════════════════════════

    [Fact]
    public void SubcontractingManager_RmConsumption_ProportionalToReceivedQty()
    {
        // SCO has 100 FG total, supplied 200 RM.
        // Receiving 25 FG → should consume 50 RM (25/100 ratio)
        var sco = CreateScoWithSuppliedItems(fgQty: 100, rmRequiredQty: 200);

        var manager = new SubcontractingManager(null!, null!);
        var result = manager.CalculateRmConsumption(sco, receivedFgQty: 25);

        result.ShouldNotBeEmpty();
        result[0].ConsumedQty.ShouldBe(50m); // 200 × (25/100) = 50
    }

    [Fact]
    public void SubcontractingManager_RmConsumption_FullReceipt()
    {
        var sco = CreateScoWithSuppliedItems(fgQty: 50, rmRequiredQty: 100);

        var manager = new SubcontractingManager(null!, null!);
        var result = manager.CalculateRmConsumption(sco, receivedFgQty: 50);

        result[0].ConsumedQty.ShouldBe(100m); // Full consumption
    }

    [Fact]
    public void SubcontractingManager_RmConsumption_ZeroQty_ReturnsEmpty()
    {
        var sco = CreateScoWithSuppliedItems(fgQty: 0, rmRequiredQty: 100);

        var manager = new SubcontractingManager(null!, null!);
        var result = manager.CalculateRmConsumption(sco, receivedFgQty: 10);

        result.ShouldBeEmpty();
    }

    // ═══════════════════════════════════════════════
    // Error Code Constants
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData(MyERPDomainErrorCodes.RepackMissingItems, "MyERP:05041")]
    [InlineData(MyERPDomainErrorCodes.RepackMultiFgManualRate, "MyERP:05042")]
    [InlineData(MyERPDomainErrorCodes.DisassembleSourceNotFound, "MyERP:05043")]
    [InlineData(MyERPDomainErrorCodes.DisassembleCrossWorkOrder, "MyERP:05044")]
    [InlineData(MyERPDomainErrorCodes.DisassembleQtyExceedsSource, "MyERP:05045")]
    [InlineData(MyERPDomainErrorCodes.DisassembleScaleFactorMismatch, "MyERP:05046")]
    public void ErrorCodes_Exist(string code, string expected)
    {
        code.ShouldBe(expected);
    }

    // ═══════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════

    private static StockEntry CreateStockEntry(StockEntryType type)
    {
        return new StockEntry(Guid.NewGuid(), Guid.NewGuid(), type, DateTime.UtcNow);
    }

    private static StockEntryItem CreateSEItem(decimal qty, decimal rate, bool isFinished = false)
    {
        var item = new StockEntryItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), qty,
            isFinished ? null : Guid.NewGuid(),
            isFinished ? Guid.NewGuid() : null,
            rate);
        item.IsFinishedItem = isFinished;
        return item;
    }

    private static CostCenterAllocation CreateTestAllocation(
        (Guid CostCenterId, decimal Percentage)[] entries)
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        foreach (var (ccId, pct) in entries)
            alloc.AddEntry(ccId, pct);
        return alloc;
    }

    private static SubcontractingOrder CreateScoWithSuppliedItems(decimal fgQty, decimal rmRequiredQty)
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), Guid.NewGuid(), "SCO-TEST-001", DateTime.UtcNow, Guid.NewGuid());
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "FG Item", fgQty, 100));

        var rmItemId = Guid.NewGuid();
        sco.AddSuppliedItem(new SubcontractingOrderSuppliedItem(
            Guid.NewGuid(), sco.Id, rmItemId, "Raw Material", rmRequiredQty));

        return sco;
    }
}
