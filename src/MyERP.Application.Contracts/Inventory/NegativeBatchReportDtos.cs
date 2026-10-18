using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class NegativeBatchFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? BatchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class NegativeBatchRowDto
{
    public DateTime PostingDateTime { get; set; }
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal PreviousQty { get; set; }
    public decimal TransactionQty { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
}

public class NegativeBatchReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<NegativeBatchRowDto> Rows { get; set; } = new();
    public int TotalIncidents => Rows.Count;
    public int DistinctNegativeBatches { get; set; }
}
