using System;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class PeriodClosingVoucherTests
{
    private static PeriodClosingVoucher CreatePCV() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, new DateTime(2026, 6, 30), Guid.NewGuid());

    [Fact]
    public void Create_SetsDefaults()
    {
        var pcv = CreatePCV();
        pcv.Status.ShouldBe(Core.DocumentStatus.Draft);
        pcv.TotalClosingAmount.ShouldBe(0);
        pcv.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void AddEntry_UpdatesTotal()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 5000m, true);
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 3000m, false);
        pcv.Entries.Count.ShouldBe(2);
        pcv.TotalClosingAmount.ShouldBe(8000m);
    }

    [Fact]
    public void Submit_WithEntries_Succeeds()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 10000m, true);
        pcv.Submit();
        pcv.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_Empty_Throws()
    {
        var pcv = CreatePCV();
        Should.Throw<BusinessException>(() => pcv.Submit());
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();
        pcv.Cancel();
        pcv.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddEntry_AfterSubmit_Throws()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();
        Should.Throw<BusinessException>(() => pcv.AddEntry(Guid.NewGuid(), null, 1000m, false));
    }
}
