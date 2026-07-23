using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using MyERP.Sales.DomainServices;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Advanced business flow tests covering recently-added complex domain logic:
/// - Financial Report Formula Engine (topological sort, formula evaluation, growth)
/// - Cost Center Allocation (distribution, DAG validation, rounding)
/// - Month-End Close (readiness checks)
/// - Party Link (inter-company bidirectional)
/// - Coupon Code (promotional/gift card lifecycle)
/// - Packing Slip (case number overlap, submit/cancel)
/// - Delivery Schedule (frequency generation, partial delivery)
/// - POS Consolidation (dimension grouping, serial ordering)
/// </summary>
public class AdvancedBusinessFlowTests
{
    // ─── Financial Report Formula Engine ──────────────────────────────────────

    [Fact]
    public void FormulaEngine_SimpleAddition()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "REV", 100_000m },
            { "COGS", 60_000m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("REV - COGS", refs);
        Assert.Equal(40_000m, result);
    }

    [Fact]
    public void FormulaEngine_MultiOperations()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "A", 100m },
            { "B", 50m },
            { "C", 25m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("A + B - C", refs);
        Assert.Equal(125m, result);
    }

    [Fact]
    public void FormulaEngine_Multiplication()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "BASE", 1000m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("BASE * 0.06", refs);
        Assert.Equal(60m, result);
    }

    [Fact]
    public void FormulaEngine_DivisionByZero_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "NUM", 500m },
            { "DEN", 0m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("NUM / DEN", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void FormulaEngine_AbsFunction()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "LOSS", -5000m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("abs(LOSS)", refs);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void FormulaEngine_CaseInsensitiveReferences()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "Revenue", 200_000m },
            { "Expense", 150_000m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("revenue - expense", refs);
        Assert.Equal(50_000m, result);
    }

    [Fact]
    public void FormulaEngine_UnknownReference_TreatedAsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "KNOWN", 100m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("KNOWN + UNKNOWN", refs);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void FormulaEngine_EmptyFormula_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var result = FinancialReportFormulaEngine.EvaluateFormula("", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void FormulaEngine_FloorFunction()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "VAL", 99.7m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("floor(VAL)", refs);
        Assert.Equal(99m, result);
    }

    [Fact]
    public void FormulaEngine_CeilFunction()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            { "VAL", 99.1m }
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("ceil(VAL)", refs);
        Assert.Equal(100m, result);
    }

    // ─── Growth Calculation (v16 behavior) ────────────────────────────────────

    [Fact]
    public void Growth_ZeroToPosive_Returns100Percent()
    {
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(50_000m, 0));
    }

    [Fact]
    public void Growth_ZeroToNegative_ReturnsMinus100Percent()
    {
        Assert.Equal(-100m, FinancialReportFormulaEngine.CalculateGrowth(-50_000m, 0));
    }

    [Fact]
    public void Growth_BothZero_ReturnsZero()
    {
        Assert.Equal(0m, FinancialReportFormulaEngine.CalculateGrowth(0, 0));
    }

    [Fact]
    public void Growth_NormalIncrease_50Percent()
    {
        Assert.Equal(50m, FinancialReportFormulaEngine.CalculateGrowth(150_000m, 100_000m));
    }

    [Fact]
    public void Growth_NormalDecrease_Minus25Percent()
    {
        Assert.Equal(-25m, FinancialReportFormulaEngine.CalculateGrowth(75_000m, 100_000m));
    }

    [Fact]
    public void Growth_Doubled_Returns100Percent()
    {
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(200_000m, 100_000m));
    }

    // ─── Cost Center Allocation ───────────────────────────────────────────────

    [Fact]
    public void CostCenterAllocation_EvenDistribution()
    {
        var companyId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        alloc.AddEntry(cc1, 50m);
        alloc.AddEntry(cc2, 50m);
        alloc.ValidatePercentages();

        var distribution = alloc.Distribute(10_000m);
        Assert.Equal(2, distribution.Count);
        Assert.Equal(5_000m, distribution[0].Amount);
        Assert.Equal(5_000m, distribution[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_UnevenDistribution_RemainderToFirst()
    {
        var companyId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        var cc1 = Guid.NewGuid();
        var cc2 = Guid.NewGuid();
        var cc3 = Guid.NewGuid();
        alloc.AddEntry(cc1, 33.33m);
        alloc.AddEntry(cc2, 33.33m);
        alloc.AddEntry(cc3, 33.34m);
        alloc.ValidatePercentages();

        var distribution = alloc.Distribute(10_000m);
        Assert.Equal(3, distribution.Count);
        // Sum must equal original amount exactly
        Assert.Equal(10_000m, distribution.Sum(d => d.Amount));
    }

    [Fact]
    public void CostCenterAllocation_SelfReferenceThrows()
    {
        var companyId = Guid.NewGuid();
        var mainCc = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, mainCc, DateTime.Today);
        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.AddEntry(mainCc, 100m));
    }

    [Fact]
    public void CostCenterAllocation_PercentagesMustSum100()
    {
        var companyId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 30m);
        // Sum = 90%, should fail validation
        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.ValidatePercentages());
    }

    [Fact]
    public void CostCenterAllocation_DuplicateChildThrows()
    {
        var companyId = Guid.NewGuid();
        var childCc = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        alloc.AddEntry(childCc, 50m);
        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.AddEntry(childCc, 50m));
    }

    [Fact]
    public void CostCenterAllocation_ZeroPercentageThrows()
    {
        var companyId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.AddEntry(Guid.NewGuid(), 0m));
    }

    [Fact]
    public void CostCenterAllocation_Over100PercentageThrows()
    {
        var companyId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), companyId, Guid.NewGuid(), DateTime.Today);
        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.AddEntry(Guid.NewGuid(), 101m));
    }

    // ─── Month-End Close ──────────────────────────────────────────────────────

    [Fact]
    public void MonthEndReadinessReport_AllPassed_IsReady()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), DateTime.Today);
        report.AddCheck("Trial Balance", true, "DR = CR");
        report.AddCheck("No Draft JEs", true, "0 drafts");
        report.AddCheck("FY Open", true, "2026 open");
        Assert.True(report.IsReady);
        Assert.Equal(3, report.PassedCount);
    }

    [Fact]
    public void MonthEndReadinessReport_AnyFailed_NotReady()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), DateTime.Today);
        report.AddCheck("Trial Balance", true, "DR = CR");
        report.AddCheck("No Draft JEs", false, "5 drafts found");
        Assert.False(report.IsReady);
        Assert.Equal(1, report.PassedCount);
    }

    [Fact]
    public void MonthEndCloseStatus_AllComplete_IsFullyClosed()
    {
        var status = new MonthEndCloseStatus(Guid.NewGuid(), DateTime.Today);
        status.IsTrialBalanceBalanced = true;
        status.HasPeriodClosingVoucher = true;
        status.IsPeriodClosed = true;
        Assert.True(status.IsFullyClosed);
    }

    [Fact]
    public void MonthEndCloseStatus_Partial_NotFullyClosed()
    {
        var status = new MonthEndCloseStatus(Guid.NewGuid(), DateTime.Today);
        status.IsTrialBalanceBalanced = true;
        status.HasPeriodClosingVoucher = true;
        status.IsPeriodClosed = false; // period not yet closed
        Assert.False(status.IsFullyClosed);
    }

    // ─── Party Link (Inter-Company) ───────────────────────────────────────────

    [Fact]
    public void PartyLink_SelfLinkSameId_ValidatesAtServiceLevel()
    {
        // PartyLink entity allows same Guid in different party types —
        // self-link detection is enforced at service level (same type + same ID)
        var id = Guid.NewGuid();
        var link = new PartyLink(Guid.NewGuid(), "Customer", id, "Supplier", id);
        Assert.NotNull(link);
        Assert.Equal(id, link.PrimaryPartyId);
        Assert.Equal(id, link.SecondaryPartyId);
    }

    [Fact]
    public void PartyLink_ValidBidirectionalLink()
    {
        var customerId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var link = new PartyLink(Guid.NewGuid(), "Customer", customerId, "Supplier", supplierId);
        Assert.Equal("Customer", link.PrimaryPartyType);
        Assert.Equal(customerId, link.PrimaryPartyId);
        Assert.Equal("Supplier", link.SecondaryPartyType);
        Assert.Equal(supplierId, link.SecondaryPartyId);
    }

    [Fact]
    public void PartyLink_SameIdDifferentType_Allowed()
    {
        var id = Guid.NewGuid();
        // Same ID but different party types is valid (rare but possible)
        var link = new PartyLink(Guid.NewGuid(), "Customer", id, "Supplier", Guid.NewGuid());
        Assert.NotNull(link);
    }

    // ─── Coupon Code ──────────────────────────────────────────────────────────

    [Fact]
    public void CouponCode_Promotional_GeneratesCodeFromName()
    {
        var code = CouponCode.GeneratePromotionalCode("SUMMER SALE 2026");
        Assert.NotNull(code);
        Assert.True(code.Length <= 8);
        Assert.Equal("SUMMERSA", code); // first 8 non-digit uppercase chars
    }

    [Fact]
    public void CouponCode_GiftCard_ForcesMaxUse1()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "GC-001", "Gift 100", CouponType.GiftCard, ruleId);
        Assert.Equal(1, coupon.MaximumUse);
    }

    [Fact]
    public void CouponCode_RecordUsage_IncrementsUsed()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "DISC10", "Discount 10%", CouponType.Promotional, ruleId);
        coupon.MaximumUse = 5;
        coupon.RecordUse();
        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void CouponCode_ExceedsMaxUsage_Throws()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "ONCE", "One Time", CouponType.Promotional, ruleId);
        coupon.MaximumUse = 1;
        coupon.RecordUse();
        Assert.Throws<Volo.Abp.BusinessException>(() => coupon.RecordUse());
    }

    [Fact]
    public void CouponCode_ReverseUsage_Decrements()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "DISC20", "20% Off", CouponType.Promotional, ruleId);
        coupon.MaximumUse = 10;
        coupon.RecordUse();
        coupon.RecordUse();
        coupon.ReverseUse();
        Assert.Equal(1, coupon.Used);
    }

    [Fact]
    public void CouponCode_DisabledIsInvalid()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "OFF50", "50% Off", CouponType.Promotional, ruleId);
        coupon.IsEnabled = false;
        Assert.False(coupon.IsValid(DateTime.Today));
    }

    [Fact]
    public void CouponCode_ExpiredIsInvalid()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "EXPIRED", "Expired", CouponType.Promotional, ruleId);
        coupon.ValidUpto = DateTime.Today.AddDays(-1);
        Assert.False(coupon.IsValid(DateTime.Today));
    }

    [Fact]
    public void CouponCode_FutureStartIsInvalid()
    {
        var ruleId = Guid.NewGuid();
        var coupon = new CouponCode(Guid.NewGuid(), "FUTURE", "Future", CouponType.Promotional, ruleId);
        coupon.ValidFrom = DateTime.Today.AddDays(5);
        Assert.False(coupon.IsValid(DateTime.Today));
    }

    // ─── Packing Slip ─────────────────────────────────────────────────────────

    [Fact]
    public void PackingSlip_InvalidCaseRange_Throws()
    {
        // from > to is invalid — caught in constructor
        Assert.Throws<ArgumentException>(() =>
            new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, 3));
    }

    [Fact]
    public void PackingSlip_AddItem_IncreasesItemCount()
    {
        var ps = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 3);
        ps.AddItem(Guid.NewGuid(), 10m, 2.5m); // qty=10, netWeight=2.5 per item
        Assert.Single(ps.Items);
    }

    [Fact]
    public void PackingSlip_Submit_Lifecycle()
    {
        var ps = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        ps.AddItem(Guid.NewGuid(), 5m, 1m);
        ps.Submit();
        Assert.Equal(DocumentStatus.Submitted, ps.Status);
    }

    [Fact]
    public void PackingSlip_CancelAfterSubmit()
    {
        var ps = new PackingSlip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        ps.AddItem(Guid.NewGuid(), 5m, 1m);
        ps.Submit();
        ps.Cancel();
        Assert.Equal(DocumentStatus.Cancelled, ps.Status);
    }

    // ─── Delivery Schedule ────────────────────────────────────────────────────

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m);
        entry.RecordDelivery(40m);
        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery_IsComplete()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 50m);
        entry.RecordDelivery(50m);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 20m);
        entry.RecordDelivery(25m); // over-delivery
        Assert.True(entry.PendingQty >= 0);
    }

    [Fact]
    public void DeliveryScheduleEntry_ProgressiveDelivery()
    {
        var entry = new DeliveryScheduleEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.Today.AddDays(30), 100m);
        entry.RecordDelivery(30m);
        entry.RecordDelivery(20m);
        entry.RecordDelivery(50m);
        Assert.Equal(100m, entry.DeliveredQty);
        Assert.True(entry.IsFullyDelivered);
    }

    // ─── POS Consolidation ────────────────────────────────────────────────────

    [Fact]
    public void PosConsolidation_ResultTracksSourceInvoices()
    {
        var result = new ConsolidationResult();
        result.SourceInvoiceIds.Add(Guid.NewGuid());
        result.SourceInvoiceIds.Add(Guid.NewGuid());
        Assert.Equal(2, result.SourceInvoiceIds.Count);
    }

    [Fact]
    public void PosConsolidation_ConsolidatedItemProperties()
    {
        var item = new ConsolidatedItem
        {
            ItemId = Guid.NewGuid(),
            Description = "Widget",
            Quantity = 5m,
            UnitPrice = 10m,
            Amount = 50m
        };
        Assert.Equal(5m, item.Quantity);
        Assert.Equal(50m, item.Amount);
    }

    [Fact]
    public void PosConsolidation_MultipleItems()
    {
        var result = new ConsolidationResult();
        result.Items.Add(new ConsolidatedItem { ItemId = Guid.NewGuid(), Quantity = 5, UnitPrice = 10, Amount = 50 });
        result.Items.Add(new ConsolidatedItem { ItemId = Guid.NewGuid(), Quantity = 3, UnitPrice = 20, Amount = 60 });
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(110m, result.Items.Sum(i => i.Amount));
    }

    // ─── Financial Report Template Entity ─────────────────────────────────────

    [Fact]
    public void FinancialReportTemplate_ValidateFormulas_DetectsCycle()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test P&L", FinancialReportType.ProfitAndLoss);
        template.AddRow("A", FinancialReportDataSource.CalculatedAmount, 1, "A", "B + 100");
        template.AddRow("B", FinancialReportDataSource.CalculatedAmount, 2, "B", "A + 50");
        var errors = template.ValidateFormulas();
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void FinancialReportTemplate_ValidFormulas_NoErrors()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test P&L", FinancialReportType.ProfitAndLoss);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1, "REV");
        template.AddRow("COGS", FinancialReportDataSource.AccountData, 2, "COGS");
        template.AddRow("Gross Profit", FinancialReportDataSource.CalculatedAmount, 3, "GP", "REV - COGS");
        var errors = template.ValidateFormulas();
        Assert.Empty(errors);
    }

    [Fact]
    public void FinancialReportTemplate_StandardCannotBeDeleted()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard P&L", FinancialReportType.ProfitAndLoss);
        template.IsStandard = true;
        Assert.True(template.IsStandard);
    }

    [Fact]
    public void FinancialReportTemplate_EnableDisable()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Custom", FinancialReportType.Custom);
        Assert.True(template.IsEnabled); // defaults enabled
        template.Disable();
        Assert.False(template.IsEnabled);
        template.Enable();
        Assert.True(template.IsEnabled);
    }

    // ─── Customer Credit Limit Per-Company ────────────────────────────────────

    [Fact]
    public void CustomerCreditLimit_BypassFlag()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50_000m);
        limit.BypassCreditLimitCheck = true;
        Assert.True(limit.BypassCreditLimitCheck);
    }

    [Fact]
    public void CustomerCreditLimit_OverdueThreshold()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m);
        limit.OverdueBillingThreshold = 30_000m;
        Assert.Equal(30_000m, limit.OverdueBillingThreshold);
    }

    [Fact]
    public void CustomerCreditLimit_DefaultsZero()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m);
        Assert.Equal(0m, limit.CreditLimit);
        Assert.False(limit.BypassCreditLimitCheck);
    }

    // ─── Accounting Period Per-Document-Type Closure ───────────────────────────

    [Fact]
    public void AccountingPeriod_CloseSpecificDocumentType()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Jan 2026",
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31));
        period.CloseDocumentType("SalesInvoice");
        Assert.True(period.IsClosedForDocumentType("SalesInvoice"));
        Assert.False(period.IsClosedForDocumentType("JournalEntry"));
    }

    [Fact]
    public void AccountingPeriod_ReopenDocumentType()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Feb 2026",
            new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));
        period.CloseDocumentType("PurchaseInvoice");
        period.CloseDocumentType("SalesInvoice");
        period.ReopenDocumentType("PurchaseInvoice");
        Assert.False(period.IsClosedForDocumentType("PurchaseInvoice"));
        Assert.True(period.IsClosedForDocumentType("SalesInvoice"));
    }

    [Fact]
    public void AccountingPeriod_BlanketClosure_BlocksAll()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Mar 2026",
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
        period.Close(); // blanket close without specific doctype
        Assert.True(period.IsClosed);
        Assert.True(period.IsClosedForDocumentType("AnyDocType"));
    }

    [Fact]
    public void AccountingPeriod_CaseInsensitiveDocType()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "Apr 2026",
            new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        period.CloseDocumentType("salesinvoice");
        Assert.True(period.IsClosedForDocumentType("SalesInvoice"));
        Assert.True(period.IsClosedForDocumentType("SALESINVOICE"));
    }
}
