using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class SerialNoWarrantyExpiryFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? ExpiryType { get; set; } = "Both";
    public string? MaintenanceStatus { get; set; }
    public int? DaysThreshold { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class SerialNoWarrantyExpiryRowDto
{
    public Guid SerialNoId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public int? DaysToWarrantyExpiry { get; set; }
    public bool IsWarrantyExpired { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
    public int? DaysToAmcExpiry { get; set; }
    public bool IsAmcExpired { get; set; }
    public string MaintenanceStatus { get; set; } = string.Empty;
    public SerialNoStatus Status { get; set; }
    public string? PurchaseDocumentType { get; set; }
    public string? DeliveryDocumentType { get; set; }
}

public class SerialNoWarrantyExpiryReportDto
{
    public DateTime AsOfDate { get; set; }
    public int TotalCount { get; set; }
    public int UnderWarrantyCount { get; set; }
    public int OutOfWarrantyCount { get; set; }
    public int UnderAmcCount { get; set; }
    public int OutOfAmcCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public List<SerialNoWarrantyExpiryRowDto> Rows { get; set; } = new();
}
