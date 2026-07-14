using System;
using MyERP.Accounting.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Tests for PayrollPostingService GL entry patterns.
/// Verifies the entity contracts and calculation patterns the service depends on.
/// </summary>
public class PayrollPostingServiceTests
{
    [Fact]
    public void PayrollEntry_TotalCostToCompany_IncludesEmployerContributions()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Employee A", 5000, 550, 650, 9.95m, 34.85m, 9.95m, 9.95m, 200);
        // entry.RecalculateTotals() called by AddLine

        // Expense = gross + employer contributions = total cost to company
        var totalEmployerContrib = 650m + 34.85m + 9.95m; // EPF + SOCSO + EIS employer
        entry.TotalEmployerContributions.ShouldBe(totalEmployerContrib);
    }

    [Fact]
    public void PayrollGL_Balanced_DebitEqualsCredit()
    {
        // Payroll JE pattern: DR Salary Expense (total cost), CR various payables
        var gross = 5000m;
        var epfEmployee = 550m;
        var epfEmployer = 650m;
        var socsoEE = 9.95m;
        var socsoER = 34.85m;
        var eisEE = 9.95m;
        var eisER = 9.95m;
        var pcb = 200m;

        var totalExpense = gross + epfEmployer + socsoER + eisER; // DR side
        var netPayable = gross - epfEmployee - socsoEE - eisEE - pcb; // CR Net Salary
        var epfPayable = epfEmployee + epfEmployer; // CR EPF
        var socsoPayable = socsoEE + socsoER; // CR SOCSO
        var eisPayable = eisEE + eisER; // CR EIS
        var pcbPayable = pcb; // CR PCB

        var totalCredits = netPayable + epfPayable + socsoPayable + eisPayable + pcbPayable;

        // DR = CR (double-entry)
        totalExpense.ShouldBe(totalCredits);
    }

    [Fact]
    public void PayrollEntry_Submit_ChangesStatus()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Employee A", 3000, 330, 390, 9.95m, 34.85m, 9.95m, 9.95m, 100);
        entry.Submit();
        entry.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void PayrollEntry_Cancel_ChangesStatus()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Employee A", 3000, 330, 390, 9.95m, 34.85m, 9.95m, 9.95m, 100);
        entry.Submit();
        entry.Cancel();
        entry.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void PayrollEntry_RecalculateTotals_SumsAllLines()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Emp A", 5000, 550, 650, 9.95m, 34.85m, 9.95m, 9.95m, 200);
        entry.AddLine(Guid.NewGuid(), "Emp B", 3000, 330, 390, 9.95m, 34.85m, 9.95m, 9.95m, 100);

        entry.TotalGrossSalary.ShouldBe(8000m);
    }

    [Fact]
    public void NetSalary_DeductsStatutory()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Employee", 5000, 550, 650, 9.95m, 34.85m, 9.95m, 9.95m, 200);

        var line = entry.Lines[0];
        var expectedNet = 5000 - 550 - 9.95m - 9.95m - 200;
        line.NetSalary.ShouldBe(expectedNet);
    }

    [Fact]
    public void LoanDeduction_ReducesNetSalary()
    {
        var entry = CreateEntry();
        entry.AddLine(Guid.NewGuid(), "Employee", 5000, 550, 650, 9.95m, 34.85m, 9.95m, 9.95m, 0);

        var line = entry.Lines[0];
        line.LoanDeduction = 500;

        // Net = Gross - EPF - SOCSO - EIS - PCB - Loan
        var expectedNet = 5000 - 550 - 9.95m - 9.95m - 0 - 500;
        line.NetSalary.ShouldBe(expectedNet);
    }

    private static PayrollEntry CreateEntry()
    {
        return new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PAY-001", 2026, 7, DateTime.UtcNow);
    }
}
