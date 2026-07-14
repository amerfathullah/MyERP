using System;
using System.Linq;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.HumanResources;

public class PayrollPostingTests
{
    [Fact]
    public void PayrollEntry_TotalExpense_IncludesEmployerContributions()
    {
        var entry = CreatePayrollEntry();
        // Gross: 5000, Employer: EPF 650 + SOCSO 87.50 + EIS 10 = 747.50
        // Total cost = Gross + Employer = 5000 + 747.50 = 5747.50
        var totalExpense = entry.TotalGrossSalary + entry.TotalEmployerContributions;
        totalExpense.ShouldBeGreaterThan(entry.TotalGrossSalary);
    }

    [Fact]
    public void PayrollEntry_BalanceEquation_DebitEqualsCreditConcept()
    {
        var entry = CreatePayrollEntry();
        // DR = TotalGrossSalary + TotalEmployerContributions (total cost to company)
        decimal totalDebit = entry.TotalGrossSalary + entry.TotalEmployerContributions;

        // CR = NetSalary + EPF(E+R) + SOCSO(E+R) + EIS(E+R) + PCB
        var line = entry.Lines.First();
        decimal totalCredit = entry.TotalNetSalary
            + (line.EpfEmployee + line.EpfEmployer)
            + (line.SocsoEmployee + line.SocsoEmployer)
            + (line.EisEmployee + line.EisEmployer)
            + line.Pcb;

        // DR must equal CR (double-entry) — this is the fundamental accounting constraint
        totalDebit.ShouldBe(totalCredit);
    }

    [Fact]
    public void PayrollEntry_MultipleEmployees_AggregatesCorrectly()
    {
        var entry = new PayrollEntry(
            Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 7,
            new DateTime(2026, 7, 31));

        entry.AddLine(Guid.NewGuid(), "Employee A", 5000, 550, 650, 17.50m, 87.50m, 10, 10, 200);
        entry.AddLine(Guid.NewGuid(), "Employee B", 8000, 880, 1040, 17.50m, 87.50m, 16, 16, 500);

        entry.TotalGrossSalary.ShouldBe(13000m);
        entry.TotalNetSalary.ShouldBe(
            (5000 - 550 - 17.50m - 10 - 200) + (8000 - 880 - 17.50m - 16 - 500));
        entry.TotalEmployerContributions.ShouldBe(650 + 87.50m + 10 + 1040 + 87.50m + 16);
    }

    [Fact]
    public void PayrollEntry_GLPattern_SalaryExpenseDebit()
    {
        // Salary expense = gross + employer contributions
        // This is the full cost to the company
        var entry = CreatePayrollEntry();
        var grossPlusEmployer = entry.TotalGrossSalary + entry.TotalEmployerContributions;
        grossPlusEmployer.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PayrollEntry_GLPattern_NetPayableCredit()
    {
        // Net payable = gross - all employee deductions
        // This is what employees receive in their bank accounts
        var entry = CreatePayrollEntry();
        entry.TotalNetSalary.ShouldBeGreaterThan(0);
        entry.TotalNetSalary.ShouldBeLessThan(entry.TotalGrossSalary);
    }

    [Fact]
    public void PayrollEntry_GLPattern_StatutoryPayableCredits()
    {
        // Each statutory body gets a separate payable:
        // - EPF (KWSP): employee + employer portions
        // - SOCSO (PERKESO): employee + employer
        // - EIS: employee + employer
        // - PCB (LHDN): withheld income tax
        var entry = CreatePayrollEntry();
        var line = entry.Lines.First();

        var epfPayable = line.EpfEmployee + line.EpfEmployer;
        var socsoPayable = line.SocsoEmployee + line.SocsoEmployer;
        var eisPayable = line.EisEmployee + line.EisEmployer;
        var pcbPayable = line.Pcb;

        epfPayable.ShouldBeGreaterThan(0);
        socsoPayable.ShouldBeGreaterThan(0);
        eisPayable.ShouldBeGreaterThan(0);
        pcbPayable.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PayrollEntry_Submit_ChangesStatus()
    {
        var entry = CreatePayrollEntry();
        entry.Status.ShouldBe(MyERP.Core.DocumentStatus.Draft);
        entry.Submit();
        entry.Status.ShouldBe(MyERP.Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void PayrollEntry_EmptySubmit_Throws()
    {
        var entry = new PayrollEntry(
            Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 7,
            new DateTime(2026, 7, 31));

        // No lines added — should throw
        Should.Throw<Volo.Abp.BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void PayrollEntry_PostingDate_IsLastDayOfMonth()
    {
        var entry = new PayrollEntry(
            Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 2,
            new DateTime(2026, 2, 28));

        entry.PostingDate.Day.ShouldBe(28); // Feb 2026 (non-leap year)
    }

    [Fact]
    public void PayrollEntryLine_NetSalary_Calculation()
    {
        var entry = CreatePayrollEntry();
        var line = entry.Lines.First();

        // Net = Gross - (EPF_E + SOCSO_E + EIS_E + PCB)
        // Net = 5000 - (550 + 17.50 + 10 + 200) = 5000 - 777.50 = 4222.50
        line.NetSalary.ShouldBe(5000m - 550m - 17.50m - 10m - 200m);
    }

    private static PayrollEntry CreatePayrollEntry()
    {
        var entry = new PayrollEntry(
            Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 7,
            new DateTime(2026, 7, 31));

        // Standard MY payroll: RM 5000 salary
        // EPF Employee: 11% = 550, EPF Employer: 13% = 650
        // SOCSO Employee: 0.35% = 17.50, SOCSO Employer: 1.75% = 87.50
        // EIS Employee: 0.2% = 10, EIS Employer: 0.2% = 10
        // PCB: estimated = 200
        entry.AddLine(Guid.NewGuid(), "Test Employee", 5000m,
            550m, 650m, 17.50m, 87.50m, 10m, 10m, 200m);

        return entry;
    }
}
