using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockGlComparisonRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? AsOfDate { get; set; }
}

public class StockGlComparisonDto
{
    public decimal TotalStockValue { get; set; }
    public decimal TotalGlBalance { get; set; }
    public decimal Difference { get; set; }
    public bool IsMatched { get; set; }
    public int WarehouseCount { get; set; }
    public int ItemCount { get; set; }
    public DateTime AsOfDate { get; set; }
    public List<StockGlWarehouseComparisonDto> PerWarehouse { get; set; } = new();
    public List<StockGlVoucherComparisonDto> MismatchedVouchers { get; set; } = new();
}

public class StockGlVoucherComparisonDto
{
    public string LedgerType { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string? VoucherNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public decimal StockValue { get; set; }
    public decimal GlBalance { get; set; }
    public decimal Difference { get; set; }
}

public class StockGlWarehouseComparisonDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal StockValue { get; set; }
    public decimal GlBalance { get; set; }
    public decimal Difference { get; set; }
    public bool HasMismatch { get; set; }
    public Guid? StockAccountId { get; set; }
    public string? StockAccountName { get; set; }
}

public class CreateGlRepostingInputDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public List<GlRepostVoucherDto> Vouchers { get; set; } = new();
}

public class GlRepostVoucherDto
{
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
}

public class CreateGlRepostingResultDto
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<Guid> RepostIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
