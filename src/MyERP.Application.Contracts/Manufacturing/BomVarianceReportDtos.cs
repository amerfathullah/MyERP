using System;
using System.Collections.Generic;

namespace MyERP.Manufacturing;

public class BomVarianceReportRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid? BomId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public bool OnlyOverproduced { get; set; } = true;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class BomVarianceReportDto
{
    public Guid CompanyId { get; set; }
    public List<BomVarianceReportRowDto> Rows { get; set; } = new();
    public int TotalWorkOrders { get; set; }
    public decimal TotalOrderedQty { get; set; }
    public decimal TotalProducedQty { get; set; }
    public decimal TotalRequiredQty { get; set; }
    public decimal TotalConsumedQty { get; set; }
    public decimal TotalVarianceQty { get; set; }
}

public class BomVarianceReportRowDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = null!;
    public Guid BomId { get; set; }
    public string BomNumber { get; set; } = null!;
    public Guid FinishedGoodItemId { get; set; }
    public string FinishedGoodItemCode { get; set; } = null!;
    public string FinishedGoodItemName { get; set; } = null!;
    public decimal OrderedQty { get; set; }
    public decimal ProducedQty { get; set; }

    public Guid RawMaterialItemId { get; set; }
    public string RawMaterialItemCode { get; set; } = null!;
    public string RawMaterialItemName { get; set; } = null!;
    public decimal RequiredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public decimal VarianceQty { get; set; }
}
