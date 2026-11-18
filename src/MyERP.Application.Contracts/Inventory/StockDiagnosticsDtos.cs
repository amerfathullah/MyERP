using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class IncorrectBalanceQtyRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public int Precision { get; set; } = 2;
}

public class IncorrectBalanceQtyRowDto
{
    public Guid StockLedgerEntryId { get; set; }
    public DateTime PostingDate { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal ExpectedBalanceQty { get; set; }
    public decimal ActualBalanceQty { get; set; }
    public decimal Difference { get; set; }
}

public class IncorrectBalanceQtyReportDto
{
    public List<IncorrectBalanceQtyRowDto> Rows { get; set; } = new();
}

public class IncorrectStockValueRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime? FromDate { get; set; }
}

public class IncorrectStockValueRowDto
{
    public Guid StockLedgerEntryId { get; set; }
    public DateTime PostingDate { get; set; }
    public TimeSpan PostingTime { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal ExpectedStockValue { get; set; }
    public decimal StockValue { get; set; }
    public decimal DifferenceValue { get; set; }
}

public class IncorrectStockValueReportDto
{
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime? ResolvedFromDate { get; set; }
    public List<IncorrectStockValueRowDto> Rows { get; set; } = new();
}
