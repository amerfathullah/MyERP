namespace MyERP.Maintenance;

public static class WarrantyClaimConsts
{
    public const int MaxClaimNumberLength = 50;
    public const int MaxComplaintLength = 2000;
    public const int MaxResolutionLength = 2000;
}

/// <summary>Warranty claim status lifecycle.</summary>
public enum WarrantyClaimStatus
{
    Open = 0,
    WorkInProgress = 1,
    Closed = 2,
    Cancelled = 3
}
