using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class MaterialRequestsWithoutSupplierQuotationsFilterDto
{
    public Guid? CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? SearchTerm { get; set; }
}

public class MaterialRequestsWithoutSupplierQuotationsItemDto
{
    public Guid MaterialRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
}

public class MaterialRequestsWithoutSupplierQuotationsReportDto
{
    public Guid? CompanyId { get; set; }
    public int TotalPendingRequests { get; set; }
    public int TotalPendingItems { get; set; }
    public decimal TotalPendingQuantity { get; set; }
    public List<MaterialRequestsWithoutSupplierQuotationsItemDto> Items { get; set; } = new();
}
