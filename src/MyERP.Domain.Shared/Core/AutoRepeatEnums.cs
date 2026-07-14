namespace MyERP.Core;

/// <summary>
/// Frequency for auto-repeat document generation.
/// </summary>
public enum RepeatFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    HalfYearly = 4,
    Yearly = 5
}

/// <summary>
/// Day of week for weekly repeat frequency.
/// </summary>
public enum RepeatDayOfWeek
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7
}
