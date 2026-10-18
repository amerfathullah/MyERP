using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class SerialNoLedgerFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? VoucherType { get; set; }
    public string? SearchTerm { get; set; }
}

public class SerialNoLedgerRowDto
{
    public DateTime PostingDateTime { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal ValuationRate { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PartyType { get; set; }
    public string? PartyName { get; set; }
}

public class SerialNoLedgerReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<SerialNoLedgerRowDto> Rows { get; set; } = new();
    public int TotalEntries => Rows.Count;
    public int InwardCount { get; set; }
    public int OutwardCount { get; set; }
}
