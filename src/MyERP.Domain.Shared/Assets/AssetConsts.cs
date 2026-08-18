namespace MyERP.Assets;

public static class AssetConsts
{
    public const int MaxAssetNumberLength = 50;
    public const int MaxAssetNameLength = 200;
    public const int MaxLocationLength = 200;
    public const int MaxNoteLength = 2000;
    public const int MaxSubjectLength = 250;
    public const int MaxDescriptionLength = 4000;
    public const int MaxDowntimeLength = 50;
    public const int MaxReferenceTypeLength = 100;
    public const int MaxReferenceIdLength = 100;
}

public static class AssetCategoryConsts
{
    public const int MaxCategoryNameLength = 100;
}

public static class AssetMovementConsts
{
    public const int MaxMovementNumberLength = 50;
    public const int MaxLocationLength = 200;
}

public static class AssetRepairConsts
{
    public const int MaxRepairNumberLength = 50;
    public const int MaxActionsPerformedLength = 4000;
}

public static class AssetCapitalizationConsts
{
    public const int MaxCapitalizationNumberLength = 50;
    public const int MaxTitleLength = 250;
    public const int MaxBatchNumberLength = 100;
    public const int MaxSerialNumberLength = 100;
}

public static class AssetValueAdjustmentConsts
{
    public const int MaxAdjustmentNumberLength = 50;
    public const int MaxNotesLength = 2000;
}

public static class LocationConsts
{
    public const int MaxLocationNameLength = 200;
}

public static class AssetShiftFactorConsts
{
    public const int MaxShiftNameLength = 100;
}

public static class AssetShiftAllocationConsts
{
    public const int MaxAllocationNumberLength = 50;
}

public static class FleetConsts
{
    public const int MaxCategoryNameLength = 100;
    public const int MaxDriverNameLength = 200;
    public const int MaxLicenseNumberLength = 50;
    public const int MaxLicensePlateLength = 30;
}

public enum DriverStatus
{
    Active = 0,
    Suspended = 1,
    Left = 2,
}

public enum VehicleFuelType
{
    Petrol = 0,
    Diesel = 1,
    Electric = 2,
    Hybrid = 3,
    Cng = 4,
}
