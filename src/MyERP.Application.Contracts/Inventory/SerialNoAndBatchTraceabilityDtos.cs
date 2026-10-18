using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class SerialNoAndBatchTraceabilityFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public string? SerialNo { get; set; }
    public string? TraceabilityDirection { get; set; } = "Both"; // "Backward", "Forward", "Both"
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class SerialNoAndBatchTraceabilityNodeDto
{
    public Guid NodeId { get; set; }
    public string Direction { get; set; } = string.Empty; // "Backward", "Forward"
    public int Indent { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceDocType { get; set; } = string.Empty;
    public Guid ReferenceDocId { get; set; }
    public string ReferenceDocNo { get; set; } = string.Empty;
    public DateTime PostingDateTime { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNo { get; set; }
    public DateTime? BatchExpiryDate { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? AmcExpiryDate { get; set; }
}

public class SerialNoAndBatchTraceabilityReportDto
{
    public int TotalNodes { get; set; }
    public int RootNodesCount { get; set; }
    public List<SerialNoAndBatchTraceabilityNodeDto> Nodes { get; set; } = new();
}
