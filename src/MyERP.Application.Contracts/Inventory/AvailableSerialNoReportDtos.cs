using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class AvailableSerialNoFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class AvailableSerialNoRowDto
{
    public Guid SleId { get; set; }
    public DateTime PostingDateTime { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? StockUom { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal InQty { get; set; }
    public decimal OutQty { get; set; }
    public decimal BalanceQty { get; set; }
    public List<string> SerialNosMoved { get; set; } = new();
    public List<string> BalanceSerialNos { get; set; } = new();
    public int BalanceSerialCount { get; set; }
    public decimal IncomingRate { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
    public decimal StockValueDifference { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherNo { get; set; }
    public bool HasDiscrepancy { get; set; }
}

public class AvailableSerialNoReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalInQty { get; set; }
    public decimal TotalOutQty { get; set; }
    public decimal FinalBalanceQty { get; set; }
    public int FinalAvailableSerialCount { get; set; }
    public int DiscrepancyCount { get; set; }
    public List<AvailableSerialNoRowDto> Rows { get; set; } = new();
}
