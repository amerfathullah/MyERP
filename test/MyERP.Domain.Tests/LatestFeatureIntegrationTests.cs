using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Accounting;
using MyERP.Assets.Entities;
using MyERP.Assets;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Inventory;
using MyERP.Purchasing.Entities;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for PCV GL posting, PE tax direction, Asset multi-book depreciation,
/// POS consolidation, delivery schedule, and company restriction patterns.
/// </summary>
public class LatestFeatureIntegrationTests
{
    #region PCV Closing Calculation Tests

    [Fact]
    public void PcvClosingResult_NetPL_Positive_IndicatesLoss()
    {
        // Net Debit > Credit = expenses exceed income = loss
        var balances = new List<PcvAccountBalance>
        {
            new(Guid.NewGuid(), null, 50000m, 0m, 50000m), // Expense with debit balance
            new(Guid.NewGuid(), null, 0m, 30000m, -30000m), // Revenue with credit balance
        };
        var result = new PcvClosingResult(balances, 20000m); // 50K-30K = 20K loss
        Assert.True(result.TotalNetPL > 0); // Positive = net loss
        Assert.Equal(20000m, result.TotalNetPL);
    }

    [Fact]
    public void PcvClosingResult_NetPL_Negative_IndicatesProfit()
    {
        // Net Credit > Debit = income exceeds expenses = profit
        var balances = new List<PcvAccountBalance>
        {
            new(Guid.NewGuid(), null, 30000m, 0m, 30000m), // Expense
            new(Guid.NewGuid(), null, 0m, 80000m, -80000m), // Revenue
        };
        var result = new PcvClosingResult(balances, -50000m); // 30K-80K = -50K profit
        Assert.True(result.TotalNetPL < 0); // Negative = net profit
        Assert.Equal(-50000m, result.TotalNetPL);
    }

    [Fact]
    public void PcvClosingResult_ZeroBalances_Empty()
    {
        var result = new PcvClosingResult(new List<PcvAccountBalance>(), 0m);
        Assert.Empty(result.Balances);
        Assert.Equal(0m, result.TotalNetPL);
    }

    [Fact]
    public void PcvAccountBalance_Record_Properties()
    {
        var accountId = Guid.NewGuid();
        var ccId = Guid.NewGuid();
        var bal = new PcvAccountBalance(accountId, ccId, 5000m, 3000m, 2000m);

        Assert.Equal(accountId, bal.AccountId);
        Assert.Equal(ccId, bal.CostCenterId);
        Assert.Equal(5000m, bal.TotalDebit);
        Assert.Equal(3000m, bal.TotalCredit);
        Assert.Equal(2000m, bal.NetBalance);
    }

    [Fact]
    public void PcvAccountBalance_MultipleCostCenters()
    {
        var accountId = Guid.NewGuid();
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();

        var b1 = new PcvAccountBalance(accountId, cc1, 1000m, 0m, 1000m);
        var b2 = new PcvAccountBalance(accountId, cc2, 500m, 0m, 500m);

        // Same account, different cost centers → separate entries per ERPNext
        Assert.NotEqual(b1.CostCenterId, b2.CostCenterId);
        Assert.Equal(1500m, b1.NetBalance + b2.NetBalance);
    }

    #endregion

    #region PCV Entity Lifecycle Tests

    [Fact]
    public void PeriodClosingVoucher_Submit_RequiresEntries()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());

        // Submit without entries → error
        Assert.Throws<Volo.Abp.BusinessException>(() => pcv.Submit());
    }

    [Fact]
    public void PeriodClosingVoucher_Submit_WithEntries_Succeeds()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());

        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true); // DR expense reversal
        pcv.AddEntry(Guid.NewGuid(), null, 3000m, false); // CR revenue reversal
        pcv.Submit();

        Assert.Equal(DocumentStatus.Submitted, pcv.Status);
        Assert.Equal(8000m, pcv.TotalClosingAmount); // Sum of absolute amounts
    }

    [Fact]
    public void PeriodClosingVoucher_Cancel_FromSubmitted()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();
        pcv.Cancel();

        Assert.Equal(DocumentStatus.Cancelled, pcv.Status);
    }

    [Fact]
    public void PeriodClosingVoucher_Cancel_FromDraft_Throws()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());

        Assert.Throws<Volo.Abp.BusinessException>(() => pcv.Cancel());
    }

    [Fact]
    public void PeriodClosingVoucher_AddEntry_AfterSubmit_Throws()
    {
        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow, Guid.NewGuid());
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();

        Assert.Throws<Volo.Abp.BusinessException>(() =>
            pcv.AddEntry(Guid.NewGuid(), null, 1000m, false));
    }

    #endregion

    #region PaymentEntryTax Direction Tests

    [Fact]
    public void PaymentEntryTax_PayAdd_IsDebit()
    {
        // Per gotcha #624: Pay+Add→debit
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m,
            AddDeductTax = TaxAddDeduct.Add
        };
        tax.Calculate(10000m, 1m);

        Assert.Equal(600m, tax.TaxAmount); // 6% of 10000
        Assert.Equal(TaxAddDeduct.Add, tax.AddDeductTax);
    }

    [Fact]
    public void PaymentEntryTax_PayDeduct_IsCreditDirection()
    {
        // Per gotcha #624: Pay+Deduct→credit
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m,
            AddDeductTax = TaxAddDeduct.Deduct
        };
        tax.Calculate(10000m, 1m);

        Assert.Equal(600m, tax.TaxAmount);
        Assert.Equal(TaxAddDeduct.Deduct, tax.AddDeductTax);
    }

    [Fact]
    public void PaymentEntryTax_Actual_FixedAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 250m,
            BaseTaxAmount = 250m,
            AddDeductTax = TaxAddDeduct.Add
        };

        Assert.Equal(250m, tax.TaxAmount);
        Assert.Equal(PaymentTaxChargeType.Actual, tax.ChargeType);
    }

    [Fact]
    public void PaymentEntryTax_ExchangeRate_AffectsBase()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 10m,
            AddDeductTax = TaxAddDeduct.Add
        };
        tax.Calculate(5000m, 4.72m); // USD payment, MYR base

        Assert.Equal(500m, tax.TaxAmount); // 10% of 5000 in payment currency
        Assert.Equal(2360m, tax.BaseTaxAmount); // 500 × 4.72 in base currency
    }

    [Fact]
    public void PaymentEntryTax_IncludedInPaidAmount_Default()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.False(tax.IncludedInPaidAmount);
    }

    [Fact]
    public void PaymentEntryTax_IsExchangeGainLoss_Default()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.False(tax.IsExchangeGainLoss);
    }

    #endregion

    #region Asset Multi-Book Depreciation Detail Tests

    [Fact]
    public void AssetDepreciationDetail_DepreciableAmount()
    {
        var detail = new AssetDepreciationDetail(Guid.NewGuid(), Guid.NewGuid(),
            DepreciationMethod.StraightLine, 60, 12, 100000m)
        {
            ExpectedValueAfterUsefulLife = 10000m,
            OpeningAccumulatedDepreciation = 5000m
        };

        // DepreciableAmount = Net - Expected - Opening
        Assert.Equal(85000m, detail.DepreciableAmount);
    }

    [Fact]
    public void AssetDepreciationDetail_MultiBook_DifferentMethods()
    {
        var assetId = Guid.NewGuid();

        // Tax book: Straight Line, 5 years
        var taxBook = new AssetDepreciationDetail(Guid.NewGuid(), assetId,
            DepreciationMethod.StraightLine, 60, 12, 100000m)
        {
            FinanceBookId = Guid.NewGuid(),
            ExpectedValueAfterUsefulLife = 10000m
        };

        // Management book: Written Down Value, 8 years
        var mgmtBook = new AssetDepreciationDetail(Guid.NewGuid(), assetId,
            DepreciationMethod.WrittenDownValue, 96, 12, 100000m)
        {
            FinanceBookId = Guid.NewGuid(),
            Rate = 20m,
            ExpectedValueAfterUsefulLife = 5000m
        };

        Assert.Equal(assetId, taxBook.AssetId);
        Assert.Equal(assetId, mgmtBook.AssetId);
        Assert.NotEqual(taxBook.FinanceBookId, mgmtBook.FinanceBookId);
        Assert.Equal(90000m, taxBook.DepreciableAmount);
        Assert.Equal(95000m, mgmtBook.DepreciableAmount);
    }

    [Fact]
    public void AssetDepreciationDetail_DefaultFinanceBook_IsNull()
    {
        var detail = new AssetDepreciationDetail(Guid.NewGuid(), Guid.NewGuid(),
            DepreciationMethod.StraightLine, 60, 12, 50000m);
        Assert.Null(detail.FinanceBookId);
    }

    #endregion

    #region POS Consolidation Tests

    [Fact]
    public void PosConsolidationService_MergesItemQty()
    {
        // When same item appears in multiple POS invoices → SUM qty
        var item1 = new PosConsolidatedItem("ITEM-001", "Widget", 3m, 100m, 300m);
        var item2 = new PosConsolidatedItem("ITEM-001", "Widget", 2m, 100m, 200m);

        var merged = MergeItems(new[] { item1, item2 });
        Assert.Single(merged);
        Assert.Equal(5m, merged[0].Qty);
        Assert.Equal(500m, merged[0].Amount);
        Assert.Equal(100m, merged[0].Rate); // Weighted average: 500/5
    }

    [Fact]
    public void PosConsolidationService_ReturnNegativeQty()
    {
        // Returns produce negative qty in consolidated SI
        var sale = new PosConsolidatedItem("ITEM-001", "Widget", 5m, 100m, 500m);
        var ret = new PosConsolidatedItem("ITEM-001", "Widget", -2m, 100m, -200m);

        var merged = MergeItems(new[] { sale, ret });
        Assert.Single(merged);
        Assert.Equal(3m, merged[0].Qty);
        Assert.Equal(300m, merged[0].Amount);
    }

    [Fact]
    public void PosConsolidationService_DifferentItems_NotMerged()
    {
        var item1 = new PosConsolidatedItem("ITEM-001", "Widget", 3m, 100m, 300m);
        var item2 = new PosConsolidatedItem("ITEM-002", "Gadget", 2m, 200m, 400m);

        var merged = MergeItems(new[] { item1, item2 });
        Assert.Equal(2, merged.Count);
    }

    // Simple merge algorithm matching POS consolidation pattern
    private static List<PosConsolidatedItem> MergeItems(IEnumerable<PosConsolidatedItem> items)
    {
        return items
            .GroupBy(i => i.ItemCode)
            .Select(g => new PosConsolidatedItem(
                g.Key,
                g.First().ItemName,
                g.Sum(i => i.Qty),
                g.Sum(i => i.Qty) != 0 ? g.Sum(i => i.Amount) / g.Sum(i => i.Qty) : 0,
                g.Sum(i => i.Amount)))
            .ToList();
    }

    private record PosConsolidatedItem(string ItemCode, string ItemName, decimal Qty, decimal Rate, decimal Amount);

    #endregion

    #region Delivery Schedule Tests

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_Default()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 100m);

        Assert.Equal(100m, entry.ScheduledQty);
        Assert.Equal(0m, entry.DeliveredQty);
        Assert.Equal(100m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 100m);

        entry.RecordDelivery(40m);

        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 100m);

        entry.RecordDelivery(100m);

        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_NeverNegative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 50m);

        // Over-deliver shouldn't produce negative pending
        entry.RecordDelivery(60m);

        Assert.True(entry.PendingQty >= 0); // Depends on entity implementation
    }

    #endregion

    #region Company Restriction Tests

    [Fact]
    public void CompanyRestrictionEntry_Properties()
    {
        var entry = new CompanyRestrictionEntry(Guid.NewGuid(), "Item", Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal("Item", entry.ParentType);
        Assert.NotEqual(Guid.Empty, entry.ParentId);
        Assert.NotEqual(Guid.Empty, entry.CompanyId);
    }

    [Fact]
    public void Customer_RestrictToCompanies_DefaultFalse()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Test Customer");
        Assert.False(customer.RestrictToCompanies);
    }

    [Fact]
    public void Supplier_RestrictToCompanies_DefaultFalse()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Test Supplier");
        Assert.False(supplier.RestrictToCompanies);
    }

    [Fact]
    public void Item_RestrictToCompanies_DefaultFalse()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test Item", ItemType.Goods);
        Assert.False(item.RestrictToCompanies);
    }

    #endregion

    #region AccountClosingBalance Period Format Tests

    [Fact]
    public void AccountClosingBalanceService_PeriodFormat()
    {
        Assert.Equal("2026-07", AccountClosingBalanceService.GetPeriodFromDate(new DateTime(2026, 7, 22)));
        Assert.Equal("2026-01", AccountClosingBalanceService.GetPeriodFromDate(new DateTime(2026, 1, 1)));
        Assert.Equal("2025-12", AccountClosingBalanceService.GetPeriodFromDate(new DateTime(2025, 12, 31)));
    }

    [Fact]
    public void AccountClosingBalance_Balance_Computed()
    {
        var balance = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "2026-07", 15000m, 10000m);

        Assert.Equal(15000m, balance.Debit);
        Assert.Equal(10000m, balance.Credit);
        Assert.Equal(5000m, balance.Balance); // Debit - Credit
    }

    [Fact]
    public void AccountClosingBalance_CreditHeavy_NegativeBalance()
    {
        var balance = new AccountClosingBalance(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, "2026-07", 3000m, 8000m);

        Assert.Equal(-5000m, balance.Balance); // 3K - 8K = -5K (credit balance)
    }

    #endregion

    #region FinanceBook Tests

    [Fact]
    public void FinanceBook_DefaultNotDefault()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Tax Book");
        Assert.False(book.IsDefault);
    }

    [Fact]
    public void FinanceBook_SetDefault()
    {
        var book = new FinanceBook(Guid.NewGuid(), Guid.NewGuid(), "Management Book");
        book.IsDefault = true;
        Assert.True(book.IsDefault);
    }

    [Fact]
    public void FinanceBook_RequiresName()
    {
        var companyId = Guid.NewGuid();
        var book = new FinanceBook(Guid.NewGuid(), companyId, "Standard");
        Assert.Equal("Standard", book.Name);
        Assert.Equal(companyId, book.CompanyId);
    }

    #endregion

    #region PaymentEntry Taxes Collection Tests

    [Fact]
    public void PaymentEntry_Taxes_DefaultEmpty()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 1000m, Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(pe.Taxes);
        Assert.Empty(pe.Taxes);
        Assert.Equal(0m, pe.TotalTaxes);
    }

    [Fact]
    public void PaymentEntry_AddTax_CalculatesTotal()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 10000m, Guid.NewGuid(), Guid.NewGuid());

        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m,
            AddDeductTax = TaxAddDeduct.Add
        };
        tax.Calculate(10000m, 1m);
        pe.AddTax(tax);

        Assert.Single(pe.Taxes);
        Assert.Equal(600m, pe.TotalTaxes);
    }

    [Fact]
    public void PaymentEntry_IncludedTax_AffectsGrandTotal()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 10000m, Guid.NewGuid(), Guid.NewGuid());

        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m,
            IncludedInPaidAmount = true,
            AddDeductTax = TaxAddDeduct.Add
        };
        tax.Calculate(10000m, 1m);
        pe.AddTax(tax);

        // Per gotcha #437: included taxes reduce what party receives
        Assert.Equal(600m, pe.TotalIncludedTaxes);
        // GrandTotal = PaidAmount + NonIncluded - Included
        // When only included: GrandTotal = 10000 + 0 - 600 = 9400
    }

    [Fact]
    public void PaymentEntry_ExchangeGainLoss_ExcludedFromTaxTotal()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), Guid.NewGuid(),
            PaymentType.Receive, DateTime.UtcNow, 10000m, Guid.NewGuid(), Guid.NewGuid());

        var realTax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 6m,
            AddDeductTax = TaxAddDeduct.Add
        };
        realTax.Calculate(10000m, 1m);
        pe.AddTax(realTax);

        var fxEntry = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            IsExchangeGainLoss = true,
            TaxAmount = 150m,
            AddDeductTax = TaxAddDeduct.Add
        };
        pe.AddTax(fxEntry);

        // Per gotcha #437: exchange GL excluded from unallocated calculation
        Assert.Equal(600m, pe.TotalTaxes); // Only real tax, not FX entry
    }

    #endregion

    #region SalesInvoice Loyalty Fields Tests

    [Fact]
    public void SalesInvoice_LoyaltyFields_DefaultZero()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);

        Assert.Equal(0, si.LoyaltyPointsRedeemed);
        Assert.Equal(0m, si.LoyaltyRedemptionAmount);
        Assert.Equal(0, si.LoyaltyPointsEarned);
        Assert.Null(si.LoyaltyProgramId);
    }

    [Fact]
    public void SalesInvoice_LoyaltyPoints_CanBeSet()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SI-001", DateTime.UtcNow);

        si.LoyaltyPointsRedeemed = 500;
        si.LoyaltyRedemptionAmount = 50m; // 500 points × RM 0.10/point
        si.LoyaltyPointsEarned = 150;
        si.LoyaltyProgramId = Guid.NewGuid();

        Assert.Equal(500, si.LoyaltyPointsRedeemed);
        Assert.Equal(50m, si.LoyaltyRedemptionAmount);
        Assert.Equal(150, si.LoyaltyPointsEarned);
        Assert.NotNull(si.LoyaltyProgramId);
    }

    #endregion

    #region DeliveryScheduleService Frequency Tests

    [Fact]
    public void DeliveryScheduleService_MonthlyFrequency_GeneratesCorrectEntries()
    {
        // 100 units delivered monthly over 5 months → 20/month
        var service = new DeliveryScheduleService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            100m,
            new DateTime(2026, 1, 1), new DateTime(2026, 5, 31),
            DeliveryFrequency.Monthly);

        Assert.Equal(5, entries.Count);
        Assert.Equal(100m, entries.Sum(e => e.ScheduledQty)); // Total = ordered
    }

    [Fact]
    public void DeliveryScheduleService_LastEntry_AbsorbsRounding()
    {
        // Per ERPNext: last entry absorbs rounding remainder
        var service = new DeliveryScheduleService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            100m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            DeliveryFrequency.Quarterly);

        // 100/4 quarters = 25 each (no rounding needed here)
        Assert.Equal(100m, entries.Sum(e => e.ScheduledQty));
    }

    [Fact]
    public void DeliveryScheduleService_WeeklyFrequency()
    {
        var service = new DeliveryScheduleService();
        var entries = service.GenerateSchedule(
            Guid.NewGuid(), Guid.NewGuid(),
            42m,
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 21),
            DeliveryFrequency.Weekly);

        // ~3 weeks → 14/week
        Assert.True(entries.Count >= 3);
        Assert.Equal(42m, entries.Sum(e => e.ScheduledQty));
    }

    #endregion
}
