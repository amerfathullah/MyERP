using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Inventory;
using MyERP.Purchasing;
using MyERP.Shared;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests verifying domain services that were previously orphaned (not wired into AppServices)
/// now function correctly and enforce their business rules.
/// </summary>
public class OrphanedServiceWiringTests
{
    // === GlRepostService ===

    [Fact]
    public void GlRepostService_AllowedVoucherTypes_Contains_SevenTypes()
    {
        Assert.Contains("SalesInvoice", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("PurchaseInvoice", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("PaymentEntry", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("JournalEntry", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("PurchaseReceipt", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("DeliveryNote", GlRepostService.AllowedVoucherTypes);
        Assert.Contains("StockEntry", GlRepostService.AllowedVoucherTypes);
    }

    [Fact]
    public void GlRepostService_IsRepostAllowed_RejectsUnknownTypes()
    {
        Assert.False(GlRepostService.IsRepostAllowed("BankTransaction"));
        Assert.False(GlRepostService.IsRepostAllowed("MaterialRequest"));
        Assert.False(GlRepostService.IsRepostAllowed("WorkOrder"));
        Assert.False(GlRepostService.IsRepostAllowed("Quotation"));
    }

    [Fact]
    public void GlRepostService_IsRepostAllowed_CaseInsensitive()
    {
        Assert.True(GlRepostService.IsRepostAllowed("salesinvoice"));
        Assert.True(GlRepostService.IsRepostAllowed("PURCHASEINVOICE"));
        Assert.True(GlRepostService.IsRepostAllowed("JournalEntry"));
    }

    [Fact]
    public void GlRepostResult_TotalProcessed_SumsAllCategories()
    {
        var result = new GlRepostResult(5, 2, 1, new List<string> { "error" });
        Assert.Equal(8, result.TotalProcessed);
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void GlRepostResult_NoErrors_WhenFailedIsZero()
    {
        var result = new GlRepostResult(10, 3, 0, new List<string>());
        Assert.False(result.HasErrors);
        Assert.Equal(13, result.TotalProcessed);
    }

    // === StockEntryManager ===

    [Fact]
    public void StockEntryManager_TransferQty_WithinLimit_Succeeds()
    {
        var manager = new StockEntryManager(null!, null!);
        // No exception expected: requesting 5, allowed = 10 - 3 = 7
        manager.ValidateTransferQty(requiredQty: 10, transferredQty: 3, requestedQty: 5);
    }

    [Fact]
    public void StockEntryManager_TransferQty_ExceedsLimit_Throws()
    {
        var manager = new StockEntryManager(null!, null!);
        // allowed = 10 - 8 = 2, requesting 5 → exceeds
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            manager.ValidateTransferQty(requiredQty: 10, transferredQty: 8, requestedQty: 5));
    }

    [Fact]
    public void StockEntryManager_TransferQty_ExactLimit_Succeeds()
    {
        var manager = new StockEntryManager(null!, null!);
        // allowed = 10 - 7 = 3, requesting exactly 3 → OK
        manager.ValidateTransferQty(requiredQty: 10, transferredQty: 7, requestedQty: 3);
    }

    [Fact]
    public void StockEntryManager_TransferQty_ReturnBypass()
    {
        var manager = new StockEntryManager(null!, null!);
        // Returns bypass the limit entirely
        manager.ValidateTransferQty(requiredQty: 10, transferredQty: 10, requestedQty: 20, isReturn: true);
    }

    [Fact]
    public void StockEntryManager_TransferQty_MaterialTransferredModeBypass()
    {
        var manager = new StockEntryManager(null!, null!);
        // "Material Transferred" backflush mode bypasses the limit
        manager.ValidateTransferQty(requiredQty: 10, transferredQty: 10, requestedQty: 20,
            isMaterialTransferredMode: true);
    }

    [Fact]
    public void StockEntryManager_TransferQty_NegativeAllowed_ClampedToZero()
    {
        var manager = new StockEntryManager(null!, null!);
        // Over-transferred: allowed = MAX(0, 10 - 15) = 0, requesting 1 → throws
        Assert.Throws<Volo.Abp.BusinessException>(() =>
            manager.ValidateTransferQty(requiredQty: 10, transferredQty: 15, requestedQty: 1));
    }

    // === MaterialRequestManager ===

    [Fact]
    public void MaterialRequestManager_ValidateForSubmission_EmptyItems_Throws()
    {
        var manager = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        Assert.Throws<Volo.Abp.BusinessException>(() => manager.ValidateForSubmission(mr));
    }

    [Fact]
    public void MaterialRequestManager_ValidateForSubmission_WithItems_Succeeds()
    {
        var manager = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Test Item", 10, "Unit", null);
        manager.ValidateForSubmission(mr); // No exception
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_AllOrdered()
    {
        var manager = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-002",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Item A", 10, "Unit", null);
        mr.Items.First().OrderedQuantity = 10;
        Assert.True(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_PartiallyOrdered()
    {
        var manager = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-003",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Item A", 10, "Unit", null);
        mr.Items.First().OrderedQuantity = 5;
        Assert.False(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MaterialRequestManager_IsFullyFulfilled_ThresholdRounding()
    {
        var manager = new MaterialRequestManager(null!);
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-004",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Item A", 10, "Unit", null);
        // 99.99% threshold: 9.999/10 = 99.99% → treated as fulfilled
        mr.Items.First().OrderedQuantity = 9.999m;
        Assert.True(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MaterialRequestManager_GetPendingQty_Calculation()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-005",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Item A", 100, "Unit", null);
        var item = mr.Items.First();
        item.OrderedQuantity = 30;

        Assert.Equal(70m, MaterialRequestManager.GetPendingQty(item));
    }

    [Fact]
    public void MaterialRequestManager_GetPendingQty_NeverNegative()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-006",
            MaterialRequestType.Purchase, DateTime.UtcNow, null);
        mr.AddItem(Guid.NewGuid(), "Item A", 10, "Unit", null);
        var item = mr.Items.First();
        item.OrderedQuantity = 15; // Over-ordered

        Assert.Equal(0m, MaterialRequestManager.GetPendingQty(item));
    }

    // === PcvClosingResult ===

    [Fact]
    public void PcvClosingResult_PositiveNetPL_MeansNetLoss()
    {
        // Expenses exceed income → positive = net loss
        var result = new PcvClosingResult(
            new List<PcvAccountBalance>
            {
                new(Guid.NewGuid(), null, 5000, 0, 5000),  // Expense: 5000 debit
                new(Guid.NewGuid(), null, 0, 3000, -3000),  // Revenue: 3000 credit
            },
            2000m); // Net loss of 2000

        Assert.Equal(2, result.Balances.Count);
        Assert.True(result.TotalNetPL > 0); // Positive = loss
    }

    [Fact]
    public void PcvClosingResult_NegativeNetPL_MeansNetProfit()
    {
        var result = new PcvClosingResult(
            new List<PcvAccountBalance>
            {
                new(Guid.NewGuid(), null, 2000, 0, 2000),  // Expense
                new(Guid.NewGuid(), null, 0, 5000, -5000),  // Revenue
            },
            -3000m); // Net profit of 3000

        Assert.True(result.TotalNetPL < 0); // Negative = profit
    }

    [Fact]
    public void PcvAccountBalance_NetBalance_Calculation()
    {
        var bal = new PcvAccountBalance(Guid.NewGuid(), null, 1500, 500, 1000);
        Assert.Equal(1000m, bal.NetBalance); // 1500 - 500 = 1000 debit balance
    }

    // === RepostItemValuationArgs ===

    [Fact]
    public void RepostItemValuationArgs_AllFieldsSettable()
    {
        var args = new MyERP.Inventory.BackgroundJobs.RepostItemValuationArgs
        {
            ItemId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(),
            FromDate = new DateTime(2026, 1, 1),
            CompanyId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Reason = "Backdated entry"
        };

        Assert.NotEqual(Guid.Empty, args.ItemId);
        Assert.NotEqual(Guid.Empty, args.WarehouseId);
        Assert.NotEqual(Guid.Empty, args.CompanyId);
        Assert.Equal("Backdated entry", args.Reason);
    }

    // === StockEntry → StockEntryManager Integration ===

    [Fact]
    public void StockEntry_Transfer_BothWarehouses_Required()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.MaterialTransfer, DateTime.UtcNow);

        // Transfer items need both source and target
        entry.AddItem(Guid.NewGuid(), 10, Guid.NewGuid(), Guid.NewGuid(), 100);
        Assert.Single(entry.Items);

        var item = entry.Items.First();
        Assert.True(item.SourceWarehouseId.HasValue);
        Assert.True(item.TargetWarehouseId.HasValue);
    }

    [Fact]
    public void StockEntry_Receipt_TargetOnly()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.MaterialReceipt, DateTime.UtcNow);

        var targetWh = Guid.NewGuid();
        entry.AddItem(Guid.NewGuid(), 10, null, targetWh, 100);

        var item = entry.Items.First();
        Assert.Null(item.SourceWarehouseId);
        Assert.Equal(targetWh, item.TargetWarehouseId);
    }

    [Fact]
    public void StockEntry_Issue_SourceOnly()
    {
        var entry = new StockEntry(Guid.NewGuid(), Guid.NewGuid(),
            StockEntryType.MaterialIssue, DateTime.UtcNow);

        var sourceWh = Guid.NewGuid();
        entry.AddItem(Guid.NewGuid(), 10, sourceWh, null, 100);

        var item = entry.Items.First();
        Assert.Equal(sourceWh, item.SourceWarehouseId);
        Assert.Null(item.TargetWarehouseId);
    }

    // === DeliveryScheduleEntry Entity ===

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_Calculation()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100);
        entry.RecordDelivery(40);

        Assert.Equal(60m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullyDelivered()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 50);
        entry.RecordDelivery(50);

        Assert.True(entry.IsFullyDelivered);
        Assert.Equal(0m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 100);

        entry.RecordDelivery(30);
        Assert.Equal(30m, entry.DeliveredQty);
        Assert.Equal(70m, entry.PendingQty);

        entry.RecordDelivery(70);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_NeverNegative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(30), 10);
        entry.RecordDelivery(15); // Over-delivered

        Assert.Equal(0m, entry.PendingQty); // Never negative
    }
}
