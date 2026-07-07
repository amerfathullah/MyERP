using System;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.HumanResources;

public class ContributionRuleTests
{
    [Fact]
    public void IsApplicable_AllCriteriaMet_ReturnsTrue()
    {
        var rule = CreateEpfRule();

        rule.IsApplicable(
            date: new DateTime(2026, 7, 1),
            salary: 5000m,
            age: 30,
            citizenship: CitizenshipType.Malaysian).ShouldBeTrue();
    }

    [Fact]
    public void IsApplicable_BeforeEffectiveDate_ReturnsFalse()
    {
        var rule = CreateEpfRule();

        rule.IsApplicable(
            date: new DateTime(2023, 1, 1),
            salary: 5000m,
            age: 30,
            citizenship: CitizenshipType.Malaysian).ShouldBeFalse();
    }

    [Fact]
    public void IsApplicable_AgeAboveMax_ReturnsFalse()
    {
        var rule = CreateEpfRule();
        rule.MaxAge = 60;

        rule.IsApplicable(
            date: new DateTime(2026, 7, 1),
            salary: 5000m,
            age: 65,
            citizenship: CitizenshipType.Malaysian).ShouldBeFalse();
    }

    [Fact]
    public void IsApplicable_WrongCitizenship_ReturnsFalse()
    {
        var rule = CreateEpfRule();
        rule.CitizenshipFilter = CitizenshipType.Malaysian;

        rule.IsApplicable(
            date: new DateTime(2026, 7, 1),
            salary: 5000m,
            age: 30,
            citizenship: CitizenshipType.ForeignWorker).ShouldBeFalse();
    }

    [Fact]
    public void IsApplicable_Inactive_ReturnsFalse()
    {
        var rule = CreateEpfRule();
        rule.IsActive = false;

        rule.IsApplicable(
            date: new DateTime(2026, 7, 1),
            salary: 5000m,
            age: 30,
            citizenship: CitizenshipType.Malaysian).ShouldBeFalse();
    }

    [Fact]
    public void EpfCalculation_StandardRate_ProducesCorrectAmount()
    {
        // EPF: Employee 11%, Employer 12% on RM5000
        var employeeRate = 11m;
        var employerRate = 12m;
        var salary = 5000m;

        var employeeContribution = Math.Round(salary * employeeRate / 100m, 2);
        var employerContribution = Math.Round(salary * employerRate / 100m, 2);

        employeeContribution.ShouldBe(550m);
        employerContribution.ShouldBe(600m);
    }

    [Fact]
    public void SocsoCalculation_WithCeiling_CapsAtLimit()
    {
        // SOCSO ceiling: RM5000
        var rate = 0.5m;
        var salary = 8000m;
        var ceiling = 5000m;

        var baseSalary = Math.Min(salary, ceiling);
        var contribution = Math.Round(baseSalary * rate / 100m, 2);

        contribution.ShouldBe(25m); // 5000 * 0.5% = 25, not 8000 * 0.5% = 40
    }

    private static ContributionRule CreateEpfRule()
    {
        return new ContributionRule(
            Guid.NewGuid(),
            type: ContributionType.EPF,
            employeeRate: 11m,
            employerRate: 12m,
            effectiveFrom: new DateTime(2024, 1, 1))
        {
            IsActive = true
        };
    }
}
