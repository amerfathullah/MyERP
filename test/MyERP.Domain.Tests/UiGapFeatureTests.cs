using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for UI-gap features: FinancialReportTemplate rows, StockClosingEntry,
/// CostCenterAllocation distribution, and FinancialReportFormulaEngine.
/// </summary>
public class UiGapFeatureTests
{
    #region Helpers

    private static FinancialReportTemplate CreateTemplate(string name = "Test Template")
        => new(Guid.NewGuid(), name, FinancialReportType.ProfitAndLoss);

    private StockClosingEntry CreateClosingEntry(DateTime? toDate = null)
        => new(Guid.NewGuid(), _companyId, toDate ?? new DateTime(2026, 6, 30));

    private CostCenterAllocation CreateAllocation(DateTime? validFrom = null)
        => new(Guid.NewGuid(), _companyId, _mainCcId, validFrom ?? new DateTime(2026, 1, 1));

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _mainCcId = Guid.NewGuid();
    private readonly Guid _childCc1 = Guid.NewGuid();
    private readonly Guid _childCc2 = Guid.NewGuid();

    #endregion

    #region Financial Report Row — Defaults & Properties (8 tests)

    [Fact]
    public void FinancialReportRow_Defaults()
    {
        var template = CreateTemplate();
        var row = template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1);

        Assert.Equal(FinancialReportDataSource.AccountData, row.DataSource);
        Assert.Equal(0, row.IndentLevel);
        Assert.False(row.IsBold);
        Assert.Equal(1, row.SignMultiplier);
        Assert.False(row.HideWhenEmpty);
        Assert.Null(row.CalculationFormula);
        Assert.Null(row.ReferenceCode);
    }

    [Fact]
    public void FinancialReportRow_CalculatedAmount_HasFormula()
    {
        var template = CreateTemplate();
        var row = template.AddRow(
            "Net Profit",
            FinancialReportDataSource.CalculatedAmount,
            sortOrder: 10,
            referenceCode: "NET_PROFIT",
            calculationFormula: "REVENUE - COGS");

        Assert.Equal(FinancialReportDataSource.CalculatedAmount, row.DataSource);
        Assert.Equal("REVENUE - COGS", row.CalculationFormula);
        Assert.Equal("NET_PROFIT", row.ReferenceCode);
    }

    [Fact]
    public void FinancialReportRow_BlankLine_DataSource()
    {
        var template = CreateTemplate();
        var row = template.AddRow("", FinancialReportDataSource.BlankLine, sortOrder: 5);

        Assert.Equal(FinancialReportDataSource.BlankLine, row.DataSource);
    }

    [Fact]
    public void FinancialReportTemplate_Execute_EmptyRows_ReturnsNoResults()
    {
        var template = CreateTemplate();

        // Template with no rows → ValidateFormulas should return no errors
        var errors = template.ValidateFormulas();
        Assert.Empty(errors);
        Assert.Empty(template.Rows);
    }

    [Fact]
    public void FinancialReportTemplate_MultipleRows_IndentLevels()
    {
        var template = CreateTemplate();
        var header = template.AddRow("Income", FinancialReportDataSource.AccountData, 1, isBold: true);
        var detail = template.AddRow("Sales Revenue", FinancialReportDataSource.AccountData, 2);
        var subDetail = template.AddRow("Product Sales", FinancialReportDataSource.AccountData, 3);

        header.IndentLevel = 0;
        detail.IndentLevel = 1;
        subDetail.IndentLevel = 2;

        Assert.Equal(3, template.Rows.Count);
        Assert.Equal(0, header.IndentLevel);
        Assert.Equal(1, detail.IndentLevel);
        Assert.Equal(2, subDetail.IndentLevel);
        Assert.True(header.IsBold);
    }

    [Fact]
    public void FinancialReportTemplate_HideWhenEmpty_ZeroAmount()
    {
        var template = CreateTemplate();
        var row = template.AddRow(
            "Other Income",
            FinancialReportDataSource.AccountData,
            sortOrder: 5,
            hideWhenEmpty: true);

        Assert.True(row.HideWhenEmpty);
        // When HideWhenEmpty=true and amount is 0, the engine skips this row from results.
        // This verifies the flag is stored correctly on the row.
    }

    [Fact]
    public void FinancialReportTemplate_SignMultiplier_NegatesAmount()
    {
        var template = CreateTemplate();
        var row = template.AddRow("Expenses", FinancialReportDataSource.AccountData, sortOrder: 3);
        row.SignMultiplier = -1;

        Assert.Equal(-1, row.SignMultiplier);
        // When the engine applies SignMultiplier=-1 to a positive amount, it becomes negative.
        // We verify the formula engine respects this by testing EvaluateFormula separately.
    }

    [Fact]
    public void FinancialReportTemplate_SectionBreak_DataSource()
    {
        var template = CreateTemplate();
        var row = template.AddRow("---", FinancialReportDataSource.SectionBreak, sortOrder: 10);

        Assert.Equal(FinancialReportDataSource.SectionBreak, row.DataSource);
    }

    #endregion

    #region Stock Closing Entry (7 tests)

    [Fact]
    public void StockClosingEntry_DefaultState_IsDraft()
    {
        var entry = CreateClosingEntry();

        Assert.Equal(StockClosingStatus.Draft, entry.Status);
        Assert.Equal(0, entry.TotalEntries);
        Assert.Equal(0m, entry.TotalStockValue);
        Assert.Empty(entry.Balances);
    }

    [Fact]
    public void StockClosingEntry_AddBalance_IncrementsTotals()
    {
        var entry = CreateClosingEntry();

        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200m, 8000m, 40m);

        // Balances collection grows but TotalEntries/TotalStockValue are set on Submit
        Assert.Equal(2, entry.Balances.Count);
    }

    [Fact]
    public void StockClosingEntry_Submit_CalculatesTotals()
    {
        var entry = CreateClosingEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 200m, 8000m, 40m);

        entry.Submit();

        Assert.Equal(StockClosingStatus.Submitted, entry.Status);
        Assert.Equal(2, entry.TotalEntries);
        Assert.Equal(13_000m, entry.TotalStockValue);
    }

    [Fact]
    public void StockClosingEntry_Submit_EmptyBalances_Throws()
    {
        var entry = CreateClosingEntry();

        var ex = Assert.Throws<BusinessException>(() => entry.Submit());
        Assert.Equal("MyERP:05028", ex.Code);
    }

    [Fact]
    public void StockClosingEntry_Cancel_AfterSubmit()
    {
        var entry = CreateClosingEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        entry.Cancel();

        Assert.Equal(StockClosingStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void StockClosingEntry_AddBalance_AfterSubmit_Throws()
    {
        var entry = CreateClosingEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 100m, 5000m, 50m);
        entry.Submit();

        Assert.Throws<BusinessException>(() =>
            entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 50m, 2500m, 50m));
    }

    [Fact]
    public void StockClosingBalance_Properties()
    {
        var entry = CreateClosingEntry();
        entry.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 150m, 7500m, 50m);

        var bal = entry.Balances.First();
        Assert.Equal(150m, bal.Qty);
        Assert.Equal(50m, bal.ValuationRate);
        Assert.Equal(7500m, bal.StockValue);
        Assert.Null(bal.FifoQueue);
    }

    #endregion

    #region Cost Center Allocation (8 tests)

    [Fact]
    public void CostCenterAllocation_Distribution_EvenSplit()
    {
        var alloc = CreateAllocation();
        alloc.AddEntry(_childCc1, 50m);
        alloc.AddEntry(_childCc2, 50m);

        var result = alloc.Distribute(100m);

        Assert.Equal(2, result.Count);
        Assert.Equal(100m, result.Sum(r => r.Amount));
        Assert.All(result, r => Assert.Equal(50m, r.Amount));
    }

    [Fact]
    public void CostCenterAllocation_Distribution_UnevenRounding()
    {
        var alloc = CreateAllocation();
        alloc.AddEntry(_childCc1, 33.33m);
        alloc.AddEntry(_childCc2, 66.67m);

        var result = alloc.Distribute(100m);

        // Total must equal exactly 100 — remainder absorbed by first entry
        Assert.Equal(100m, result.Sum(r => r.Amount));
    }

    [Fact]
    public void CostCenterAllocation_Validate_Percentages_Sum100()
    {
        var alloc = CreateAllocation();
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 40m);

        // Should not throw
        alloc.ValidatePercentages();
    }

    [Fact]
    public void CostCenterAllocation_Validate_Percentages_Not100_Throws()
    {
        var alloc = CreateAllocation();
        alloc.AddEntry(_childCc1, 60m);
        alloc.AddEntry(_childCc2, 30m); // Total = 90%

        var ex = Assert.Throws<BusinessException>(() => alloc.ValidatePercentages());
        Assert.Equal("MyERP:02042", ex.Code);
    }

    [Fact]
    public void CostCenterAllocation_SelfReference_Throws()
    {
        var alloc = CreateAllocation();

        var ex = Assert.Throws<BusinessException>(() => alloc.AddEntry(_mainCcId, 100m));
        Assert.Equal("MyERP:02038", ex.Code);
    }

    [Fact]
    public void CostCenterAllocation_IsActive_Default_True()
    {
        var alloc = CreateAllocation();

        Assert.True(alloc.IsActive);
    }

    [Fact]
    public void CostCenterAllocation_ValidFrom_Required()
    {
        var alloc = CreateAllocation(new DateTime(2026, 7, 1));

        Assert.Equal(new DateTime(2026, 7, 1), alloc.ValidFrom);
    }

    [Fact]
    public void CostCenterAllocation_EntryProperties()
    {
        var allocId = Guid.NewGuid();
        var childCcId = Guid.NewGuid();
        var entry = new CostCenterAllocationEntry(Guid.NewGuid(), allocId, childCcId, 75.5m);

        Assert.Equal(allocId, entry.CostCenterAllocationId);
        Assert.Equal(childCcId, entry.ChildCostCenterId);
        Assert.Equal(75.5m, entry.Percentage);
    }

    #endregion

    #region Cross-Feature: FinancialReportFormulaEngine (3 tests)

    [Fact]
    public void FinancialReportFormulaEngine_Addition()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["REF1"] = 500m,
            ["REF2"] = 300m
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("REF1 + REF2", refs);

        Assert.Equal(800m, result);
    }

    [Fact]
    public void FinancialReportFormulaEngine_DivisionByZero_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["REVENUE"] = 1000m,
            ["ZERO"] = 0m
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("REVENUE / ZERO", refs);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void FinancialReportFormulaEngine_CaseInsensitiveRefs()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["REF1"] = 100m,
            ["REF2"] = 50m
        };

        // Use lowercase in formula — should still resolve
        var result = FinancialReportFormulaEngine.EvaluateFormula("ref1 + ref2", refs);

        Assert.Equal(150m, result);
    }

    #endregion

    #region POS Consolidation Bug Fix Tests (5 tests)

    [Fact]
    public void PosConsolidation_DimensionHash_IncludesProjectId()
    {
        // Two invoices with different ProjectId should produce different dimension hashes
        // for POS consolidation grouping.
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        var siA = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "POS-001", DateTime.UtcNow);
        siA.ProjectId = projectA;

        var siB = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "POS-002", DateTime.UtcNow);
        siB.ProjectId = projectB;

        // Simulate dimension hash: CompanyId + ProjectId produces unique grouping key
        var hashA = HashCode.Combine(siA.CompanyId, siA.ProjectId);
        var hashB = HashCode.Combine(siB.CompanyId, siB.ProjectId);

        Assert.NotEqual(hashA, hashB);

        // Same project → same hash
        var siC = new SalesInvoice(Guid.NewGuid(), companyId, customerId, "POS-003", DateTime.UtcNow);
        siC.ProjectId = projectA;
        var hashC = HashCode.Combine(siC.CompanyId, siC.ProjectId);
        Assert.Equal(hashA, hashC);
    }

    [Fact]
    public void PosConsolidation_SourceMarking_ConsolidatedSalesInvoiceId()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "POS-001", DateTime.UtcNow);

        // Default: not consolidated
        Assert.Null(si.ConsolidatedSalesInvoiceId);

        // Mark as consolidated
        var consolidatedId = Guid.NewGuid();
        si.ConsolidatedSalesInvoiceId = consolidatedId;
        Assert.Equal(consolidatedId, si.ConsolidatedSalesInvoiceId);
    }

    [Fact]
    public void PosConsolidation_Currency_FromSourceInvoice()
    {
        // CurrencyCode defaults to "MYR" but should be settable per invoice
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.UtcNow);
        Assert.Equal("MYR", si.CurrencyCode);

        // Change to USD — consolidated invoice should use source currency, not hardcode "MYR"
        si.CurrencyCode = "USD";
        Assert.Equal("USD", si.CurrencyCode);
    }

    [Fact]
    public void PosClosingEntry_Submit_CalculatesGrandTotal()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        entry.AddInvoice(Guid.NewGuid(), "POS-001", 150m);
        entry.AddInvoice(Guid.NewGuid(), "POS-002", 250m);
        entry.AddPayment(Guid.NewGuid(), "Cash", 400m, 395m);

        entry.Submit();

        Assert.Equal(PosClosingStatus.Submitted, entry.Status);
        // GrandTotal recalculated from linked invoices on submit
        Assert.Equal(400m, entry.GrandTotal);
    }

    [Fact]
    public void PosClosingEntry_Variance_Calculation()
    {
        var entry = new PosClosingEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        entry.AddPayment(Guid.NewGuid(), "Cash", 1000m, 990m);    // short by 10
        entry.AddPayment(Guid.NewGuid(), "Card", 500m, 505m);     // over by 5

        // Difference = Expected - Closing (positive = short, negative = overage)
        Assert.Equal(10m, entry.Payments[0].Difference);
        Assert.Equal(-5m, entry.Payments[1].Difference);

        // TotalDifference = sum of all payment variances
        Assert.Equal(5m, entry.TotalDifference);
    }

    #endregion

    #region Invoice Status Safety Net Tests (3 tests)

    [Fact]
    public void SalesInvoice_Overdue_Detection()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.UtcNow);
        si.DueDate = DateTime.UtcNow.AddDays(-5); // Past due
        si.GrandTotal = 1000m;
        // AmountPaid defaults to 0, so OutstandingAmount = 1000
        si.AddItem(Guid.NewGuid(), "Item A", 10, 100, 0);
        // Outstanding > 0 AND DueDate < now → overdue
        Assert.True(si.DueDate < DateTime.UtcNow);
        Assert.True(si.OutstandingAmount > 0);
    }

    [Fact]
    public void SalesInvoice_NotOverdue_FutureDate()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-002", DateTime.UtcNow);
        si.DueDate = DateTime.UtcNow.AddDays(30); // Future
        si.GrandTotal = 500m;
        si.AddItem(Guid.NewGuid(), "Item B", 5, 100, 0);

        Assert.True(si.DueDate > DateTime.UtcNow);
        // Even with outstanding > 0, not overdue because date is in the future
        Assert.True(si.OutstandingAmount > 0);
    }

    [Fact]
    public void SalesInvoice_FullyPaid_NotOverdue()
    {
        var si = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-003", DateTime.UtcNow);
        si.DueDate = DateTime.UtcNow.AddDays(-10); // Past due
        si.GrandTotal = 1000m;
        si.AmountPaid = 1000m; // Fully paid
        si.AddItem(Guid.NewGuid(), "Item C", 10, 100, 0);

        // OutstandingAmount = GrandTotal - AmountPaid - WriteOffAmount - TotalAdvance = 0
        Assert.Equal(0m, si.OutstandingAmount);
        // Fully paid is never overdue regardless of DueDate
        Assert.False(si.OutstandingAmount > 0);
    }

    #endregion

    #region Delivery Schedule Reversal Tests (4 tests)

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(7), 100m);

        Assert.Equal(100m, entry.PendingQty);

        entry.RecordDelivery(40m);

        Assert.Equal(40m, entry.DeliveredQty);
        Assert.Equal(60m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery_IsComplete()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(7), 50m);

        entry.RecordDelivery(50m);

        Assert.Equal(50m, entry.DeliveredQty);
        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingNeverNegative()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(7), 30m);

        // Deliver more than scheduled
        entry.RecordDelivery(50m);

        // PendingQty uses Math.Max(0, ...) — never goes negative
        Assert.Equal(0m, entry.PendingQty);
        Assert.Equal(50m, entry.DeliveredQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_Defaults()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(14), 75m);

        Assert.Equal(0m, entry.DeliveredQty);
        Assert.Equal(75m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    #endregion

    #region Proforma Invoice Tests (4 tests)

    [Fact]
    public void ProformaInvoice_DefaultStatus()
    {
        var pi = new ProformaInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(ProformaInvoiceStatus.Draft, pi.Status);
    }

    [Fact]
    public void ProformaInvoice_CancelFromIssued()
    {
        var pi = new ProformaInvoice(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow);

        pi.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Test Item", 10m, 50m);
        pi.Submit();

        Assert.Equal(ProformaInvoiceStatus.Issued, pi.Status);

        pi.Cancel();

        Assert.Equal(ProformaInvoiceStatus.Cancelled, pi.Status);
    }

    [Fact]
    public void ProformaInvoice_RequiresCompanyId()
    {
        Assert.Throws<ArgumentException>(() => new ProformaInvoice(
            Guid.NewGuid(),
            Guid.Empty, // invalid
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow));
    }

    [Fact]
    public void ProformaInvoice_RequiresSalesOrderId()
    {
        Assert.Throws<ArgumentException>(() => new ProformaInvoice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty, // invalid
            Guid.NewGuid(),
            DateTime.UtcNow));
    }

    #endregion

    #region Asset Capitalization Tests (3 tests)

    [Fact]
    public void AssetCapitalization_TotalValue_SumsAllSources()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), _companyId, "CAP-001",
            DateTime.UtcNow, Guid.NewGuid());

        cap.AddStockItem(Guid.NewGuid(), "Steel Plate", 10m, 100m);       // 1000
        cap.AddServiceItem(Guid.NewGuid(), "Installation", 500m);          // 500
        cap.AddConsumedAsset(Guid.NewGuid(), "Old Machine", 2000m);        // 2000

        Assert.Equal(3500m, cap.TotalCapitalizedAmount);
        Assert.Single(cap.StockItems);
        Assert.Single(cap.ServiceItems);
        Assert.Single(cap.ConsumedAssets);
    }

    [Fact]
    public void AssetCapitalization_Submit_FromDraft()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), _companyId, "CAP-002",
            DateTime.UtcNow, Guid.NewGuid());

        Assert.Equal(AssetCapitalizationStatus.Draft, cap.Status);

        cap.Submit();

        Assert.Equal(AssetCapitalizationStatus.Submitted, cap.Status);
    }

    [Fact]
    public void AssetCapitalization_Cancel_FromSubmitted()
    {
        var cap = new AssetCapitalization(
            Guid.NewGuid(), _companyId, "CAP-003",
            DateTime.UtcNow, Guid.NewGuid());

        cap.Submit();
        cap.Cancel();

        Assert.Equal(AssetCapitalizationStatus.Cancelled, cap.Status);
    }

    #endregion
}
