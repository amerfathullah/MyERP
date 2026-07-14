using System;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Core;

public class AutoRepeatTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _docId = Guid.NewGuid();

    private AutoRepeat CreateAutoRepeat(
        RepeatFrequency frequency = RepeatFrequency.Monthly,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        return new AutoRepeat(Guid.NewGuid(), _companyId, "SalesInvoice", _docId,
            frequency, startDate ?? new DateTime(2026, 1, 1), endDate);
    }

    [Fact]
    public void AutoRepeat_DefaultState()
    {
        var ar = CreateAutoRepeat();
        Assert.True(ar.IsEnabled);
        Assert.Equal(0, ar.GeneratedCount);
        Assert.Null(ar.LastGeneratedDate);
        Assert.Equal(new DateTime(2026, 1, 1), ar.NextScheduleDate);
        Assert.False(ar.NotifyByEmail);
    }

    [Fact]
    public void AutoRepeat_EndDateBeforeStartDate_Throws()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            new AutoRepeat(Guid.NewGuid(), _companyId, "SalesInvoice", _docId,
                RepeatFrequency.Monthly, new DateTime(2026, 6, 1), new DateTime(2026, 1, 1)));
        Assert.Equal("MyERP:01011", ex.Code);
    }

    [Fact]
    public void AutoRepeat_NextDate_Daily()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Daily, new DateTime(2026, 3, 15));
        var next = ar.CalculateNextDate(new DateTime(2026, 3, 15));
        Assert.Equal(new DateTime(2026, 3, 16), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Weekly()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Weekly, new DateTime(2026, 3, 15));
        var next = ar.CalculateNextDate(new DateTime(2026, 3, 15));
        Assert.Equal(new DateTime(2026, 3, 22), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Monthly()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 15));
        var next = ar.CalculateNextDate(new DateTime(2026, 1, 15));
        Assert.Equal(new DateTime(2026, 2, 15), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Monthly_ClampToLastDay()
    {
        // Jan 31 → Feb should clamp to Feb 28 (2026 is not a leap year)
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 31));
        var next = ar.CalculateNextDate(new DateTime(2026, 1, 31));
        Assert.Equal(new DateTime(2026, 2, 28), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Monthly_LeapYear()
    {
        // Jan 31 → Feb in leap year (2028) should clamp to Feb 29
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2028, 1, 31));
        var next = ar.CalculateNextDate(new DateTime(2028, 1, 31));
        Assert.Equal(new DateTime(2028, 2, 29), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Quarterly()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Quarterly, new DateTime(2026, 1, 15));
        var next = ar.CalculateNextDate(new DateTime(2026, 1, 15));
        Assert.Equal(new DateTime(2026, 4, 15), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_HalfYearly()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.HalfYearly, new DateTime(2026, 1, 15));
        var next = ar.CalculateNextDate(new DateTime(2026, 1, 15));
        Assert.Equal(new DateTime(2026, 7, 15), next);
    }

    [Fact]
    public void AutoRepeat_NextDate_Yearly()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Yearly, new DateTime(2026, 3, 10));
        var next = ar.CalculateNextDate(new DateTime(2026, 3, 10));
        Assert.Equal(new DateTime(2027, 3, 10), next);
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_AdvancesSchedule()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        ar.RecordGeneration(new DateTime(2026, 1, 1));

        Assert.Equal(1, ar.GeneratedCount);
        Assert.Equal(new DateTime(2026, 1, 1), ar.LastGeneratedDate);
        Assert.Equal(new DateTime(2026, 2, 1), ar.NextScheduleDate);
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_MultipleAdvances()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 1, 1));
        ar.RecordGeneration(new DateTime(2026, 1, 1));
        ar.RecordGeneration(new DateTime(2026, 2, 1));
        ar.RecordGeneration(new DateTime(2026, 3, 1));

        Assert.Equal(3, ar.GeneratedCount);
        Assert.Equal(new DateTime(2026, 4, 1), ar.NextScheduleDate);
    }

    [Fact]
    public void AutoRepeat_RecordGeneration_AutoDisablesAtEndDate()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 3, 15));

        ar.RecordGeneration(new DateTime(2026, 1, 1)); // Next: Feb 1 (within end)
        Assert.True(ar.IsEnabled);

        ar.RecordGeneration(new DateTime(2026, 2, 1)); // Next: Mar 1 (within end)
        Assert.True(ar.IsEnabled);

        ar.RecordGeneration(new DateTime(2026, 3, 1)); // Next: Apr 1 (past end)
        Assert.False(ar.IsEnabled);
    }

    [Fact]
    public void AutoRepeat_IsDueOn_BeforeStartDate_NotDue()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 3, 1));
        Assert.False(ar.IsDueOn(new DateTime(2026, 2, 28)));
    }

    [Fact]
    public void AutoRepeat_IsDueOn_OnScheduleDate_Due()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 3, 1));
        Assert.True(ar.IsDueOn(new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void AutoRepeat_IsDueOn_AfterScheduleDate_Due()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 3, 1));
        Assert.True(ar.IsDueOn(new DateTime(2026, 3, 5)));
    }

    [Fact]
    public void AutoRepeat_IsDueOn_Disabled_NotDue()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly, new DateTime(2026, 3, 1));
        ar.Disable();
        Assert.False(ar.IsDueOn(new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void AutoRepeat_IsDueOn_PastEndDate_NotDue()
    {
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly,
            new DateTime(2026, 1, 1), new DateTime(2026, 6, 30));
        // Manually advance past end
        Assert.False(ar.IsDueOn(new DateTime(2026, 7, 1)));
    }

    [Fact]
    public void AutoRepeat_Disable()
    {
        var ar = CreateAutoRepeat();
        ar.Disable();
        Assert.False(ar.IsEnabled);
    }

    [Fact]
    public void AutoRepeat_Enable_Valid()
    {
        var ar = CreateAutoRepeat(endDate: new DateTime(2027, 12, 31));
        ar.Disable();
        ar.Enable();
        Assert.True(ar.IsEnabled);
    }

    [Fact]
    public void AutoRepeat_Enable_PastEndDate_Throws()
    {
        // Create with a valid end date, then simulate time passing by disabling
        var ar = CreateAutoRepeat(RepeatFrequency.Monthly,
            new DateTime(2020, 1, 1), new DateTime(2020, 12, 31));
        ar.Disable();
        // End date is in the past (2020) vs current time (2026) → can't re-enable
        Assert.Throws<BusinessException>(() => ar.Enable());
    }

    [Fact]
    public void AutoRepeat_Quarterly_EndOfMonth_Clamping()
    {
        // Nov 30 + 3 months = Feb 28
        var ar = CreateAutoRepeat(RepeatFrequency.Quarterly, new DateTime(2026, 11, 30));
        var next = ar.CalculateNextDate(new DateTime(2026, 11, 30));
        Assert.Equal(new DateTime(2027, 2, 28), next);
    }

    [Fact]
    public void AutoRepeat_ReferenceDocumentType_Required()
    {
        Assert.Throws<ArgumentException>(() =>
            new AutoRepeat(Guid.NewGuid(), _companyId, "", _docId,
                RepeatFrequency.Monthly, DateTime.Today));
    }
}
