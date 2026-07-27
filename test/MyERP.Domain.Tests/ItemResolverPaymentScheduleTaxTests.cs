using System;
using System.Collections.Generic;
using Xunit;
using MyERP.Inventory.DomainServices;
using MyERP.Accounting.DomainServices;
using MyERP.Tax.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Inventory;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for ItemDetailsResolverService, PaymentScheduleValidationService,
/// TransactionTaxRecalculationService, and Item entity enhancements.
/// </summary>
public class ItemResolverPaymentScheduleTaxTests
{
    // ========================
    // Item Entity — New Fields
    // ========================

    [Fact]
    public void Item_SalesUom_Defaults_Null()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Null(item.SalesUom);
    }

    [Fact]
    public void Item_PurchaseUom_Defaults_Null()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Null(item.PurchaseUom);
    }

    [Fact]
    public void Item_SalesUom_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.SalesUom = "Dozen";
        Assert.Equal("Dozen", item.SalesUom);
    }

    [Fact]
    public void Item_PurchaseUom_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.PurchaseUom = "Box";
        Assert.Equal("Box", item.PurchaseUom);
    }

    [Fact]
    public void Item_WeightPerUnit_Defaults_Zero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Equal(0m, item.WeightPerUnit);
    }

    [Fact]
    public void Item_WeightPerUnit_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.WeightPerUnit = 2.5m;
        Assert.Equal(2.5m, item.WeightPerUnit);
    }

    [Fact]
    public void Item_DefaultBomId_Defaults_Null()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Null(item.DefaultBomId);
    }

    [Fact]
    public void Item_DefaultBomId_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        var bomId = Guid.NewGuid();
        item.DefaultBomId = bomId;
        Assert.Equal(bomId, item.DefaultBomId);
    }

    [Fact]
    public void Item_LeadTimeDays_Defaults_Zero()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Equal(0, item.LeadTimeDays);
    }

    [Fact]
    public void Item_LeadTimeDays_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.LeadTimeDays = 14;
        Assert.Equal(14, item.LeadTimeDays);
    }

    [Fact]
    public void Item_WeightUom_Defaults_Null()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        Assert.Null(item.WeightUom);
    }

    [Fact]
    public void Item_WeightUom_Can_Be_Set()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "TEST-001", "Test Item", ItemType.Goods);
        item.WeightUom = "Kg";
        Assert.Equal("Kg", item.WeightUom);
    }

    // ========================
    // ResolvedItemDetails — DTO
    // ========================

    [Fact]
    public void ResolvedItemDetails_Defaults_Correct()
    {
        var details = new ResolvedItemDetails();
        Assert.Equal("Unit", details.Uom);
        Assert.Equal("Unit", details.StockUom);
        Assert.Equal(1m, details.ConversionFactor);
        Assert.Equal(0m, details.Rate);
        Assert.Equal(0m, details.ActualQty);
        Assert.Equal(0m, details.CompanyTotalStock);
        Assert.Equal(0m, details.DefaultDiscountPercentage);
        Assert.Null(details.WarehouseId);
        Assert.Null(details.IncomeAccountId);
        Assert.Null(details.ExpenseAccountId);
        Assert.Null(details.CostCenterId);
        Assert.Null(details.DefaultBomId);
    }

    [Fact]
    public void ResolvedItemDetails_AllFields_Settable()
    {
        var details = new ResolvedItemDetails
        {
            ItemId = Guid.NewGuid(),
            ItemCode = "ITEM-001",
            ItemName = "Widget",
            Description = "A fine widget",
            Uom = "Dozen",
            StockUom = "Unit",
            ConversionFactor = 12,
            IsStockItem = true,
            HasBatchNo = true,
            HasSerialNo = false,
            ItemGroup = "Products",
            Rate = 120m,
            WarehouseId = Guid.NewGuid(),
            IncomeAccountId = Guid.NewGuid(),
            CostCenterId = Guid.NewGuid(),
            DefaultSupplierId = Guid.NewGuid(),
            DefaultBomId = Guid.NewGuid(),
            DefaultDiscountPercentage = 5m,
            MinOrderQty = 10m,
            WeightPerUnit = 0.5m,
            TotalWeight = 6m,
            LastPurchaseRate = 95m,
            ActualQty = 100,
            ProjectedQty = 80,
            ReservedQty = 20,
            AvailableQty = 80,
            CompanyTotalStock = 500
        };

        Assert.Equal("ITEM-001", details.ItemCode);
        Assert.Equal("Widget", details.ItemName);
        Assert.Equal(12m, details.ConversionFactor);
        Assert.Equal(120m, details.Rate);
        Assert.Equal(500m, details.CompanyTotalStock);
    }

    [Fact]
    public void ItemResolutionContext_Defaults()
    {
        var ctx = new ItemResolutionContext();
        Assert.Equal(TransactionType.Selling, ctx.TransactionType);
        Assert.Null(ctx.CompanyId);
        Assert.Null(ctx.WarehouseOverride);
        Assert.Null(ctx.PartyId);
        Assert.Null(ctx.PriceListId);
        Assert.Null(ctx.TransactionDate);
    }

    [Fact]
    public void TransactionType_Enum_Values()
    {
        Assert.Equal(0, (int)TransactionType.Selling);
        Assert.Equal(1, (int)TransactionType.Buying);
    }

    // ====================================
    // PaymentScheduleValidationService
    // ====================================

    [Fact]
    public void PaymentSchedule_EmptyEntries_IsValid()
    {
        var svc = new PaymentScheduleValidationService();
        var result = svc.Validate(Array.Empty<PaymentScheduleInput>(), 1000m, DateTime.Today);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PaymentSchedule_Net30_Valid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 100m, PaymentAmount = 5000m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PaymentSchedule_SplitPayment_Valid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today, InvoicePortion = 50m, PaymentAmount = 2500m },
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 50m, PaymentAmount = 2500m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PaymentSchedule_PortionsNot100_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 80m, PaymentAmount = 4000m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("100%"));
    }

    [Fact]
    public void PaymentSchedule_AmountMismatch_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 100m, PaymentAmount = 4500m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("grand total"));
    }

    [Fact]
    public void PaymentSchedule_DueBeforePosting_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(-5), InvoicePortion = 100m, PaymentAmount = 5000m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("before posting date"));
    }

    [Fact]
    public void PaymentSchedule_ZeroPortion_Invalid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 0m, PaymentAmount = 0m },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 100m, PaymentAmount = 5000m }
        };
        var result = svc.Validate(entries, 5000m, DateTime.Today);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("positive"));
    }

    [Fact]
    public void PaymentSchedule_ResolveDueDate_ReturnsMax()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 40m, PaymentAmount = 2000m },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 30m, PaymentAmount = 1500m },
            new() { DueDate = DateTime.Today.AddDays(90), InvoicePortion = 30m, PaymentAmount = 1500m }
        };
        var dueDate = svc.ResolveDueDate(entries, DateTime.Today);
        Assert.Equal(DateTime.Today.AddDays(90), dueDate);
    }

    [Fact]
    public void PaymentSchedule_ResolveDueDate_Empty_ReturnsPostingDate()
    {
        var svc = new PaymentScheduleValidationService();
        var dueDate = svc.ResolveDueDate(Array.Empty<PaymentScheduleInput>(), DateTime.Today);
        Assert.Equal(DateTime.Today, dueDate);
    }

    [Fact]
    public void PaymentSchedule_ResolveDueDate_ClampsToPostingDate()
    {
        var svc = new PaymentScheduleValidationService();
        var yesterday = DateTime.Today.AddDays(-1);
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = yesterday, InvoicePortion = 100m, PaymentAmount = 5000m }
        };
        var dueDate = svc.ResolveDueDate(entries, DateTime.Today);
        Assert.Equal(DateTime.Today, dueDate); // floor rule
    }

    [Fact]
    public void PaymentSchedule_Recalculate_EvenSplit()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 50m, PaymentAmount = 2500m },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 50m, PaymentAmount = 2500m }
        };
        var result = svc.RecalculateAmounts(entries, 6000m);
        Assert.Equal(2, result.Count);
        Assert.Equal(3000m, result[0].PaymentAmount);
        Assert.Equal(3000m, result[1].PaymentAmount);
    }

    [Fact]
    public void PaymentSchedule_Recalculate_LastAbsorbsRounding()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 33.33m, PaymentAmount = 1666.50m },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 33.33m, PaymentAmount = 1666.50m },
            new() { DueDate = DateTime.Today.AddDays(90), InvoicePortion = 33.34m, PaymentAmount = 1667.00m }
        };
        var result = svc.RecalculateAmounts(entries, 10000m);
        Assert.Equal(3, result.Count);
        var total = result[0].PaymentAmount + result[1].PaymentAmount + result[2].PaymentAmount;
        Assert.Equal(10000m, total); // No rounding loss
    }

    [Fact]
    public void PaymentSchedule_Recalculate_Empty_ReturnsEmpty()
    {
        var svc = new PaymentScheduleValidationService();
        var result = svc.RecalculateAmounts(Array.Empty<PaymentScheduleInput>(), 5000m);
        Assert.Empty(result);
    }

    [Fact]
    public void PaymentSchedule_ThreePartSplit_Valid()
    {
        var svc = new PaymentScheduleValidationService();
        var entries = new List<PaymentScheduleInput>
        {
            new() { DueDate = DateTime.Today, InvoicePortion = 40m, PaymentAmount = 4000m },
            new() { DueDate = DateTime.Today.AddDays(30), InvoicePortion = 30m, PaymentAmount = 3000m },
            new() { DueDate = DateTime.Today.AddDays(60), InvoicePortion = 30m, PaymentAmount = 3000m }
        };
        var result = svc.Validate(entries, 10000m, DateTime.Today);
        Assert.True(result.IsValid);
    }

    // ====================================
    // TransactionTaxRecalculationService
    // ====================================

    [Fact]
    public void TaxRecalculation_NetOnly_NoTaxRows()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 10, UnitPrice = 100 },
            new() { ItemId = Guid.NewGuid(), Quantity = 5, UnitPrice = 200 }
        };
        var result = TransactionTaxRecalculationService.CalculateNetOnly(items, 0m, 1m);
        Assert.Equal(2000m, result.NetTotal);  // 10×100 + 5×200
        Assert.Equal(0m, result.TaxAmount);
        Assert.Equal(2000m, result.GrandTotal);
        Assert.Equal(2000m, result.BaseNetTotal);
    }

    [Fact]
    public void TaxRecalculation_NetOnly_WithDiscount()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 10, UnitPrice = 100 }
        };
        var result = TransactionTaxRecalculationService.CalculateNetOnly(items, 50m, 1m);
        Assert.Equal(1000m, result.NetTotal);
        Assert.Equal(950m, result.GrandTotal); // 1000 - 50
    }

    [Fact]
    public void TaxRecalculation_NetOnly_MultiCurrency()
    {
        var items = new List<TaxItemInput>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100 } // 100 USD
        };
        var result = TransactionTaxRecalculationService.CalculateNetOnly(items, 0m, 4.72m); // MYR rate
        Assert.Equal(100m, result.NetTotal);
        Assert.Equal(472m, result.BaseNetTotal);  // 100 × 4.72
        Assert.Equal(472m, result.BaseGrandTotal);
    }

    [Fact]
    public void TaxRecalculation_Input_Defaults()
    {
        var input = new TaxRecalculationInput();
        Assert.Equal(1m, input.ExchangeRate);
        Assert.Equal(0m, input.DiscountAmount);
        Assert.True(input.IsDiscountOnGrandTotal);
        Assert.Empty(input.Items);
    }

    [Fact]
    public void RecalculatedTotals_AllFields()
    {
        var totals = new RecalculatedTotals
        {
            NetTotal = 1000,
            TaxAmount = 60,
            GrandTotal = 1060,
            BaseNetTotal = 4720,
            BaseTaxAmount = 283.20m,
            BaseGrandTotal = 5003.20m
        };
        Assert.Equal(1060m, totals.GrandTotal);
        Assert.Equal(5003.20m, totals.BaseGrandTotal);
    }

    // ====================================
    // ResolvedDefaults — Internal Type
    // ====================================

    [Fact]
    public void ItemDefault_PerCompany_Defaults()
    {
        var def = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(def.DefaultWarehouseId);
        Assert.Null(def.IncomeAccountId);
        Assert.Null(def.ExpenseAccountId);
        Assert.Null(def.BuyingCostCenterId);
        Assert.Null(def.SellingCostCenterId);
        Assert.Null(def.DefaultSupplierId);
        Assert.Equal(0m, def.DefaultDiscountPercentage);
    }

    [Fact]
    public void ItemDefault_AllFields_Settable()
    {
        var def = new ItemDefault(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        def.DefaultWarehouseId = Guid.NewGuid();
        def.IncomeAccountId = Guid.NewGuid();
        def.ExpenseAccountId = Guid.NewGuid();
        def.BuyingCostCenterId = Guid.NewGuid();
        def.SellingCostCenterId = Guid.NewGuid();
        def.DefaultSupplierId = Guid.NewGuid();
        def.DefaultPriceListId = Guid.NewGuid();
        def.DefaultDiscountPercentage = 10m;

        Assert.NotNull(def.DefaultWarehouseId);
        Assert.NotNull(def.IncomeAccountId);
        Assert.NotNull(def.DefaultSupplierId);
        Assert.Equal(10m, def.DefaultDiscountPercentage);
    }

    // ====================================
    // PaymentScheduleValidationResult
    // ====================================

    [Fact]
    public void ValidationResult_Default_NotValid()
    {
        var result = new PaymentScheduleValidationResult();
        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidationResult_WithErrors()
    {
        var result = new PaymentScheduleValidationResult();
        result.Errors.Add("Error 1");
        result.Errors.Add("Error 2");
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void RecalculatedScheduleEntry_Properties()
    {
        var entry = new RecalculatedScheduleEntry
        {
            DueDate = DateTime.Today.AddDays(30),
            InvoicePortion = 50m,
            PaymentAmount = 2500m,
            Description = "50% Advance"
        };
        Assert.Equal(50m, entry.InvoicePortion);
        Assert.Equal(2500m, entry.PaymentAmount);
        Assert.Equal("50% Advance", entry.Description);
    }
}
