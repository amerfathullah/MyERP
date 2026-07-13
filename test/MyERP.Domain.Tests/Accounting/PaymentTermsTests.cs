using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class PaymentTermsTests
{
    [Fact]
    public void GenerateSchedule_Net30_ShouldReturnSingleLine()
    {
        var template = CreateTemplate("Net 30");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 100m, 30, "Net 30"));

        var schedule = template.GenerateSchedule(new DateTime(2026, 1, 1), 10000m);

        schedule.ShouldHaveSingleItem();
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 1, 31));
        schedule[0].PaymentAmount.ShouldBe(10000m);
        schedule[0].InvoicePortion.ShouldBe(100m);
    }

    [Fact]
    public void GenerateSchedule_SplitPayment_ShouldReturnMultipleLines()
    {
        var template = CreateTemplate("50/50 Split");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 0, "Advance"));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 30, "Balance"));

        var schedule = template.GenerateSchedule(new DateTime(2026, 3, 15), 20000m);

        schedule.Count.ShouldBe(2);
        schedule[0].DueDate.ShouldBe(new DateTime(2026, 3, 15)); // immediate
        schedule[0].PaymentAmount.ShouldBe(10000m);
        schedule[1].DueDate.ShouldBe(new DateTime(2026, 4, 14)); // +30 days
        schedule[1].PaymentAmount.ShouldBe(10000m);
    }

    [Fact]
    public void GenerateSchedule_ThreePartSplit_30_60_90()
    {
        var template = CreateTemplate("30/60/90");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 33.33m, 30));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 33.33m, 60));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 33.34m, 90));

        var schedule = template.GenerateSchedule(new DateTime(2026, 6, 1), 9000m);

        schedule.Count.ShouldBe(3);
        // 33.33% of 9000 = 2999.70, 33.34% = 3000.60
        schedule[0].PaymentAmount.ShouldBe(2999.70m);
        schedule[1].PaymentAmount.ShouldBe(2999.70m);
        schedule[2].PaymentAmount.ShouldBe(3000.60m);
        // Total should match original
        (schedule[0].PaymentAmount + schedule[1].PaymentAmount + schedule[2].PaymentAmount).ShouldBe(9000m);
    }

    [Fact]
    public void ValidatePortions_SumTo100_ShouldNotThrow()
    {
        var template = CreateTemplate("Valid");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 60m, 0));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 40m, 30));

        Should.NotThrow(() => template.ValidatePortions());
    }

    [Fact]
    public void ValidatePortions_SumNot100_ShouldThrow()
    {
        var template = CreateTemplate("Invalid");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 50m, 0));
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 30m, 30));

        Should.Throw<BusinessException>(() => template.ValidatePortions());
    }

    [Fact]
    public void GenerateSchedule_DueDateNeverBeforePostingDate()
    {
        var template = CreateTemplate("Immediate");
        template.AddTerm(new PaymentTerm(Guid.NewGuid(), template.Id, 100m, -5, "Pre-paid")); // negative days

        var postingDate = new DateTime(2026, 7, 1);
        var schedule = template.GenerateSchedule(postingDate, 5000m);

        schedule[0].DueDate.ShouldBeGreaterThanOrEqualTo(postingDate);
    }

    private static PaymentTermsTemplate CreateTemplate(string name) =>
        new(Guid.NewGuid(), name);
}
