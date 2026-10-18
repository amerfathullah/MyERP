using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockLedgerEntriesOfCancelledVouchersFilterDto
{
    public Guid? CompanyId { get; set; }
    public string? VoucherType { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class CancelledVoucherKeyDto
{
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
}

public class FixUncancelledEntriesInputDto
{
    public List<CancelledVoucherKeyDto> VoucherKeys { get; set; } = new();
}

public class StockLedgerEntryOfCancelledVoucherRowDto
{
    public Guid SleId { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public TimeSpan PostingTime { get; set; }
    public DateTime PostingDateTime { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal StockValueDifference { get; set; }
    public Guid? SerialAndBatchBundleId { get; set; }
    public string? SerialAndBatchBundleName { get; set; }
    public bool IsBundleActive { get; set; }
}

public class StockLedgerEntriesOfCancelledVouchersReportDto
{
    public int TotalUncancelledEntries { get; set; }
    public int DistinctCancelledVouchersCount { get; set; }
    public decimal TotalDiscrepancyQty { get; set; }
    public decimal TotalDiscrepancyValue { get; set; }
    public List<StockLedgerEntryOfCancelledVoucherRowDto> Rows { get; set; } = new();
}

public class FixUncancelledEntriesResultDto
{
    public int CancelledSleCount { get; set; }
    public int CancelledBundleCount { get; set; }
    public int RepostItemValuationCreatedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}
