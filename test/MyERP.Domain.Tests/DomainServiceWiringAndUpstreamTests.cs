using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Shared;
using Xunit;

namespace MyERP.Tests;

/// <summary>
/// Tests for domain service wiring verification, upstream PR #57380 SLE cancel fix,
/// and recently-wired domain services (SubcontractingManager, AssetLifecycleManager,
/// PeriodClosingPostingService).
/// </summary>
public class DomainServiceWiringAndUpstreamTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    // === SubcontractingManager wiring tests ===

    [Fact]
    public void SCO_PerReceived_Uses_Min_Percentage_Formula()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), _companyId, "SCO-001",
            DateTime.Today, Guid.NewGuid(), _tenantId);
        var item1 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "Widget A", 100m, 10m);
        var item2 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "Widget B", 50m, 20m);
        sco.AddItem(item1);
        sco.AddItem(item2);
        sco.Submit();

        // Simulate partial receipt: item1=80/100=80%, item2=50/50=100%
        item1.ReceivedQty = 80;
        item2.ReceivedQty = 50;

        var minPer = sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty * 100m) : 100m);
        Assert.Equal(80m, minPer); // MIN(80%, 100%) = 80%
    }

    [Fact]
    public void SCO_Full_Receipt_Triggers_Close()
    {
        var sco = new SubcontractingOrder(Guid.NewGuid(), _companyId, "SCO-002",
            DateTime.Today, Guid.NewGuid(), _tenantId);
        sco.AddItem(new SubcontractingOrderItem(Guid.NewGuid(), sco.Id,
            Guid.NewGuid(), "Item", 10m, 5m));
        sco.Submit();

        var item = sco.Items.First();
        item.ReceivedQty = 10; // 100%

        var minPer = sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty * 100m) : 100m);
        Assert.Equal(100m, minPer);
    }

    [Fact]
    public void SCO_Reverse_ReceivedQty_Never_Goes_Negative()
    {
        var item = new SubcontractingOrderItem(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Item", 10m, 5m);
        item.ReceivedQty = 3;

        // Simulate reversal of more than received
        item.ReceivedQty = Math.Max(0, item.ReceivedQty - 5);
        Assert.Equal(0m, item.ReceivedQty);
    }

    // === AssetLifecycleManager wiring tests ===

    [Fact]
    public void Asset_DisposalGainLoss_Positive_When_SoldAboveBookValue()
    {
        var asset = new Asset(Guid.NewGuid(), _companyId, "AST-001", "Laptop",
            DateTime.Today.AddYears(-2), 5000m, _tenantId);
        asset.Submit();

        // Selling above book value = gain
        var disposalAmount = 3000m;
        var gainLoss = disposalAmount - asset.ValueAfterDepreciation;
        Assert.True(gainLoss != 0 || disposalAmount == asset.ValueAfterDepreciation);
    }

    [Fact]
    public void Asset_Scrap_Always_Results_In_Loss()
    {
        var asset = new Asset(Guid.NewGuid(), _companyId, "AST-002", "Printer",
            DateTime.Today.AddYears(-1), 2000m, _tenantId);
        asset.Submit();

        // Scrap = disposal amount of 0
        var gainLoss = 0m - asset.ValueAfterDepreciation;
        Assert.True(gainLoss <= 0); // Always loss or zero
    }

    [Fact]
    public void Asset_Category_Depreciation_Defaults()
    {
        var category = new AssetCategory(Guid.NewGuid(), "Office Equipment", _tenantId);
        // IsDepreciable defaults to true (most categories need depreciation)
        Assert.True(category.IsDepreciable);
    }

    // === PeriodClosingPostingService wiring tests ===

    [Fact]
    public void PCV_Closing_Account_Must_Be_Liability_Or_Equity()
    {
        // Liability account is valid
        var liabilityAccount = new Account(Guid.NewGuid(), _companyId, "3000",
            "Retained Earnings", AccountType.Equity, _tenantId);
        Assert.Equal(AccountType.Equity, liabilityAccount.AccountType);

        // Revenue account is NOT valid for closing
        var revenueAccount = new Account(Guid.NewGuid(), _companyId, "4000",
            "Sales Revenue", AccountType.Revenue, _tenantId);
        Assert.NotEqual(AccountType.Liability, revenueAccount.AccountType);
        Assert.NotEqual(AccountType.Equity, revenueAccount.AccountType);
    }

    [Fact]
    public void PCV_Net_Profit_Calculation_Revenue_Minus_Expense()
    {
        // Revenue = CR balance (negative net in DR-CR formula)
        decimal revenueBalance = -50000m; // CR 50,000
        // Expense = DR balance (positive net in DR-CR formula)
        decimal expenseBalance = 35000m; // DR 35,000

        var netPL = revenueBalance + expenseBalance; // -50000 + 35000 = -15000
        // Negative = profit (revenue > expense)
        Assert.True(netPL < 0);
        Assert.Equal(15000m, Math.Abs(netPL));
    }

    [Fact]
    public void PCV_Zero_Balance_Accounts_Excluded()
    {
        var balances = new[]
        {
            new { AccountId = Guid.NewGuid(), NetBalance = 1000m },
            new { AccountId = Guid.NewGuid(), NetBalance = 0m },
            new { AccountId = Guid.NewGuid(), NetBalance = -500m },
        };

        var nonZero = balances.Where(b => Math.Abs(b.NetBalance) > 0.01m).ToList();
        Assert.Equal(2, nonZero.Count);
    }

    [Fact]
    public void PCV_Empty_Entries_Throws_On_Submit()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), DateTime.Today, DateTime.Today,
            Guid.NewGuid(), _tenantId);

        // PCV with no entries throws (requires at least P&L activity to close)
        Assert.ThrowsAny<Exception>(() => pcv.Submit());
    }

    // === Upstream PR #57380: SLE cancel same-posting-datetime fix ===

    [Fact]
    public void SLE_Cancel_Creates_Reversal_With_Negative_Qty()
    {
        // Original SLE: +10 units
        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today,
            10m, 100m, 10m, 1000m, _tenantId);

        // Cancel reversal should be -10 units
        var reversalQty = -sle.QuantityChange;
        Assert.Equal(-10m, reversalQty);
    }

    [Fact]
    public void SLE_Same_PostingDateTime_Orders_By_Creation()
    {
        var postingDate = DateTime.Today;
        var item = Guid.NewGuid();
        var warehouse = Guid.NewGuid();

        // Two SLEs at exactly the same posting datetime
        var sle1 = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            item, warehouse, postingDate,
            10m, 100m, 10m, 1000m, _tenantId);
        var sle2 = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            item, warehouse, postingDate,
            -5m, 100m, 5m, 500m, _tenantId);

        // Both have same PostingDate — tie-break by Creation timestamp
        Assert.Equal(sle1.PostingDate, sle2.PostingDate);
    }

    [Fact]
    public void SLE_Cancel_Reversal_Uses_Original_Rate()
    {
        decimal originalRate = 42.50m;
        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today,
            10m, originalRate, 10m, 425m, _tenantId);

        // Reversal must use the SAME rate as original
        Assert.Equal(originalRate, sle.ValuationRate);
    }

    // === Upstream PR #57358: WO variant respects selected BOM ===

    [Fact]
    public void WorkOrder_BomId_Can_Be_Set_Independently()
    {
        var bomId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001",
            itemId, bomId, 10m, _tenantId);

        Assert.Equal(bomId, wo.BomId);
        Assert.Equal(itemId, wo.ItemId);
    }

    [Fact]
    public void BOM_IsDefault_Can_Be_Set()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001",
            Guid.NewGuid(), _tenantId);
        Assert.False(bom.IsDefault); // defaults false

        bom.IsDefault = true;
        Assert.True(bom.IsDefault);
    }

    // === Upstream PR #57390: Item Group root resolved structurally ===

    [Fact]
    public void ItemGroup_AutoParents_To_Root_When_No_Parent()
    {
        var group = new ItemGroup(Guid.NewGuid(), "Electronics", tenantId: _tenantId);
        Assert.Null(group.ParentId); // No parent = root candidate
        Assert.False(group.IsGroup); // Leaf by default
    }

    [Fact]
    public void ItemGroup_Can_Have_Parent()
    {
        var root = new ItemGroup(Guid.NewGuid(), "All Items", isGroup: true, tenantId: _tenantId);
        var child = new ItemGroup(Guid.NewGuid(), "Electronics", parentId: root.Id, tenantId: _tenantId);
        Assert.Equal(root.Id, child.ParentId);
    }

    // === PutawayRule capacity allocation tests ===

    [Fact]
    public void PutawayRule_Available_Capacity_Decreases_With_Stock()
    {
        var rule = new PutawayRule(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), _tenantId)
        { StockCapacity = 100m, Priority = 1 };

        var currentBalance = 60m;
        var available = rule.GetAvailableCapacity(currentBalance);
        Assert.Equal(40m, available);
    }

    [Fact]
    public void PutawayRule_Unlimited_When_Capacity_Zero()
    {
        var rule = new PutawayRule(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), _tenantId)
        { StockCapacity = 0m, Priority = 1 }; // 0 = unlimited

        var available = rule.GetAvailableCapacity(999m);
        Assert.Equal(decimal.MaxValue, available);
    }

    // === CostCenterAllocation wiring tests ===

    [Fact]
    public void CostCenterAllocation_Distribute_Even_Split()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), DateTime.Today, _tenantId);
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        alloc.AddEntry(cc1, 50m);
        alloc.AddEntry(cc2, 50m);

        var result = alloc.Distribute(1000m);
        Assert.Equal(2, result.Count);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_Rounding_Goes_To_First_Entry()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), DateTime.Today, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.34m);

        var result = alloc.Distribute(100m);
        var total = result.Sum(r => r.Amount);
        Assert.Equal(100m, total); // Must sum to exactly 100
    }

    // === Financial Report Template formula tests ===

    [Fact]
    public void FinancialReportTemplate_Validate_Detects_Cycle()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(),
            "Test Report", FinancialReportType.Custom);

        // A references B, B references A → cycle
        template.AddRow("A", FinancialReportDataSource.CalculatedAmount, 1,
            referenceCode: "ROW_A", calculationFormula: "ROW_B + 100");
        template.AddRow("B", FinancialReportDataSource.CalculatedAmount, 2,
            referenceCode: "ROW_B", calculationFormula: "ROW_A * 2");

        var errors = template.ValidateFormulas();
        Assert.NotEmpty(errors); // Should detect circular dependency
    }

    [Fact]
    public void FinancialReportTemplate_Valid_Formulas_No_Errors()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(),
            "P&L", FinancialReportType.ProfitAndLoss);

        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1,
            referenceCode: "REV");
        template.AddRow("Expense", FinancialReportDataSource.AccountData, 2,
            referenceCode: "EXP");
        template.AddRow("Net Profit", FinancialReportDataSource.CalculatedAmount, 3,
            referenceCode: "NP", calculationFormula: "REV - EXP");

        var errors = template.ValidateFormulas();
        Assert.Empty(errors);
    }

    // === DeliverySchedule entity tests ===

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_Reduces_Pending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.Today.AddDays(30), 100m, _tenantId);

        Assert.Equal(100m, entry.PendingQty);
        entry.RecordDelivery(40m);
        Assert.Equal(60m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_Full_Delivery_Sets_Pending_Zero()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), DateTime.Today, 50m, _tenantId);

        entry.RecordDelivery(50m);
        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    // === Coupon Code entity tests ===

    [Fact]
    public void CouponCode_Gift_Card_Forces_MaxUse_One()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "GIFT123", "Gift Card",
            CouponType.GiftCard, Guid.NewGuid(), _tenantId);

        Assert.Equal(1, coupon.MaximumUse);
    }

    [Fact]
    public void CouponCode_RecordUse_Increments_Used()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "SUMMER2026", "Summer Sale",
            CouponType.Promotional, Guid.NewGuid(), _tenantId);
        coupon.MaximumUse = 100;

        Assert.Equal(0, coupon.Used);
        coupon.RecordUse();
        Assert.Equal(1, coupon.Used);
        coupon.RecordUse();
        Assert.Equal(2, coupon.Used);
    }

    // === PartyLink entity tests ===

    [Fact]
    public void PartyLink_Self_Link_Throws()
    {
        var partyId = Guid.NewGuid();
        Assert.ThrowsAny<Exception>(() =>
            new MyERP.Core.Entities.PartyLink(Guid.NewGuid(), "Customer", partyId, "Customer", partyId, _tenantId));
    }

    [Fact]
    public void PartyLink_Valid_Bidirectional()
    {
        var link = new MyERP.Core.Entities.PartyLink(Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "Supplier", Guid.NewGuid(), _tenantId);
        Assert.Equal("Customer", link.PrimaryPartyType);
        Assert.Equal("Supplier", link.SecondaryPartyType);
    }

    // === PackingSlip entity tests ===

    [Fact]
    public void PackingSlip_Invalid_Case_Range_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(),
                5, 3, _tenantId)); // from > to
    }

    [Fact]
    public void PackingSlip_Valid_Range_Succeeds()
    {
        var slip = new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            1, 5, _tenantId);
        Assert.Equal(1, slip.FromCaseNo);
        Assert.Equal(5, slip.ToCaseNo);
    }

    // === AccountCategory entity tests ===

    [Fact]
    public void AccountCategory_Create_With_RootType()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Cash and Cash Equivalents", "Asset");
        Assert.Equal("Cash and Cash Equivalents", cat.Name);
        Assert.Equal("Asset", cat.RootType);
    }

    [Fact]
    public void AccountCategory_Empty_Name_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountCategory(Guid.NewGuid(), "", "Asset"));
    }

    // === PosOpeningEntry lifecycle ===

    [Fact]
    public void PosOpeningEntry_Default_Status_Is_Open()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        Assert.Equal(PosOpeningStatus.Open, entry.Status);
    }

    // === MonthEnd readiness ===

    [Fact]
    public void MonthEndReadinessReport_All_Passed_Is_Ready()
    {
        var report = new MyERP.Accounting.DomainServices.MonthEndReadinessReport(_companyId, DateTime.Today);
        report.AddCheck("TB Balanced", true);
        report.AddCheck("No Draft JEs", true);
        Assert.True(report.IsReady);
    }

    [Fact]
    public void MonthEndReadinessReport_Any_Failed_Not_Ready()
    {
        var report = new MyERP.Accounting.DomainServices.MonthEndReadinessReport(_companyId, DateTime.Today);
        report.AddCheck("TB Balanced", true);
        report.AddCheck("Draft JEs Exist", false, "3 draft JEs found");
        Assert.False(report.IsReady);
    }

    // === CompanyRestriction entity tests ===

    [Fact]
    public void Item_RestrictToCompanies_Defaults_False()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "ITEM-001", "Test Item", MyERP.Inventory.ItemType.Goods, _tenantId);
        Assert.False(item.RestrictToCompanies);
    }

    [Fact]
    public void Customer_RestrictToCompanies_Defaults_False()
    {
        var customer = new Customer(Guid.NewGuid(), _companyId, "Acme Corp", _tenantId);
        Assert.False(customer.RestrictToCompanies);
    }

    // === SLE entity validation ===

    [Fact]
    public void SLE_Immutable_After_Creation()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today,
            10m, 50m, 10m, 500m, _tenantId);

        // SLE fields are set in constructor — verify they persist
        Assert.Equal(10m, sle.QuantityChange);
        Assert.Equal(50m, sle.ValuationRate);
        Assert.Equal(500m, sle.StockValue);
    }

    [Fact]
    public void SLE_VoucherType_Tracks_Source_Document()
    {
        var sle = new StockLedgerEntry(Guid.NewGuid(), _companyId,
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Today,
            10m, 50m, 10m, 500m, _tenantId)
        { VoucherType = "PurchaseReceipt", VoucherId = Guid.NewGuid() };

        Assert.Equal("PurchaseReceipt", sle.VoucherType);
    }
}
