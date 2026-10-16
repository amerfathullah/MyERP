using System;
using System.Collections.Generic;

namespace MyERP.Purchasing;

public class SubcontractedRawMaterialsToBeTransferredFilterDto
{
    public Guid CompanyId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? SubcontractOrderId { get; set; }
    public Guid? RawMaterialItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class SubcontractedRawMaterialsToBeTransferredRowDto
{
    public Guid SubcontractOrderId { get; set; }
    public string SubcontractOrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid RmItemId { get; set; }
    public string RmItemCode { get; set; } = string.Empty;
    public string RmItemName { get; set; } = string.Empty;
    public decimal RequiredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public decimal PendingQty { get; set; }
    public Guid? ReserveWarehouseId { get; set; }
    public string? ReserveWarehouseName { get; set; }
}

public class SubcontractedRawMaterialsToBeTransferredSummaryDto
{
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalTransferredQty { get; set; }
    public decimal TotalReturnedQty { get; set; }
    public decimal TotalPendingQty { get; set; }
    public int TotalOrders { get; set; }
}

public class SubcontractedRawMaterialsToBeTransferredReportDto
{
    public List<SubcontractedRawMaterialsToBeTransferredRowDto> Rows { get; set; } = new();
    public SubcontractedRawMaterialsToBeTransferredSummaryDto Summary { get; set; } = new();
}
