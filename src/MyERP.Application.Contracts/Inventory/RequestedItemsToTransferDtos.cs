using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class RequestedItemsToTransferFilterDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? SearchText { get; set; }
}

public class RequestedItemsToTransferRowDto
{
    public Guid MaterialRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Uom { get; set; } = string.Empty;
    public decimal RequestedQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal QtyToTransfer { get; set; }
    public decimal CompletionPercent { get; set; }
    public Guid? SourceWarehouseId { get; set; }
    public string? SourceWarehouseName { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public string? TargetWarehouseName { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }
}

public class RequestedItemsToTransferReportDto
{
    public List<RequestedItemsToTransferRowDto> Rows { get; set; } = new();
    public int TotalRequests { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQtyToTransfer { get; set; }
}
