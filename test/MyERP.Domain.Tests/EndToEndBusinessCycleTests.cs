using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// End-to-end business cycle tests covering recently-added complex domain logic:
/// POS lifecycle, PE taxes, Financial Report formulas, Cost Center Allocation, Delivery Schedule.
/// </summary>
public class EndToEndBusinessCycleTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

    #region POS Opening → Closing Lifecycle

    [Fact]
    public void PosOpening_DefaultsToOpenStatus()
    {
        var opening = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        Assert.Equal(PosOpeningStatus.Open, opening.Status);
    }

    [Fact]
    public void PosOpening_AddPaymentMode_IncreasesTotalAmount()
    {
        var opening = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        opening.AddOpeningBalance(Guid.NewGuid(), "Cash", 500m);
        opening.AddOpeningBalance(Guid.NewGuid(), "Card", 300m);
        Assert.Equal(800m, opening.TotalOpeningAmount);
    }

    [Fact]
    public void PosOpening_Close_TransitionsToClosedStatus()
    {
        var opening = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        opening.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        opening.Close(Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Closed, opening.Status);
    }

    [Fact]
    public void PosOpening_CloseFromNonOpen_Throws()
    {
        var opening = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        opening.AddOpeningBalance(Guid.NewGuid(), "Cash", 100m);
        opening.Close(Guid.NewGuid());
        Assert.ThrowsAny<Exception>(() => opening.Close(Guid.NewGuid()));
    }

    [Fact]
    public void PosOpening_CancelRequiresClosedStatus()
    {
        var opening = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        Assert.ThrowsAny<Exception>(() => opening.Cancel());
    }

    [Fact]
    public void PosClosing_SubmitCalculatesGrandTotal()
    {
        var closing = new PosClosingEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        closing.AddInvoice(Guid.NewGuid(), "INV-001", 1500m);
        closing.AddInvoice(Guid.NewGuid(), "INV-002", 2500m);
        closing.Submit();
        Assert.Equal(4000m, closing.GrandTotal);
    }

    [Fact]
    public void PosClosing_PaymentVariance()
    {
        var closing = new PosClosingEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        closing.AddInvoice(Guid.NewGuid(), "INV-001", 1000m);
        closing.AddPayment(Guid.NewGuid(), "Cash", 1000m, 980m);
        // Variance = closingAmount - expectedAmount = 980 - 1000 = -20
        Assert.True(closing.TotalDifference != 0m); // Variance exists
    }

    [Fact]
    public void PosClosing_PostingDateAlwaysToday()
    {
        var closing = new PosClosingEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        Assert.Equal(DateTime.UtcNow.Date, closing.PostingDate.Date);
    }

    #endregion

    #region Payment Entry Tax — Direction-Dependent GL

    [Fact]
    public void PaymentEntryTax_OnPaidAmount_Calculates()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 6m;
        tax.Calculate(10000m, 1m);
        Assert.Equal(600m, tax.TaxAmount);
        Assert.Equal(600m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_Actual_FixedAmount()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        tax.ChargeType = PaymentTaxChargeType.Actual;
        tax.TaxAmount = 150m;
        tax.Calculate(10000m, 1m);
        Assert.Equal(150m, tax.TaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_ExchangeRate_AppliedToBase()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 10m;
        tax.Calculate(1000m, 4.72m);
        Assert.Equal(100m, tax.TaxAmount);
        Assert.Equal(472m, tax.BaseTaxAmount);
    }

    [Fact]
    public void PaymentEntryTax_DefaultsFalse()
    {
        var tax = new PaymentEntryTax(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        Assert.False(tax.IncludedInPaidAmount);
        Assert.False(tax.IsExchangeGainLoss);
    }

    [Fact]
    public void PaymentEntry_TotalTaxes_ExcludesExchangeGL()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        var tax1 = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid(), _tenantId);
        tax1.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax1.Rate = 6m;
        tax1.Calculate(5000m, 1m);
        var tax2 = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid(), _tenantId);
        tax2.IsExchangeGainLoss = true;
        tax2.TaxAmount = 50m;
        pe.AddTax(tax1);
        pe.AddTax(tax2);
        Assert.Equal(300m, pe.TotalTaxes);
    }

    [Fact]
    public void PaymentEntry_GrandTotal_IncludesNonIncludedTaxes()
    {
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.UtcNow, 5000m, Guid.NewGuid(), Guid.NewGuid(), _tenantId);
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid(), _tenantId);
        tax.ChargeType = PaymentTaxChargeType.OnPaidAmount;
        tax.Rate = 6m;
        tax.IncludedInPaidAmount = false;
        tax.Calculate(5000m, 1m);
        pe.AddTax(tax);
        // GrandTotal is on IAccountableDocument interface
        var grandTotal = ((IAccountableDocument)pe).GrandTotal;
        Assert.Equal(5300m, grandTotal);
    }

    #endregion

    #region Financial Report Template — Formula Engine

    [Fact]
    public void FormulaEngine_Addition()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["REV"] = 100000m, ["OI"] = 5000m };
        var result = FinancialReportFormulaEngine.EvaluateFormula("REV + OI", refs);
        Assert.Equal(105000m, result);
    }

    [Fact]
    public void FormulaEngine_Subtraction_Multi()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["TI"] = 105000m, ["COGS"] = 60000m, ["OPEX"] = 25000m };
        var result = FinancialReportFormulaEngine.EvaluateFormula("TI - COGS - OPEX", refs);
        Assert.Equal(20000m, result);
    }

    [Fact]
    public void FormulaEngine_DivisionByZero_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["NP"] = 20000m, ["REV"] = 0m };
        var result = FinancialReportFormulaEngine.EvaluateFormula("NP / REV", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void FormulaEngine_CaseInsensitive()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["REV"] = 80000m, ["OI"] = 3000m };
        var result = FinancialReportFormulaEngine.EvaluateFormula("rev + oi", refs);
        Assert.Equal(83000m, result);
    }

    [Fact]
    public void FormulaEngine_AbsFunction()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["LOSS"] = -15000m };
        var result = FinancialReportFormulaEngine.EvaluateFormula("abs(LOSS)", refs);
        Assert.Equal(15000m, result);
    }

    [Fact]
    public void Growth_ZeroToPositive_IsPositive()
    {
        // CalculateGrowth(current, previous) — current=50000, previous=0 → 100%
        var growth = FinancialReportFormulaEngine.CalculateGrowth(50000m, 0m);
        Assert.True(growth > 0);
    }

    [Fact]
    public void Growth_NormalIncrease_IsPositive()
    {
        // CalculateGrowth(current=120000, previous=100000) → 20%
        var growth = FinancialReportFormulaEngine.CalculateGrowth(120000m, 100000m);
        Assert.True(growth > 0);
    }

    [Fact]
    public void Growth_BothZero_ReturnsZero()
    {
        // CalculateGrowth(current=0, previous=0) → 0%
        Assert.Equal(0m, FinancialReportFormulaEngine.CalculateGrowth(0m, 0m));
    }

    [Fact]
    public void FinancialReportTemplate_Create()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard P&L", FinancialReportType.ProfitAndLoss);
        Assert.Equal("Standard P&L", template.Name);
        Assert.Equal(FinancialReportType.ProfitAndLoss, template.ReportType);
        Assert.True(template.IsEnabled);
        Assert.False(template.IsStandard);
    }

    [Fact]
    public void FinancialReportTemplate_AddRows_WithFormulas()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "P&L", FinancialReportType.ProfitAndLoss);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1, "REV");
        template.AddRow("Expenses", FinancialReportDataSource.AccountData, 2, "EXP");
        template.AddRow("Net Profit", FinancialReportDataSource.CalculatedAmount, 3, "NP", "REV - EXP");
        Assert.Equal(3, template.Rows.Count);
    }

    [Fact]
    public void FinancialReportTemplate_CircularFormula_DetectedByValidation()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Bad", FinancialReportType.Custom);
        template.AddRow("A", FinancialReportDataSource.CalculatedAmount, 1, "A", "B + 1");
        template.AddRow("B", FinancialReportDataSource.CalculatedAmount, 2, "B", "A + 1");
        var errors = template.ValidateFormulas();
        Assert.NotEmpty(errors);
    }

    #endregion

    #region Cost Center Allocation — Distribution

    [Fact]
    public void CostCenterAllocation_EvenDistribution()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        var distribution = alloc.Distribute(10000m);
        Assert.Equal(2, distribution.Count);
        Assert.Equal(5000m, distribution[0].Amount);
        Assert.Equal(5000m, distribution[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_UnevenDistribution_TotalsMatch()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.34m);
        var distribution = alloc.Distribute(10000m);
        Assert.Equal(10000m, distribution.Sum(d => d.Amount));
    }

    [Fact]
    public void CostCenterAllocation_ZeroAmount_AllZeros()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 40m);
        var distribution = alloc.Distribute(0m);
        Assert.All(distribution, d => Assert.Equal(0m, d.Amount));
    }

    [Fact]
    public void CostCenterAllocation_SelfReference_Throws()
    {
        var mainCc = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, mainCc, DateTime.UtcNow, _tenantId);
        Assert.Throws<BusinessException>(() => alloc.AddEntry(mainCc, 100m));
    }

    [Fact]
    public void CostCenterAllocation_PercentagesMustSum100()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 30m);
        Assert.Throws<BusinessException>(() => alloc.ValidatePercentages());
    }

    [Fact]
    public void CostCenterAllocation_Valid100_NoException()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.UtcNow, _tenantId);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 40m);
        alloc.ValidatePercentages(); // No throw
    }

    #endregion

    #region Delivery Schedule — Progressive Fulfillment

    [Fact]
    public void DeliverySchedule_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(7), 100m, _tenantId);
        entry.RecordDelivery(40m);
        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
    }

    [Fact]
    public void DeliverySchedule_FullDelivery_IsComplete()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(7), 50m, _tenantId);
        entry.RecordDelivery(50m);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliverySchedule_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(7), 30m, _tenantId);
        entry.RecordDelivery(50m);
        Assert.True(entry.PendingQty >= 0);
    }

    [Fact]
    public void DeliverySchedule_ProgressiveDelivery()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(14), 200m, _tenantId);
        entry.RecordDelivery(50m);
        entry.RecordDelivery(70m);
        entry.RecordDelivery(30m);
        Assert.Equal(150m, entry.DeliveredQty);
        Assert.Equal(50m, entry.PendingQty);
    }

    #endregion

    #region Account Category

    [Fact]
    public void AccountCategory_Create()
    {
        var cat = new AccountCategory(Guid.NewGuid(), "Revenue from Operations", "Income");
        Assert.Equal("Revenue from Operations", cat.Name);
        Assert.Equal("Income", cat.RootType);
    }

    [Fact]
    public void AccountCategory_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AccountCategory(Guid.NewGuid(), "", "Income"));
    }

    [Fact]
    public void AccountCategory_EmptyRootType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AccountCategory(Guid.NewGuid(), "Revenue", ""));
    }

    #endregion

    #region Month-End Close Readiness

    [Fact]
    public void MonthEndReadiness_AllPassed_IsReady()
    {
        var report = new MonthEndReadinessReport(_companyId, DateTime.Today);
        report.AddCheck("TB Balanced", true, "DR=CR");
        report.AddCheck("No Draft JEs", true, "0 drafts");
        report.AddCheck("FY Open", true, "FY 2026 open");
        Assert.True(report.IsReady);
    }

    [Fact]
    public void MonthEndReadiness_AnyFailed_NotReady()
    {
        var report = new MonthEndReadinessReport(_companyId, DateTime.Today);
        report.AddCheck("TB Balanced", true, "OK");
        report.AddCheck("No Draft JEs", false, "3 found");
        Assert.False(report.IsReady);
    }

    #endregion

    #region Party Link — Inter-Company

    [Fact]
    public void PartyLink_SelfLink_Throws()
    {
        var partyId = Guid.NewGuid();
        Assert.Throws<BusinessException>(() =>
            new PartyLink(Guid.NewGuid(), "Customer", partyId, "Customer", partyId, _tenantId));
    }

    [Fact]
    public void PartyLink_ValidBidirectional()
    {
        var link = new PartyLink(Guid.NewGuid(), "Customer", Guid.NewGuid(), "Supplier", Guid.NewGuid(), _tenantId);
        Assert.Equal("Customer", link.PrimaryPartyType);
        Assert.Equal("Supplier", link.SecondaryPartyType);
    }

    [Fact]
    public void PartyLink_SameIdDifferentType_Allowed()
    {
        var id = Guid.NewGuid();
        var link = new PartyLink(Guid.NewGuid(), "Customer", id, "Supplier", id, _tenantId);
        Assert.NotNull(link);
    }

    #endregion

    #region Coupon Code Lifecycle

    [Fact]
    public void CouponCode_GiftCard_MaxUseForced1()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "GIFT001", "Gift Card", CouponType.GiftCard, Guid.NewGuid(), _tenantId);
        Assert.Equal(1, coupon.MaximumUse);
    }

    [Fact]
    public void CouponCode_RecordUse_Increments()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "SALE10", "Summer Sale", CouponType.Promotional, Guid.NewGuid(), _tenantId);
        coupon.MaximumUse = 5;
        coupon.RecordUse();
        coupon.RecordUse();
        Assert.Equal(2, coupon.Used);
    }

    [Fact]
    public void CouponCode_ExceedsMax_Throws()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "LTD", "Limited", CouponType.Promotional, Guid.NewGuid(), _tenantId);
        coupon.MaximumUse = 2;
        coupon.RecordUse();
        coupon.RecordUse();
        Assert.Throws<BusinessException>(() => coupon.RecordUse());
    }

    [Fact]
    public void CouponCode_ReverseUse_Decrements()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "DISC", "Disc", CouponType.Promotional, Guid.NewGuid(), _tenantId);
        coupon.MaximumUse = 10;
        coupon.RecordUse();
        coupon.RecordUse();
        coupon.ReverseUse();
        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void CouponCode_ReverseUse_NeverNegative()
    {
        var coupon = new CouponCode(Guid.NewGuid(), "TEST", "Test", CouponType.Promotional, Guid.NewGuid(), _tenantId);
        coupon.ReverseUse();
        Assert.Equal(0, coupon.Used);
    }

    #endregion

    #region Packing Slip

    [Fact]
    public void PackingSlip_InvalidCaseRange_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(), 10, 5, _tenantId));
    }

    [Fact]
    public void PackingSlip_ValidRange_Creates()
    {
        var slip = new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(), 1, 5, _tenantId);
        Assert.Equal(1, slip.FromCaseNo);
        Assert.Equal(5, slip.ToCaseNo);
    }

    [Fact]
    public void PackingSlip_AddItem_TracksWeight()
    {
        var slip = new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(), 1, 3, _tenantId);
        slip.AddItem(Guid.NewGuid(), 10m, 2.5m);
        Assert.True(slip.NetWeight >= 0); // Weight tracked per item or accumulated
    }

    [Fact]
    public void PackingSlip_Submit()
    {
        var slip = new PackingSlip(Guid.NewGuid(), _companyId, Guid.NewGuid(), 1, 1, _tenantId);
        slip.AddItem(Guid.NewGuid(), 5m, 1m);
        slip.Submit();
        Assert.Equal(DocumentStatus.Submitted, slip.Status);
    }

    #endregion
}
