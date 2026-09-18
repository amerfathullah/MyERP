using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

/// <summary>
/// Time bucket granularity for Material Requirements Planning.
/// </summary>
public enum MrpBucketSize
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2
}

/// <summary>
/// Filter input for the Material Requirements Planning (MRP) report.
/// </summary>
public class MaterialRequirementsPlanningFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public MrpBucketSize BucketSize { get; set; } = MrpBucketSize.Monthly;
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool IncludeSafetyStock { get; set; } = true;
}

/// <summary>
/// Represents a single time bucket column in the MRP report.
/// </summary>
public class MrpPeriodBucketDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Label { get; set; } = null!;
}

/// <summary>
/// Bucket-level metrics for an item in the MRP report.
/// </summary>
public class MrpItemBucketDataDto
{
    public DateTime BucketFromDate { get; set; }
    public decimal GrossRequirements { get; set; }
    public decimal ScheduledReceipts { get; set; }
    public decimal ProjectedAvailableBalance { get; set; }
    public decimal PlannedOrders { get; set; }
}

/// <summary>
/// Row representing an item's projected material requirements across all buckets.
/// </summary>
public class MrpItemRowDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = "Nos";
    public bool IsRawMaterial { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal CurrentStock { get; set; }
    public List<MrpItemBucketDataDto> Buckets { get; set; } = new();
}

/// <summary>
/// Complete Material Requirements Planning (MRP) report result.
/// </summary>
public class MaterialRequirementsPlanningReportDto
{
    public List<MrpPeriodBucketDto> Buckets { get; set; } = new();
    public List<MrpItemRowDto> Rows { get; set; } = new();
}
