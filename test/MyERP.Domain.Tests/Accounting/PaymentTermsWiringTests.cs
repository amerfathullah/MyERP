using System;
using System.Linq;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Tests.Accounting;

public class PaymentTermsWiringTests
{
    private static PaymentTermsTemplate CreateTemplate(string name = "Test")
    {
        return new PaymentTermsTemplate(Guid.NewGuid(), name, Guid.NewGuid());
    }

    private static void AddTerm(PaymentTermsTemplate template, decimal portion, int creditDays, string? desc = null)
    {
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, portion, creditDays, desc));
    }

    [Fact]
    public void GenerateSchedule_SingleTerm_Net30()
    {
        var template = CreateTemplate("Net 30");
        AddTerm(template, 100m, 30, "Full payment in 30 days");

        var schedule = template.GenerateSchedule(new DateTime(2026, 1, 1), 1000m);

        schedule.Count.ShouldBe(1);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 1, 31));
        schedule[0].PaymentAmount.ShouldBe(1000m);
    }

    [Fact]
    public void GenerateSchedule_SplitTerms_50_50()
    {
        var template = CreateTemplate("50/50");
        AddTerm(template, 50m, 0, "Immediate 50%");
        AddTerm(template, 50m, 30, "Balance in 30 days");

        var schedule = template.GenerateSchedule(new DateTime(2026, 3, 15), 2000m);

        schedule.Count.ShouldBe(2);
        schedule[0].PaymentAmount.ShouldBe(1000m);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 3, 15)); // Same day (0 credit days)
        schedule[1].PaymentAmount.ShouldBe(1000m);
        schedule[1].DueDate.ShouldBe(new DateTime(2026, 4, 14)); // +30 days
    }

    [Fact]
    public void GenerateSchedule_DueDateFloorRule()
    {
        var template = CreateTemplate("Immediate");
        AddTerm(template, 100m, 0, "Immediate");

        var postingDate = new DateTime(2026, 6, 15);
        var schedule = template.GenerateSchedule(postingDate, 500m);

        schedule[0].DueDate.ShouldBeGreaterThanOrEqualTo(postingDate);
    }

    [Fact]
    public void GenerateSchedule_ThreePart_30_30_40()
    {
        var template = CreateTemplate("30/30/40");
        AddTerm(template, 30m, 0, "Advance");
        AddTerm(template, 30m, 30, "Progress");
        AddTerm(template, 40m, 60, "Final");

        var schedule = template.GenerateSchedule(new DateTime(2026, 1, 1), 10000m);

        schedule.Count.ShouldBe(3);
        schedule[0].PaymentAmount.ShouldBe(3000m); // 30%
        schedule[1].PaymentAmount.ShouldBe(3000m); // 30%
        schedule[2].PaymentAmount.ShouldBe(4000m); // 40%
        schedule[2].DueDate.ShouldBe(new DateTime(2026, 3, 2)); // +60 days
    }

    [Fact]
    public void GenerateSchedule_LastDueDate_IsMaximum()
    {
        var template = CreateTemplate("Staggered");
        AddTerm(template, 40m, 30, "First");
        AddTerm(template, 30m, 60, "Second");
        AddTerm(template, 30m, 90, "Third");

        var schedule = template.GenerateSchedule(new DateTime(2026, 1, 1), 9000m);

        var maxDueDate = schedule.Max(s => s.DueDate);
        maxDueDate.ShouldBe(new DateTime(2026, 4, 1)); // Jan 1 + 90 days
    }
}
