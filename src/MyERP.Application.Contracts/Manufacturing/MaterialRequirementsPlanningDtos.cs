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
/// Detailed MRP order requirement row for order generation (PR #58510 & PR #59007).
/// </summary>
public class MrpPlannedOrderRequirementDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string Uom { get; set; } = "Nos";
    public string TypeOfMaterial { get; set; } = "Purchase"; // "Purchase" or "Manufacture"
    public Guid? BomId { get; set; }
    public string? BomNo { get; set; }
    public decimal RequiredQty { get; set; }
    public decimal PlannedQty { get; set; }
    public decimal ProjectedQty { get; set; }
    public decimal MinOrderQty { get; set; }
    public decimal SafetyStock { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DateTime ReleaseDate { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public string? DefaultSupplierName { get; set; }
    public Guid? WarehouseId { get; set; }
}

/// <summary>
/// Complete Material Requirements Planning (MRP) report result.
/// </summary>
public class MaterialRequirementsPlanningReportDto
{
    public List<MrpPeriodBucketDto> Buckets { get; set; } = new();
    public List<MrpItemRowDto> Rows { get; set; } = new();
    public List<MrpPlannedOrderRequirementDto> Requirements { get; set; } = new();
}

/// <summary>
/// Selected row item for creating Purchase Orders or Work Orders from MRP.
/// </summary>
public class MrpOrderRowInputDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string? ItemName { get; set; }
    public string TypeOfMaterial { get; set; } = "Purchase";
    public Guid? BomId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime DeliveryDate { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
}

/// <summary>
/// Input to generate Purchase Orders and Work Orders from MRP report selection (PR #58510 & PR #58511).
/// </summary>
public class CreateOrdersFromMrpInput
{
    public Guid CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? MpsId { get; set; }
    public List<MrpOrderRowInputDto> SelectedRows { get; set; } = new();
}

/// <summary>
/// Summary of orders created from MRP.
/// </summary>
public class MrpOrdersCreatedDto
{
    public List<Guid> PurchaseOrderIds { get; set; } = new();
    public List<Guid> WorkOrderIds { get; set; } = new();
    public int PurchaseOrdersCount => PurchaseOrderIds.Count;
    public int WorkOrdersCount => WorkOrderIds.Count;
    public string Message { get; set; } = string.Empty;
}
