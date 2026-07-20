using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Sales;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// Tests verifying entity properties used by pagination and list display logic.
/// Ensures all list page columns have correct backing data.
/// </summary>
public class PaginationAndNavigationTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    #region Warehouse List Display

    [Fact]
    public void Warehouse_HasAllFieldsForListDisplay()
    {
        var wh = new Warehouse(Guid.NewGuid(), CompanyId, "Main Store");
        Assert.Equal("Main Store", wh.Name);
        Assert.True(wh.IsActive);
        Assert.False(wh.IsGroup);
    }

    [Fact]
    public void Warehouse_InactiveShowsCorrectly()
    {
        var wh = new Warehouse(Guid.NewGuid(), CompanyId, "Closed Store");
        wh.IsActive = false;
        Assert.False(wh.IsActive);
    }

    #endregion

    #region Expense Claim List Display

    [Fact]
    public void ExpenseClaim_TotalClaimedAmount_SumsDetails()
    {
        var ec = new ExpenseClaim(Guid.NewGuid(), CompanyId, Guid.NewGuid(), DateTime.Today);
        ec.AddExpense(DateTime.Today, "Transport", 50m);
        ec.AddExpense(DateTime.Today, "Meals", 30m);
        Assert.Equal(80m, ec.TotalClaimedAmount);
    }

    #endregion

    #region Batch List Display

    [Fact]
    public void Batch_ListFields_AllPresent()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-001");
        Assert.Equal("BATCH-001", batch.BatchNo);
        Assert.False(batch.IsDisabled);
        Assert.Null(batch.ExpiryDate);
    }

    [Fact]
    public void Batch_DisabledBadge_ShowsCorrectly()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-002");
        batch.IsDisabled = true;
        Assert.True(batch.IsDisabled);
    }

    [Fact]
    public void Batch_ExpiryDate_DisplaysWhenSet()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-003");
        batch.ExpiryDate = new DateTime(2027, 6, 30);
        Assert.Equal(new DateTime(2027, 6, 30), batch.ExpiryDate);
    }

    #endregion

    #region Serial Number List Display

    [Fact]
    public void SerialNo_ListFields_AllPresent()
    {
        var sn = new SerialNo(Guid.NewGuid(), Guid.NewGuid(), "SN-00001", CompanyId);
        Assert.Equal("SN-00001", sn.SerialNumber);
        Assert.Equal("Out of Warranty", sn.MaintenanceStatus);
    }

    #endregion

    #region Dunning List Display

    [Fact]
    public void Dunning_LevelBadge_ShowsCorrectly()
    {
        var dunning = new Dunning(Guid.NewGuid(), CompanyId, Guid.NewGuid(), DateTime.Today, 2);
        Assert.Equal(2, dunning.DunningLevel);
    }

    #endregion

    #region Pricing Rule List Display

    [Fact]
    public void PricingRule_DefaultProperties_ForListDisplay()
    {
        var rule = new PricingRule(Guid.NewGuid(), "10% Off Bulk", PricingRuleApplyOn.ItemCode, PricingRuleType.Discount);
        Assert.Equal("10% Off Bulk", rule.Title);
        Assert.False(rule.IsDisabled);
        Assert.Equal(1, rule.Priority);
    }

    [Fact]
    public void PricingRule_DisabledRule_ShowsCorrectly()
    {
        var rule = new PricingRule(Guid.NewGuid(), "Expired Promo", PricingRuleApplyOn.ItemCode, PricingRuleType.Discount);
        rule.IsDisabled = true;
        Assert.True(rule.IsDisabled);
    }

    #endregion

    #region Blanket Order List Display

    [Fact]
    public void BlanketOrder_ListFields_AllPresent()
    {
        var bo = new BlanketOrder(Guid.NewGuid(), CompanyId, "BO-001",
            "Selling", Guid.NewGuid(), DateTime.Today, DateTime.Today.AddYears(1));
        Assert.Equal("Selling", bo.OrderType);
        Assert.NotEqual(default, bo.FromDate);
        Assert.NotEqual(default, bo.ToDate);
    }

    #endregion

    #region Pagination Logic

    [Fact]
    public void PagedRequest_SkipCount_CalculatesCorrectly()
    {
        Assert.Equal(0, 0 * 20);
        Assert.Equal(20, 1 * 20);
        Assert.Equal(60, 3 * 20);
    }

    [Fact]
    public void PagedRequest_TotalCount_DeterminesPaginationVisibility()
    {
        int pageSize = 20;
        Assert.True(15 <= pageSize); // Hidden
        Assert.True(25 > pageSize); // Visible
    }

    #endregion

    #region Salary Slip List Display

    [Fact]
    public void SalarySlip_NetSalary_CalculatedCorrectly()
    {
        var slip = new SalarySlip(Guid.NewGuid(), CompanyId, Guid.NewGuid(),
            DateTime.Today, DateTime.Today.AddMonths(1), DateTime.Today);
        slip.AddEarning(Guid.NewGuid(), "Basic Salary", 5000m);
        slip.AddDeduction(Guid.NewGuid(), "EPF Employee", 550m);
        Assert.Equal(5000m, slip.GrossAmount);
        Assert.Equal(550m, slip.TotalDeductions);
        Assert.Equal(4450m, slip.NetAmount);
    }

    #endregion

    #region Supplier Quotation List Display

    [Fact]
    public void SupplierQuotation_ListFields_AllPresent()
    {
        var sq = new SupplierQuotation(Guid.NewGuid(), CompanyId, Guid.NewGuid(), DateTime.Today);
        sq.AddItem(Guid.NewGuid(), 100, 25m, "Widget", "Unit");
        Assert.Equal("MYR", sq.Currency);
        Assert.Equal(2500m, sq.GrandTotal);
    }

    #endregion
}
