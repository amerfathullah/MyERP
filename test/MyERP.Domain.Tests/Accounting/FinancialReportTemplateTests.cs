using System;
using System.Collections.Generic;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class FinancialReportTemplateTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Standard P&L", FinancialReportType.ProfitAndLoss);
        Assert.Equal("Standard P&L", template.Name);
        Assert.Equal(FinancialReportType.ProfitAndLoss, template.ReportType);
        Assert.True(template.IsEnabled);
        Assert.False(template.IsStandard);
        Assert.Empty(template.Rows);
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new FinancialReportTemplate(Guid.NewGuid(), "", FinancialReportType.BalanceSheet));
    }

    [Fact]
    public void AddRow_AddsToCollection()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.Custom);
        var row = template.AddRow("Revenue", FinancialReportDataSource.AccountData, 10, "REVENUE");
        Assert.Single(template.Rows);
        Assert.Equal("Revenue", row.Label);
        Assert.Equal("REVENUE", row.ReferenceCode);
        Assert.Equal(10, row.SortOrder);
    }

    [Fact]
    public void AddRow_FormulaRow()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 10, "REVENUE");
        template.AddRow("COGS", FinancialReportDataSource.AccountData, 20, "COGS");
        template.AddRow("Gross Profit", FinancialReportDataSource.CalculatedAmount, 30,
            "GP", "REVENUE - COGS");
        Assert.Equal(3, template.Rows.Count);
    }

    [Fact]
    public void ValidateFormulas_NoCircular_ReturnsEmpty()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.ProfitAndLoss);
        template.AddRow("Revenue", FinancialReportDataSource.AccountData, 10, "REVENUE");
        template.AddRow("COGS", FinancialReportDataSource.AccountData, 20, "COGS");
        template.AddRow("Gross Profit", FinancialReportDataSource.CalculatedAmount, 30,
            "GP", "REVENUE - COGS");
        template.AddRow("Net Profit", FinancialReportDataSource.CalculatedAmount, 40,
            "NET", "GP - 1000");

        var errors = template.ValidateFormulas();
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFormulas_CircularDependency_ReturnsError()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.Custom);
        // A depends on B, B depends on A → circular
        template.AddRow("Row A", FinancialReportDataSource.CalculatedAmount, 10, "A", "B + 100");
        template.AddRow("Row B", FinancialReportDataSource.CalculatedAmount, 20, "B", "A + 200");

        var errors = template.ValidateFormulas();
        Assert.NotEmpty(errors);
        Assert.Contains("Circular dependency", errors[0]);
    }

    [Fact]
    public void Enable_Disable_Toggles()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.BalanceSheet);
        Assert.True(template.IsEnabled);
        template.Disable();
        Assert.False(template.IsEnabled);
        template.Enable();
        Assert.True(template.IsEnabled);
    }

    [Fact]
    public void Row_DefaultSignMultiplier_IsOne()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.Custom);
        var row = template.AddRow("Test Row", FinancialReportDataSource.AccountData, 10);
        Assert.Equal(1, row.SignMultiplier);
    }

    [Fact]
    public void Row_HideWhenEmpty_DefaultsFalse()
    {
        var template = new FinancialReportTemplate(Guid.NewGuid(), "Test", FinancialReportType.Custom);
        var row = template.AddRow("Test Row", FinancialReportDataSource.AccountData, 10);
        Assert.False(row.HideWhenEmpty);
        Assert.False(row.IsBold);
    }

    [Fact]
    public void ReportType_AllValues_Exist()
    {
        Assert.Equal(0, (int)FinancialReportType.ProfitAndLoss);
        Assert.Equal(1, (int)FinancialReportType.BalanceSheet);
        Assert.Equal(2, (int)FinancialReportType.CashFlow);
        Assert.Equal(3, (int)FinancialReportType.Custom);
    }

    [Fact]
    public void DataSource_AllValues_Exist()
    {
        Assert.Equal(0, (int)FinancialReportDataSource.AccountData);
        Assert.Equal(1, (int)FinancialReportDataSource.CalculatedAmount);
        Assert.Equal(2, (int)FinancialReportDataSource.CustomApi);
        Assert.Equal(3, (int)FinancialReportDataSource.BlankLine);
        Assert.Equal(4, (int)FinancialReportDataSource.ColumnBreak);
        Assert.Equal(5, (int)FinancialReportDataSource.SectionBreak);
    }

    // ─── Formula Engine Tests ────────────────────────────────────────────────

    [Fact]
    public void EvaluateFormula_SimpleAddition()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["REVENUE"] = 50000m,
            ["COGS"] = 30000m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("REVENUE - COGS", refs);
        Assert.Equal(20000m, result);
    }

    [Fact]
    public void EvaluateFormula_MultipleOperations()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 100m,
            ["B"] = 50m,
            ["C"] = 25m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("A - B - C", refs);
        Assert.Equal(25m, result);
    }

    [Fact]
    public void EvaluateFormula_Multiplication()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = 1000m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("BASE * 0.1", refs);
        Assert.Equal(100m, result);
    }

    [Fact]
    public void EvaluateFormula_DivisionByZero_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = 1000m,
            ["B"] = 0m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("A / B", refs);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void EvaluateFormula_EmptyFormula_ReturnsZero()
    {
        var refs = new Dictionary<string, decimal>();
        Assert.Equal(0m, FinancialReportFormulaEngine.EvaluateFormula("", refs));
        Assert.Equal(0m, FinancialReportFormulaEngine.EvaluateFormula("  ", refs));
    }

    [Fact]
    public void EvaluateFormula_UnknownRef_TreatedAsZero()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["KNOWN"] = 500m,
        };

        // UNKNOWN gets replaced with nothing → parse as 0
        var result = FinancialReportFormulaEngine.EvaluateFormula("KNOWN + UNKNOWN", refs);
        Assert.Equal(500m, result);
    }

    [Fact]
    public void EvaluateFormula_CaseInsensitive()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Revenue"] = 10000m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("REVENUE", refs);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void EvaluateFormula_WithAbs()
    {
        var refs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["LOSS"] = -5000m,
        };

        var result = FinancialReportFormulaEngine.EvaluateFormula("abs(LOSS)", refs);
        Assert.Equal(5000m, result);
    }

    // ─── Growth Calculation Tests ────────────────────────────────────────────

    [Fact]
    public void CalculateGrowth_ZeroPrevious_CurrentPositive_Returns100()
    {
        // Per gotcha #427: v16 behavior — growth from zero = 100%
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(5000m, 0m));
    }

    [Fact]
    public void CalculateGrowth_ZeroPrevious_CurrentNegative_ReturnsMinus100()
    {
        Assert.Equal(-100m, FinancialReportFormulaEngine.CalculateGrowth(-5000m, 0m));
    }

    [Fact]
    public void CalculateGrowth_ZeroBoth_ReturnsZero()
    {
        Assert.Equal(0m, FinancialReportFormulaEngine.CalculateGrowth(0m, 0m));
    }

    [Fact]
    public void CalculateGrowth_NormalIncrease()
    {
        // 1000 → 1500 = 50% growth
        Assert.Equal(50m, FinancialReportFormulaEngine.CalculateGrowth(1500m, 1000m));
    }

    [Fact]
    public void CalculateGrowth_NormalDecrease()
    {
        // 1000 → 750 = -25% growth
        Assert.Equal(-25m, FinancialReportFormulaEngine.CalculateGrowth(750m, 1000m));
    }

    [Fact]
    public void CalculateGrowth_DoubleValue()
    {
        // 1000 → 2000 = 100% growth
        Assert.Equal(100m, FinancialReportFormulaEngine.CalculateGrowth(2000m, 1000m));
    }
}
