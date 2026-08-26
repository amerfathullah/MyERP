using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class ProcessDeferredAccountingDomainTests
{
    [Fact]
    public void Should_Create_Valid_ProcessDeferredAccounting()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var process = new ProcessDeferredAccounting(
            id,
            "ACC-PDA-20260826-001",
            companyId,
            DeferredAccountingType.Income,
            postingDate: DateTime.UtcNow.Date,
            startDate: new DateTime(2026, 1, 1),
            endDate: new DateTime(2026, 1, 31));

        process.Id.ShouldBe(id);
        process.ProcessNumber.ShouldBe("ACC-PDA-20260826-001");
        process.Type.ShouldBe(DeferredAccountingType.Income);
        process.IsSubmitted.ShouldBeFalse();
        process.IsCancelled.ShouldBeFalse();

        process.Submit(5);
        process.IsSubmitted.ShouldBeTrue();
        process.EntriesProcessed.ShouldBe(5);

        process.Cancel();
        process.IsCancelled.ShouldBeTrue();
    }

    [Fact]
    public void Should_Throw_When_EndDate_Before_StartDate()
    {
        Should.Throw<BusinessException>(() =>
        {
            new ProcessDeferredAccounting(
                Guid.NewGuid(),
                "ACC-PDA-ERR",
                Guid.NewGuid(),
                DeferredAccountingType.Expense,
                postingDate: DateTime.UtcNow.Date,
                startDate: new DateTime(2026, 2, 1),
                endDate: new DateTime(2026, 1, 1));
        });
    }
}
