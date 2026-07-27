using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for CC allocation rounding fix, ItemDetailsResolver wiring,
/// PutawayService allocation, and MaterialRequestManager fulfillment tracking.
/// </summary>
public class CcAllocationPutawayMrFulfillmentTests
{
    // ============================================
    // Cost Center Allocation — Rounding Fix Tests
    // ============================================

    [Fact]
    public void CostCenterAllocation_Distribute_Even_Split()
    {
        var allocation = CreateAllocation(
            (Guid.NewGuid(), 50m), // 50%
            (Guid.NewGuid(), 50m)  // 50%
        );

        var result = allocation.Distribute(100m);
        Assert.Equal(2, result.Count);
        Assert.Equal(50m, result[0].Amount);
        Assert.Equal(50m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_Distribute_Uneven_Absorbs_Rounding()
    {
        var allocation = CreateAllocation(
            (Guid.NewGuid(), 33.33m),
            (Guid.NewGuid(), 33.33m),
            (Guid.NewGuid(), 33.34m)
        );

        var result = allocation.Distribute(100m);
        Assert.Equal(3, result.Count);
        // Total must equal original amount
        Assert.Equal(100m, result.Sum(r => r.Amount));
    }

    [Fact]
    public void CostCenterAllocation_Distribute_Remainder_To_First()
    {
        var allocation = CreateAllocation(
            (Guid.NewGuid(), 33.33m),
            (Guid.NewGuid(), 66.67m)
        );

        var result = allocation.Distribute(10m);
        // 33.33% of 10 = 3.333 → 3.3330
        // 66.67% of 10 = 6.667 → 6.6670
        // Sum must equal original amount (first entry absorbs any rounding remainder)
        Assert.Equal(10m, result.Sum(r => r.Amount));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CostCenterAllocation_Validate_Must_Sum_100()
    {
        var allocation = new CostCenterAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        allocation.AddEntry(Guid.NewGuid(), 40m);
        allocation.AddEntry(Guid.NewGuid(), 60m);

        // 40+60=100 → should pass
        allocation.ValidatePercentages();
    }

    [Fact]
    public void CostCenterAllocation_Self_Reference_Throws()
    {
        var mainCc = Guid.NewGuid();
        var allocation = new CostCenterAllocation(
            Guid.NewGuid(), Guid.NewGuid(), mainCc, DateTime.Today);

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            allocation.AddEntry(mainCc, 100m)); // Main CC as child = cycle
    }

    [Fact]
    public void CostCenterAllocation_Zero_Percent_Throws()
    {
        var allocation = new CostCenterAllocation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            allocation.AddEntry(Guid.NewGuid(), 0m));
    }

    // ============================================
    // ItemDetailsResolver — Resolution Tests
    // ============================================

    [Fact]
    public void ResolvedItemDetails_Has_DefaultSupplier_Field()
    {
        var details = new ResolvedItemDetails();
        Assert.Null(details.DefaultSupplierId);
        details.DefaultSupplierId = Guid.NewGuid();
        Assert.NotNull(details.DefaultSupplierId);
    }

    [Fact]
    public void ResolvedItemDetails_Has_DefaultDiscount_Field()
    {
        var details = new ResolvedItemDetails();
        Assert.Equal(0m, details.DefaultDiscountPercentage);
        details.DefaultDiscountPercentage = 10m;
        Assert.Equal(10m, details.DefaultDiscountPercentage);
    }

    [Fact]
    public void ItemResolutionContext_Selling_Defaults()
    {
        var ctx = new ItemResolutionContext
        {
            ItemId = Guid.NewGuid(),
            TransactionType = TransactionType.Selling,
        };
        Assert.Equal(TransactionType.Selling, ctx.TransactionType);
        Assert.Null(ctx.CompanyId);
        Assert.Null(ctx.WarehouseOverride);
    }

    [Fact]
    public void ItemResolutionContext_Buying_With_Company()
    {
        var companyId = Guid.NewGuid();
        var ctx = new ItemResolutionContext
        {
            ItemId = Guid.NewGuid(),
            CompanyId = companyId,
            TransactionType = TransactionType.Buying,
        };
        Assert.Equal(TransactionType.Buying, ctx.TransactionType);
        Assert.Equal(companyId, ctx.CompanyId);
    }

    [Fact]
    public void ItemDefault_Per_Company_Defaults()
    {
        var itemDefault = new ItemDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(itemDefault.DefaultWarehouseId);
        Assert.Null(itemDefault.IncomeAccountId);
        Assert.Null(itemDefault.ExpenseAccountId);
        Assert.Null(itemDefault.DefaultSupplierId);
        Assert.Equal(0m, itemDefault.DefaultDiscountPercentage);
    }

    [Fact]
    public void ItemDefault_Can_Set_All_Fields()
    {
        var itemDefault = new ItemDefault(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        itemDefault.DefaultWarehouseId = Guid.NewGuid();
        itemDefault.IncomeAccountId = Guid.NewGuid();
        itemDefault.ExpenseAccountId = Guid.NewGuid();
        itemDefault.DefaultSupplierId = Guid.NewGuid();
        itemDefault.DefaultDiscountPercentage = 5m;
        itemDefault.BuyingCostCenterId = Guid.NewGuid();
        itemDefault.SellingCostCenterId = Guid.NewGuid();

        Assert.NotNull(itemDefault.DefaultWarehouseId);
        Assert.NotNull(itemDefault.IncomeAccountId);
        Assert.NotNull(itemDefault.ExpenseAccountId);
        Assert.NotNull(itemDefault.DefaultSupplierId);
        Assert.Equal(5m, itemDefault.DefaultDiscountPercentage);
    }

    // ============================================
    // PutawayService — Allocation DTOs
    // ============================================

    [Fact]
    public void PutawayAllocation_Defaults()
    {
        var alloc = new PutawayAllocation();
        Assert.Equal(Guid.Empty, alloc.WarehouseId);
        Assert.Equal(0m, alloc.Qty);
        Assert.False(alloc.IsUnallocated);
    }

    [Fact]
    public void PutawayAllocation_Unallocated_Signal()
    {
        var alloc = new PutawayAllocation
        {
            WarehouseId = Guid.Empty,
            Qty = 10m,
            IsUnallocated = true,
        };
        Assert.True(alloc.IsUnallocated);
        Assert.Equal(10m, alloc.Qty);
    }

    [Fact]
    public void PutawayRule_Available_Capacity()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        rule.StockCapacity = 100;

        Assert.Equal(100m, rule.GetAvailableCapacity(0));
        Assert.Equal(50m, rule.GetAvailableCapacity(50));
        Assert.Equal(0m, rule.GetAvailableCapacity(100));
        // Over-capacity: returns 0 (never negative)
        Assert.Equal(0m, rule.GetAvailableCapacity(150));
    }

    [Fact]
    public void PutawayRule_Unlimited_Capacity()
    {
        var rule = new PutawayRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        rule.StockCapacity = 0; // 0 = unlimited

        Assert.Equal(decimal.MaxValue, rule.GetAvailableCapacity(999));
    }

    // ============================================
    // MaterialRequestManager — Fulfillment Tests
    // ============================================

    [Fact]
    public void MR_IsFullyFulfilled_All_Items_Ordered()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-001", MaterialRequestType.Purchase, DateTime.Today);
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        mr.AddItem(item1Id, "Item 1", 10m, "Unit");
        mr.AddItem(item2Id, "Item 2", 20m, "Unit");

        // Simulate fully ordered
        mr.Items.ElementAt(0).OrderedQuantity = 10m;
        mr.Items.ElementAt(1).OrderedQuantity = 20m;

        var manager = new MaterialRequestManager(null!);
        Assert.True(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MR_IsFullyFulfilled_Partial_Returns_False()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-002", MaterialRequestType.Purchase, DateTime.Today);
        mr.AddItem(Guid.NewGuid(), "Item 1", 10m, "Unit");
        mr.AddItem(Guid.NewGuid(), "Item 2", 20m, "Unit");

        // Only first item ordered
        mr.Items.ElementAt(0).OrderedQuantity = 10m;
        mr.Items.ElementAt(1).OrderedQuantity = 0m;

        var manager = new MaterialRequestManager(null!);
        Assert.False(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MR_IsFullyFulfilled_9999_Threshold()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-003", MaterialRequestType.Purchase, DateTime.Today);
        mr.AddItem(Guid.NewGuid(), "Item 1", 100m, "Unit");

        // 99.99% ordered — should be considered fully fulfilled (float tolerance)
        mr.Items.ElementAt(0).OrderedQuantity = 99.99m;

        var manager = new MaterialRequestManager(null!);
        Assert.True(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MR_IsFullyFulfilled_9998_Returns_False()
    {
        var mr = new MaterialRequest(Guid.NewGuid(), Guid.NewGuid(), "MR-004", MaterialRequestType.Purchase, DateTime.Today);
        mr.AddItem(Guid.NewGuid(), "Item 1", 100m, "Unit");

        // 99.98% — below threshold
        mr.Items.ElementAt(0).OrderedQuantity = 99.98m;

        var manager = new MaterialRequestManager(null!);
        Assert.False(manager.IsFullyFulfilled(mr));
    }

    [Fact]
    public void MR_PendingQty_Calculation()
    {
        var item = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test", 100m, "Unit");
        item.OrderedQuantity = 60m;

        var pending = MaterialRequestManager.GetPendingQty(item);
        Assert.Equal(40m, pending);
    }

    [Fact]
    public void MR_PendingQty_Never_Negative()
    {
        var item = new MaterialRequestItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test", 100m, "Unit");
        item.OrderedQuantity = 150m; // Over-ordered

        var pending = MaterialRequestManager.GetPendingQty(item);
        Assert.Equal(0m, pending);
    }

    // ============================================
    // Accounting Rule Engine — Amount Sources
    // ============================================

    [Fact]
    public void AccountSource_Enum_Values()
    {
        Assert.Equal(0, (int)AccountSource.FixedAccount);
        Assert.Equal(1, (int)AccountSource.CustomerReceivable);
        Assert.Equal(2, (int)AccountSource.SupplierPayable);
        Assert.Equal(3, (int)AccountSource.ItemIncome);
        Assert.Equal(4, (int)AccountSource.ItemExpense);
        Assert.Equal(5, (int)AccountSource.TaxPayable);
    }

    [Fact]
    public void AmountSource_Enum_Values()
    {
        Assert.Equal(0, (int)AmountSource.NetTotal);
        Assert.Equal(1, (int)AmountSource.GrandTotal);
        Assert.Equal(2, (int)AmountSource.TaxAmount);
        Assert.Equal(3, (int)AmountSource.LineAmount);
        Assert.Equal(4, (int)AmountSource.StockCostTotal);
    }

    // ============================================
    // IAccountableDocument — Interface Tests
    // ============================================

    [Fact]
    public void IAccountableDocument_StockCostTotal_Default_Zero()
    {
        // StockCostTotal has a default interface implementation returning 0
        IAccountableDocument doc = new TestAccountableDocument();
        Assert.Equal(0m, doc.StockCostTotal);
    }

    [Fact]
    public void IAccountableDocument_FinanceBook_Default_Null()
    {
        IAccountableDocument doc = new TestAccountableDocument();
        Assert.Null(doc.FinanceBook);
    }

    [Fact]
    public void IAccountableDocument_CostCenterId_Default_Null()
    {
        IAccountableDocument doc = new TestAccountableDocument();
        Assert.Null(doc.CostCenterId);
    }

    // ============================================
    // JournalEntryLine — Amount Setter
    // ============================================

    [Fact]
    public void JournalEntryLine_Amount_Can_Be_Set()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        Assert.Equal(100m, line.Amount);

        line.Amount = 75m;
        Assert.Equal(75m, line.Amount);
    }

    [Fact]
    public void JournalEntryLine_CostCenterId_Can_Be_Assigned()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, true);
        Assert.Null(line.CostCenterId);

        var ccId = Guid.NewGuid();
        line.CostCenterId = ccId;
        Assert.Equal(ccId, line.CostCenterId);
    }

    // ============================================
    // Integration Concepts
    // ============================================

    [Fact]
    public void CcAllocation_Distribution_Preserves_Total_For_3_Way_Split()
    {
        // 3-way split where percentages produce rounding residuals
        var allocation = CreateAllocation(
            (Guid.NewGuid(), 33.33m),
            (Guid.NewGuid(), 33.33m),
            (Guid.NewGuid(), 33.34m)
        );

        // Test with amount that creates rounding issues
        var result = allocation.Distribute(1000.01m);
        var total = result.Sum(r => r.Amount);

        // After rounding fix: total MUST equal original (remainder absorbed by first entry)
        Assert.Equal(1000.01m, total);
    }

    [Fact]
    public void CcAllocation_Empty_Amount_Distributes_Zeros()
    {
        var allocation = CreateAllocation(
            (Guid.NewGuid(), 50m),
            (Guid.NewGuid(), 50m)
        );

        var result = allocation.Distribute(0m);
        Assert.Equal(2, result.Count);
        Assert.Equal(0m, result.Sum(r => r.Amount));
    }

    [Fact]
    public void TransactionType_Enum_Values()
    {
        Assert.Equal(0, (int)TransactionType.Selling);
        Assert.Equal(1, (int)TransactionType.Buying);
    }

    [Fact]
    public void ResolvedItemDetails_All_Fields_Settable()
    {
        var details = new ResolvedItemDetails
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "TEST-001",
            ItemName = "Test Item",
            Description = "A test item",
            IsStockItem = true,
            HasBatchNo = false,
            HasSerialNo = false,
            ItemGroup = "Products",
            Uom = "Dozen",
            StockUom = "Unit",
            ConversionFactor = 12m,
            Rate = 100m,
            WarehouseId = Guid.NewGuid(),
            IncomeAccountId = Guid.NewGuid(),
            ExpenseAccountId = Guid.NewGuid(),
            CostCenterId = Guid.NewGuid(),
            ActualQty = 50m,
            ProjectedQty = 40m,
            ReservedQty = 10m,
            AvailableQty = 40m,
            CompanyTotalStock = 200m,
            LastPurchaseRate = 80m,
            MinOrderQty = 5m,
            DefaultSupplierId = Guid.NewGuid(),
            DefaultDiscountPercentage = 2.5m,
            WeightPerUnit = 0.5m,
            TotalWeight = 25m,
            DefaultBomId = Guid.NewGuid(),
        };

        Assert.Equal("TEST-001", details.ItemCode);
        Assert.Equal("Dozen", details.Uom);
        Assert.Equal(12m, details.ConversionFactor);
        Assert.Equal(2.5m, details.DefaultDiscountPercentage);
    }

    // ============================================
    // Helpers
    // ============================================

    private static CostCenterAllocation CreateAllocation(
        params (Guid CostCenterId, decimal Percentage)[] entries)
    {
        var mainCc = Guid.NewGuid();
        var allocation = new CostCenterAllocation(
            Guid.NewGuid(), Guid.NewGuid(), mainCc, DateTime.Today);

        foreach (var (ccId, pct) in entries)
            allocation.AddEntry(ccId, pct);

        return allocation;
    }

    /// <summary>Test implementation of IAccountableDocument for interface default tests.</summary>
    private class TestAccountableDocument : IAccountableDocument
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string DocumentType => "Test";
        public decimal NetTotal => 100m;
        public decimal GrandTotal => 106m;
        public decimal TaxAmount => 6m;
        public Guid? CustomerId => null;
        public Guid? SupplierId => null;
        public DateTime PostingDate => DateTime.Today;
        public string CurrencyCode => "MYR";
        public decimal ExchangeRate => 1m;
    }
}
