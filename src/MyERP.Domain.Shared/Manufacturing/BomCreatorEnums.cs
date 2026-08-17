namespace MyERP.Manufacturing;

/// <summary>Processing status of a BOM Creator staging tree. Maps to ERPNext BOM Creator.status.</summary>
public enum BomCreatorStatus
{
    Draft = 0,
    Completed = 1,
    Failed = 2,
}
