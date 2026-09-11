using System;
using MyERP.HumanResources.Entities;
using Xunit;

namespace MyERP.Domain.Tests.HumanResources;

/// <summary>
/// Unit tests for Holiday List half-day holiday calculations (ERPNext PR #58792).
/// Half-day holiday counts as 0.5 days, full day counts as 1.0 day.
/// </summary>
public class HolidayListHalfDayTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void HolidayList_FullDayHolidays_CountsOneDayEach()
    {
        var list = new HolidayList(Guid.NewGuid(), _companyId, "National Holidays 2026", 2026);
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 1), "New Year", isWeeklyOff: false, isHalfDay: false));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 5, 1), "Labour Day", isWeeklyOff: false, isHalfDay: false));

        Assert.Equal(2, list.Holidays.Count);
        Assert.Equal(2.0m, list.TotalHolidays);
    }

    [Fact]
    public void HolidayList_HalfDayHolidays_CountsHalfDayEach()
    {
        var list = new HolidayList(Guid.NewGuid(), _companyId, "Festive Eve Holidays 2026", 2026);
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 28), "Chinese New Year Eve", isWeeklyOff: false, isHalfDay: true));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 12, 24), "Christmas Eve", isWeeklyOff: false, isHalfDay: true));

        Assert.Equal(2, list.Holidays.Count);
        Assert.Equal(1.0m, list.TotalHolidays);
    }

    [Fact]
    public void HolidayList_MixedFullAndHalfDayHolidays_CalculatesCorrectSum()
    {
        var list = new HolidayList(Guid.NewGuid(), _companyId, "Company Calendar 2026", 2026);
        // 3 full days + 3 half days = 3 * 1.0 + 3 * 0.5 = 4.5 days
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 1), "New Year's Day", isWeeklyOff: false, isHalfDay: false));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 28), "CNY Eve", isWeeklyOff: false, isHalfDay: true));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 29), "CNY Day 1", isWeeklyOff: false, isHalfDay: false));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 1, 30), "CNY Day 2", isWeeklyOff: false, isHalfDay: false));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 12, 24), "Christmas Eve", isWeeklyOff: false, isHalfDay: true));
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, new DateTime(2026, 12, 31), "New Year's Eve", isWeeklyOff: false, isHalfDay: true));

        Assert.Equal(6, list.Holidays.Count);
        Assert.Equal(4.5m, list.TotalHolidays);
    }

    [Fact]
    public void HolidayList_IsHoliday_ReturnsTrueForHalfDayHoliday()
    {
        var list = new HolidayList(Guid.NewGuid(), _companyId, "Calendar 2026", 2026);
        var date = new DateTime(2026, 12, 24);
        list.AddHoliday(new Holiday(Guid.NewGuid(), list.Id, date, "Christmas Eve", isWeeklyOff: false, isHalfDay: true));

        Assert.True(list.IsHoliday(date));
        Assert.True(list.IsHoliday(new DateTime(2026, 12, 24, 14, 30, 0))); // different time of same day
        Assert.False(list.IsHoliday(new DateTime(2026, 12, 25)));
    }
}
