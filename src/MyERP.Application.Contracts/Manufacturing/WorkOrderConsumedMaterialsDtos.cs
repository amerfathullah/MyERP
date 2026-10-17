using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class WorkOrderConsumedMaterialsRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? ProductionItemId { get; set; }
    public WorkOrderStatus? Status { get; set; }
    public bool ShowExtraConsumedMaterialsOnly { get; set; }
}

public class WorkOrderConsumedMaterialsReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<WorkOrderConsumedMaterialsRowDto> Rows { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public int TotalRawMaterialLines { get; set; }
    public int TotalExtraConsumedItems { get; set; }
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalConsumedQty { get; set; }
    public decimal TotalExtraConsumedQty { get; set; }
    public decimal TotalReturnedQty { get; set; }
}

public class WorkOrderConsumedMaterialsRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid ProductionItemId { get; set; }
    public string ProductionItemCode { get; set; } = null!;
    public string ProductionItemName { get; set; } = null!;
    public decimal QtyToProduce { get; set; }
    public decimal ProducedQty { get; set; }

    public Guid RawMaterialItemId { get; set; }
    public string RawMaterialItemCode { get; set; } = null!;
    public string RawMaterialName { get; set; } = null!;
    public string StockUom { get; set; } = "Unit";
    public decimal RequiredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public decimal ExtraConsumedQty { get; set; }
    public decimal ReturnedQty { get; set; }
    public bool IsAdditionalItem { get; set; }
}
