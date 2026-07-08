using System;
using MyERP.HumanResources.Entities;
using Shouldly;
using Xunit;

namespace MyERP.HumanResources;

public class PayrollEntryTests
{
    [Fact]
    public void Should_Create_Payroll_Entry()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-001", 2026, 7, new DateTime(2026, 7, 31));

        entry.Year.ShouldBe(2026);
        entry.Month.ShouldBe(7);
        entry.PayrollNumber.ShouldBe("PR-001");
        entry.Status.ShouldBe(Core.DocumentStatus.Draft);
        entry.Lines.Count.ShouldBe(0);
        entry.TotalGrossSalary.ShouldBe(0);
    }

    [Fact]
    public void Should_Add_Lines_And_Recalculate_Totals()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-002", 2026, 7, new DateTime(2026, 7, 31));

        entry.AddLine(Guid.NewGuid(), "Alice", 5000m, 550m, 650m, 100m, 200m, 10m, 20m, 150m);
        entry.AddLine(Guid.NewGuid(), "Bob", 3000m, 330m, 390m, 60m, 120m, 6m, 12m, 90m);

        entry.Lines.Count.ShouldBe(2);
        entry.TotalGrossSalary.ShouldBe(8000m);
        entry.TotalDeductions.ShouldBe(550m + 100m + 10m + 150m + 330m + 60m + 6m + 90m); // employee portions + PCB
        entry.TotalNetSalary.ShouldBe(8000m - entry.TotalDeductions);
        entry.TotalEmployerContributions.ShouldBe(650m + 200m + 20m + 390m + 120m + 12m);
    }

    [Fact]
    public void Should_Submit_With_Lines()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-003", 2026, 7, new DateTime(2026, 7, 31));
        entry.AddLine(Guid.NewGuid(), "Charlie", 4000m, 440m, 520m, 80m, 160m, 8m, 16m, 120m);

        entry.Submit();

        entry.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Should_Not_Submit_Without_Lines()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-004", 2026, 7, new DateTime(2026, 7, 31));

        Should.Throw<Volo.Abp.BusinessException>(() => entry.Submit());
    }

    [Fact]
    public void Should_Not_Add_Lines_After_Submit()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-005", 2026, 7, new DateTime(2026, 7, 31));
        entry.AddLine(Guid.NewGuid(), "Dave", 6000m, 660m, 780m, 120m, 240m, 12m, 24m, 180m);
        entry.Submit();

        Should.Throw<Volo.Abp.BusinessException>(() =>
            entry.AddLine(Guid.NewGuid(), "Eve", 5000m, 0m, 0m, 0m, 0m, 0m, 0m, 0m));
    }

    [Fact]
    public void Should_Cancel_Submitted_Entry()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-006", 2026, 7, new DateTime(2026, 7, 31));
        entry.AddLine(Guid.NewGuid(), "Frank", 4500m, 495m, 585m, 90m, 180m, 9m, 18m, 135m);
        entry.Submit();

        entry.Cancel();

        entry.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void Should_Not_Cancel_Already_Cancelled()
    {
        var entry = new PayrollEntry(Guid.NewGuid(), Guid.NewGuid(), "PR-007", 2026, 7, new DateTime(2026, 7, 31));
        entry.AddLine(Guid.NewGuid(), "Grace", 3500m, 385m, 455m, 70m, 140m, 7m, 14m, 105m);
        entry.Submit();
        entry.Cancel();

        Should.Throw<Volo.Abp.BusinessException>(() => entry.Cancel());
    }
}
