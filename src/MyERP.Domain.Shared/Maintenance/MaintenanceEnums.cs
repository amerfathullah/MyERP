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

/// <summary>Maintenance visit completion status.</summary>
public enum MaintenanceVisitStatus
{
    Open = 0,
    PartiallyCompleted = 1,
    Completed = 2,
    Cancelled = 3,
}
