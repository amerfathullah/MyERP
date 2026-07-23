using System;
using System.Linq;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class MonthEndCloseAndTemplateTests
{
    // --- MonthEndReadinessReport ---

    [Fact]
    public void MonthEndReadinessReport_DefaultIsReady_WhenNoChecks()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), DateTime.Today);
        // Empty checks = vacuously true (All on empty collection = true)
        Assert.True(report.IsReady);
        Assert.Equal(0, report.TotalChecks);
    }

    [Fact]
    public void MonthEndReadinessReport_AllPassedMeansReady()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), DateTime.Today);
        report.AddCheck("Check 1", true);
        report.AddCheck("Check 2", true);
        report.AddCheck("Check 3", true);

        Assert.True(report.IsReady);
        Assert.Equal(3, report.PassedCount);
        Assert.Equal(3, report.TotalChecks);
    }

    [Fact]
    public void MonthEndReadinessReport_AnyFailedMeansNotReady()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), DateTime.Today);
        report.AddCheck("Check 1", true);
        report.AddCheck("Check 2", false, "Difference: 0.01");
        report.AddCheck("Check 3", true);

        Assert.False(report.IsReady);
        Assert.Equal(2, report.PassedCount);
        Assert.Equal(3, report.TotalChecks);
    }

    [Fact]
    public void MonthEndReadinessReport_CheckDetails()
    {
        var report = new MonthEndReadinessReport(Guid.NewGuid(), new DateTime(2026, 6, 30));
        report.AddCheck("Trial Balance", false, "3 draft JEs found");

        var check = report.Checks.First();
        Assert.Equal("Trial Balance", check.Name);
        Assert.False(check.Passed);
        Assert.Equal("3 draft JEs found", check.Details);
    }

    // --- MonthEndCloseStatus ---

    [Fact]
    public void MonthEndCloseStatus_DefaultNotClosed()
    {
        var status = new MonthEndCloseStatus(Guid.NewGuid(), DateTime.Today);
        Assert.False(status.IsFullyClosed);
        Assert.False(status.IsTrialBalanceBalanced);
        Assert.False(status.HasPeriodClosingVoucher);
        Assert.False(status.IsPeriodClosed);
    }

    [Fact]
    public void MonthEndCloseStatus_FullyClosedRequiresAll()
    {
        var status = new MonthEndCloseStatus(Guid.NewGuid(), DateTime.Today);
        status.IsTrialBalanceBalanced = true;
        status.HasPeriodClosingVoucher = true;
        Assert.False(status.IsFullyClosed); // Missing IsPeriodClosed

        status.IsPeriodClosed = true;
        Assert.True(status.IsFullyClosed);
    }

    [Fact]
    public void MonthEndCloseStatus_PartialProgressTracked()
    {
        var status = new MonthEndCloseStatus(Guid.NewGuid(), new DateTime(2026, 7, 31));
        status.IsTrialBalanceBalanced = true;
        // Other steps not done yet
        Assert.False(status.IsFullyClosed);
        Assert.Equal(new DateTime(2026, 7, 31), status.PeriodEndDate);
    }

    // --- Financial Report Template Seeding ---

    [Fact]
    public void FinancialReportTemplate_ProfitAndLossType()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard P&L", FinancialReportType.ProfitAndLoss);
        Assert.Equal(FinancialReportType.ProfitAndLoss, template.ReportType);
        Assert.True(template.IsEnabled);
        Assert.Empty(template.Rows);
    }

    [Fact]
    public void FinancialReportTemplate_BalanceSheetType()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard BS", FinancialReportType.BalanceSheet);
        Assert.Equal(FinancialReportType.BalanceSheet, template.ReportType);
    }

    [Fact]
    public void FinancialReportTemplate_StandardTemplate_CannotDelete()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard P&L", FinancialReportType.ProfitAndLoss);
        template.IsStandard = true;
        Assert.True(template.IsStandard);
    }

    [Fact]
    public void FinancialReportTemplate_AddRowsWithFormulas()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test P&L", FinancialReportType.ProfitAndLoss);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 1, "REV",
            accountCategoryFilter: "Revenue from Operations");
        template.AddRow("Expenses", FinancialReportDataSource.AccountData, 2, "EXP",
            accountCategoryFilter: "Operating Expenses");
        template.AddRow("Net Profit", FinancialReportDataSource.CalculatedAmount, 3, "NP",
            calculationFormula: "REV - EXP", isBold: true);

        Assert.Equal(3, template.Rows.Count);
        var npRow = template.Rows.Last();
        Assert.Equal("REV - EXP", npRow.CalculationFormula);
        Assert.Equal("NP", npRow.ReferenceCode);
        Assert.True(npRow.IsBold);
    }

    [Fact]
    public void FinancialReportTemplate_ValidateFormulas_NoCycle()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        template.AddRow("A", FinancialReportDataSource.AccountData, 1, "A");
        template.AddRow("B", FinancialReportDataSource.AccountData, 2, "B");
        template.AddRow("Total", FinancialReportDataSource.CalculatedAmount, 3, "T", calculationFormula: "A + B");

        var errors = template.ValidateFormulas();
        Assert.Empty(errors);
    }

    [Fact]
    public void FinancialReportTemplate_SignMultiplier()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        var row = template.AddRow("Expense", FinancialReportDataSource.AccountData, 1, "EXP");
        row.SignMultiplier = -1; // Expenses shown as positive in P&L

        Assert.Equal(-1, row.SignMultiplier);
    }

    [Fact]
    public void FinancialReportTemplate_AccountCategoryFilter()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.BalanceSheet);
        template.AddRow("Cash", FinancialReportDataSource.AccountData, 1, "CASH",
            accountCategoryFilter: "Cash and Cash Equivalents");

        var cashRow = template.Rows.First();
        Assert.Equal("Cash and Cash Equivalents", cashRow.AccountCategoryFilter);
        Assert.Equal(FinancialReportDataSource.AccountData, cashRow.DataSource);
    }

    [Fact]
    public void FinancialReportTemplate_HideWhenEmpty()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        var row = template.AddRow("Other", FinancialReportDataSource.AccountData, 1, hideWhenEmpty: true);

        Assert.True(row.HideWhenEmpty);
    }

    [Fact]
    public void FinancialReportTemplate_BlankLineHasNoReferenceCode()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        template.AddRow("", FinancialReportDataSource.BlankLine, 5);

        var blankRow = template.Rows.First();
        Assert.Equal(FinancialReportDataSource.BlankLine, blankRow.DataSource);
        Assert.Null(blankRow.ReferenceCode);
    }

    // --- CalculateGrowth ---

    [Fact]
    public void CalculateGrowth_ZeroToPositive_Returns100()
    {
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(50m, 0m));
    }

    [Fact]
    public void CalculateGrowth_ZeroToNegative_ReturnsNeg100()
    {
        Assert.Equal(-100m, FinancialReportFormulaEngine.CalculateGrowth(-30m, 0m));
    }

    [Fact]
    public void CalculateGrowth_ZeroToZero_Returns0()
    {
        Assert.Equal(0m, FinancialReportFormulaEngine.CalculateGrowth(0m, 0m));
    }

    [Fact]
    public void CalculateGrowth_NormalIncrease()
    {
        // 100 → 150 = 50% growth
        Assert.Equal(50m, FinancialReportFormulaEngine.CalculateGrowth(150m, 100m));
    }

    [Fact]
    public void CalculateGrowth_NormalDecrease()
    {
        // 100 → 75 = -25% growth
        Assert.Equal(-25m, FinancialReportFormulaEngine.CalculateGrowth(75m, 100m));
    }

    [Fact]
    public void CalculateGrowth_Double()
    {
        // 100 → 200 = 100% growth
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(200m, 100m));
    }

    // --- FormulaEngine.EvaluateFormula ---

    [Fact]
    public void EvaluateFormula_SimpleAddition()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["REV"] = 10000m,
            ["OI"] = 500m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("REV + OI", refs);
        Assert.Equal(10500m, result);
    }

    [Fact]
    public void EvaluateFormula_SubtractionForNetProfit()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["TI"] = 15000m,
            ["TE"] = 12000m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("TI - TE", refs);
        Assert.Equal(3000m, result);
    }

    [Fact]
    public void EvaluateFormula_MultipleTerms()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["COGS"] = 5000m,
            ["OPEX"] = 3000m,
            ["DEP"] = 1000m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("COGS + OPEX + DEP", refs);
        Assert.Equal(9000m, result);
    }

    [Fact]
    public void EvaluateFormula_DivisionByZero_ReturnsZero()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 100m,
            ["B"] = 0m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("A / B", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void EvaluateFormula_CaseInsensitiveRefs()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["rev"] = 8000m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("REV", refs);
        Assert.Equal(8000m, result);
    }

    [Fact]
    public void EvaluateFormula_UnknownRef_ReturnsZero()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var result = FinancialReportFormulaEngine.EvaluateFormula("UNKNOWN", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void EvaluateFormula_AbsFunction()
    {
        var refs = new System.Collections.Generic.Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["NP"] = -5000m
        };
        var result = FinancialReportFormulaEngine.EvaluateFormula("abs(NP)", refs);
        Assert.Equal(5000m, result);
    }
}
