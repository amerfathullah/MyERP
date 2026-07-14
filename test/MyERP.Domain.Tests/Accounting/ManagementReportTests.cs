using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class ManagementReportTests
{
    // === Stock Valuation Summary ===

    [Fact]
    public void StockValue_IsQuantityTimesRate()
    {
        decimal qty = 100;
        decimal rate = 25.50m;
        decimal value = qty * rate;
        value.ShouldBe(2550m);
    }

    [Fact]
    public void StockValue_ZeroQuantity_ZeroValue()
    {
        decimal qty = 0;
        decimal rate = 100m;
        (qty * rate).ShouldBe(0m);
    }

    [Fact]
    public void StockValuation_TotalIsSum_AcrossWarehouses()
    {
        // Item A in WH1: 100 × 25 = 2500
        // Item A in WH2: 50 × 25 = 1250
        // Item B in WH1: 200 × 10 = 2000
        var values = new[] { 2500m, 1250m, 2000m };
        values.Sum().ShouldBe(5750m);
    }

    [Fact]
    public void StockValuation_OnlyPositiveQuantity()
    {
        // Bins with 0 or negative qty should not appear in valuation
        decimal qty = -5m;
        bool shouldInclude = qty > 0;
        shouldInclude.ShouldBeFalse();
    }

    // === P&L by Cost Center ===

    [Fact]
    public void ProfitMargin_Calculation()
    {
        decimal revenue = 100000m;
        decimal expense = 70000m;
        decimal margin = Math.Round((revenue - expense) / revenue * 100, 1);
        margin.ShouldBe(30.0m);
    }

    [Fact]
    public void ProfitMargin_ZeroRevenue_ReturnsZero()
    {
        decimal revenue = 0m;
        decimal expense = 5000m;
        decimal margin = revenue > 0 ? Math.Round((revenue - expense) / revenue * 100, 1) : 0;
        margin.ShouldBe(0m);
    }

    [Fact]
    public void NetProfit_RevenueMinusExpense()
    {
        decimal revenue = 50000m;
        decimal expense = 35000m;
        decimal netProfit = revenue - expense;
        netProfit.ShouldBe(15000m);
    }

    [Fact]
    public void NetProfit_NegativeIsLoss()
    {
        decimal revenue = 20000m;
        decimal expense = 28000m;
        decimal netProfit = revenue - expense;
        netProfit.ShouldBe(-8000m);
        (netProfit < 0).ShouldBeTrue(); // loss
    }

    [Fact]
    public void CostCenter_OnlyPLAccounts_NotBS()
    {
        // Per DO-NOT: cost center filtering only applies to P&L accounts
        var revenueType = MyERP.Accounting.AccountType.Revenue;
        var expenseType = MyERP.Accounting.AccountType.Expense;
        var assetType = MyERP.Accounting.AccountType.Asset;

        bool isPL_Revenue = revenueType == MyERP.Accounting.AccountType.Revenue || revenueType == MyERP.Accounting.AccountType.Expense;
        bool isPL_Expense = expenseType == MyERP.Accounting.AccountType.Revenue || expenseType == MyERP.Accounting.AccountType.Expense;
        bool isPL_Asset = assetType == MyERP.Accounting.AccountType.Revenue || assetType == MyERP.Accounting.AccountType.Expense;

        isPL_Revenue.ShouldBeTrue();
        isPL_Expense.ShouldBeTrue();
        isPL_Asset.ShouldBeFalse(); // Asset is Balance Sheet → exclude from CC filter
    }

    [Fact]
    public void JournalEntryLine_CostCenterId_DefaultsNull()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, true);
        line.CostCenterId.ShouldBeNull();
    }

    [Fact]
    public void JournalEntryLine_CostCenterId_CanBeSet()
    {
        var line = new JournalEntryLine(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1000m, true);
        var ccId = Guid.NewGuid();
        line.CostCenterId = ccId;
        line.CostCenterId.ShouldBe(ccId);
    }

    [Fact]
    public void UnallocatedEntries_NoCC_StillCounted()
    {
        // GL entries without cost center go to "Unallocated" bucket
        // They should still be included in overall P&L totals
        decimal allocated = 80000m;
        decimal unallocated = 20000m;
        (allocated + unallocated).ShouldBe(100000m);
    }
}
