using System;

namespace MyERP;

/// <summary>
/// Normalizes UI calendar date boundaries for timestamped queries so inclusive
/// end dates do not drop same-day transactions posted after midnight.
/// </summary>
public static class DateRangeHelper
{
    public static DateTime StartOfDay(DateTime date) => date.Date;

    public static DateTime ExclusiveNextDay(DateTime date) => date.Date.AddDays(1);
}
