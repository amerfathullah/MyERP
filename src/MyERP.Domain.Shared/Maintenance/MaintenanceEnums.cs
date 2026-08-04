namespace MyERP.Maintenance;

public static class WarrantyClaimConsts
{
    public const int MaxClaimNumberLength = 50;
    public const int MaxComplaintLength = 2000;
    public const int MaxResolutionLength = 2000;
}

public static class MaintenanceConsts
{
    public const int MaxRemarksLength = 2000;
    public const int MaxWorkDoneLength = 4000;
}

/// <summary>Warranty claim status lifecycle.</summary>
public enum WarrantyClaimStatus
{
    Open = 0,
    WorkInProgress = 1,
    Closed = 2,
    Cancelled = 3
}

/// <summary>Maintenance schedule periodicity for visit generation.</summary>
public enum MaintenancePeriodicity
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    HalfYearly = 3,
    Yearly = 4,
    TwoYearly = 5,
    ThreeYearly = 6,
    Random = 7
}
