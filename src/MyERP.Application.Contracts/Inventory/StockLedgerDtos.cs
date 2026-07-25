using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyERP.Inventory;

public class StockLedgerRequestDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
}

public class StockLedgerRowDto
{
    public DateTime PostingDate { get; set; }
    public string ItemName { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValue { get; set; }
    public decimal BalanceQuantity { get; set; }
    public decimal BalanceValue { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
}

public class StockLedgerReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<StockLedgerRowDto> Rows { get; set; } = new();
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
}

/// <summary>
/// Per-voucher stock ledger view (ERPNext "Stock Ledger" button on document detail pages).
/// </summary>
public class VoucherStockLedgerEntryDto
{
    public DateTime PostingDate { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal StockValueDifference { get; set; }
    public decimal BalanceQuantity { get; set; }
    public decimal BalanceValue { get; set; }
}

public class VoucherStockLedgerDto
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public List<VoucherStockLedgerEntryDto> Entries { get; set; } = new();
    public decimal TotalQtyIn { get; set; }
    public decimal TotalQtyOut { get; set; }
    public decimal TotalValueDifference { get; set; }
}
