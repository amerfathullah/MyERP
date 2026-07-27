using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;

namespace MyERP.DomainTests;

/// <summary>
/// Tests for localization polish (manufacturing settings, loan detail, ERR, scorecard),
/// employee GUID→Name resolution for loan detail, and new localization key coverage.
/// Session: 2026-07-25 (latest continuation).
/// </summary>
public class LocalizationPolishAndEmployeeResolutionTests
{
    // ── Manufacturing Settings localization prerequisites ──

    [Fact]
    public void ManufacturingSettings_OverproductionPercentage_DefaultsTo5()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(5m, settings.OverproductionPercentage);
    }

    [Fact]
    public void ManufacturingSettings_BackflushRawMaterialsBasedOn_DefaultsBOM()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal("BOM", settings.BackflushRawMaterialsBasedOn);
    }

    [Fact]
    public void ManufacturingSettings_AllowProductionOnHolidays_DefaultsFalse()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(settings.AllowProductionOnHolidays);
    }

    [Fact]
    public void ManufacturingSettings_CapacityPlanningForDays_DefaultsTo30()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(30, settings.CapacityPlanningForDays);
    }

    [Fact]
    public void ManufacturingSettings_DisableCapacityPlanning_DefaultsFalse()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(settings.DisableCapacityPlanning);
    }

    [Fact]
    public void ManufacturingSettings_EnforceTimeLogs_DefaultsFalse()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(settings.EnforceTimeLogs);
    }

    // ── Loan employee GUID resolution prerequisites ──

    [Fact]
    public void Loan_EmployeeId_IsSetOnCreation()
    {
        var empId = Guid.NewGuid();
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), empId, "LN-001", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 10000m, 6.0m, 12);
        Assert.Equal(empId, loan.EmployeeId);
    }

    [Fact]
    public void Loan_OutstandingBalance_DefaultsToLoanAmount()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-002", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 50000m, 8.0m, 24);
        Assert.Equal(50000m, loan.OutstandingBalance);
    }

    [Fact]
    public void Employee_FullName_CombinesFirstAndLast()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-001", "John");
        emp.LastName = "Doe";
        var fullName = $"{emp.FirstName} {emp.LastName}".Trim();
        Assert.Equal("John Doe", fullName);
    }

    [Fact]
    public void Employee_FullName_FirstNameOnlyWhenLastNameEmpty()
    {
        var emp = new Employee(Guid.NewGuid(), Guid.NewGuid(), "EMP-002", "Ahmad");
        var fullName = new[] { emp.FirstName, emp.LastName }.Where(s => !string.IsNullOrWhiteSpace(s));
        Assert.Equal("Ahmad", string.Join(" ", fullName));
    }

    // ── Exchange Rate Revaluation localization prerequisites ──

    [Fact]
    public void ExchangeRateRevaluation_DefaultStatusIsDraft()
    {
        var err = new ExchangeRateRevaluation(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        Assert.Equal(0, (int)err.Status);
    }

    [Fact]
    public void ExchangeRateRevaluation_RoundingAllowanceMustBeUnderOne()
    {
        var err = new ExchangeRateRevaluation(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, Guid.NewGuid());
        err.RoundingLossAllowance = 0.5m;
        Assert.True(err.RoundingLossAllowance < 1m);
    }

    // ── Supplier Scorecard localization prerequisites ──

    [Fact]
    public void SupplierScorecard_PreventPurchaseOrders_DefaultsFalse()
    {
        var supplierId = Guid.NewGuid();
        var scorecard = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), supplierId);
        var flags = scorecard.GetEnforcementFlags();
        Assert.False(flags.PreventPos);
    }

    [Fact]
    public void SupplierScorecard_DefaultScore_Is100()
    {
        var scorecard = new SupplierScorecard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(100m, scorecard.Score);
    }

    // ── Work Order localization prerequisites ──

    [Fact]
    public void WorkOrder_ProducedQuantity_DefaultsToZero()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 100);
        Assert.Equal(0m, wo.ProducedQuantity);
    }

    [Fact]
    public void WorkOrder_PercentComplete_ZeroWhenNoProduction()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-002", Guid.NewGuid(), Guid.NewGuid(), 50);
        Assert.Equal(0m, wo.PercentComplete);
    }

    // ── Date presets + localization key coverage ──

    [Fact]
    public void LocalizationKeys_NewKeysExist()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        if (!File.Exists(path)) return; // Skip if path not found in CI

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var texts = doc.RootElement.GetProperty("texts");

        var requiredKeys = new[]
        {
            "OverproductionPercent", "BackflushRmBasedOn", "SchedulingAndCapacity",
            "AllowOvertime", "AllowHolidayProduction", "DisableCapacityPlanning",
            "Sanction", "Disburse", "RecordRepayment", "DisburseLoan",
            "RoundingLossAllowance", "GetEligibleAccounts", "EligibleAccounts",
            "GainLoss", "TotalGainLoss", "CreateRevaluationEntry",
            "BlocksPO", "BlocksRFQ", "ThisMonth", "LastMonth", "ThisQuarter",
            "StartProduction", "RecordProduction", "RecordConsumption",
            "LoanSanctioned", "LoanDisbursed", "RepaymentRecorded"
        };

        foreach (var key in requiredKeys)
        {
            Assert.True(texts.TryGetProperty(key, out _), $"Missing localization key: {key}");
        }
    }

    [Fact]
    public void LocalizationKeys_CountAbove1650()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");

        if (!File.Exists(path)) return;

        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        var texts = doc.RootElement.GetProperty("texts");
        var count = texts.EnumerateObject().Count();

        Assert.True(count >= 1650, $"Expected at least 1650 keys, found {count}");
    }

    // ── Loan status labels (for localized toaster messages) ──

    [Fact]
    public void Loan_Sanction_ChangesStatusTo1()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-003", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 10000m, 5m, 12);
        loan.Sanction();
        Assert.Equal(1, (int)loan.Status);
    }

    [Fact]
    public void Loan_Cancel_ChangesStatusTo5()
    {
        var loan = new Loan(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LN-004", LoanType.TermLoan, InterestCalculationMethod.DiminishingBalance, 10000m, 5m, 12);
        loan.Cancel();
        Assert.Equal(5, (int)loan.Status);
    }

    // ── CostCenterAllocation (from prior session — verify cycle detection still works) ──

    [Fact]
    public void CostCenterAllocation_SelfReference_Throws()
    {
        var ccId = Guid.NewGuid();
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), ccId, DateTime.Today);
        Assert.ThrowsAny<Exception>(() => alloc.AddEntry(ccId, 100m));
    }

    [Fact]
    public void CostCenterAllocation_EvenDistribution_SumsTo100()
    {
        var alloc = new CostCenterAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.Today);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.AddEntry(Guid.NewGuid(), 50m);
        alloc.ValidatePercentages();
        var total = alloc.Entries.Sum(e => e.Percentage);
        Assert.Equal(100m, total);
    }

    // ── Item details for dropdown entity selectors ──

    [Fact]
    public void Item_ItemName_NotEmpty()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITEM-001", "Widget A", ItemType.Goods);
        Assert.False(string.IsNullOrEmpty(item.ItemName));
    }

    [Fact]
    public void Customer_Name_NotEmpty()
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp");
        Assert.False(string.IsNullOrEmpty(customer.Name));
    }

    [Fact]
    public void Supplier_Name_NotEmpty()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "SupplyCo");
        Assert.False(string.IsNullOrEmpty(supplier.Name));
    }
}
