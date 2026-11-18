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

public class IncorrectSerialNoValuationRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class IncorrectSerialNoValuationRowDto
{
    public Guid? CompanyId { get; set; }
    public Guid? StockLedgerEntryId { get; set; }
    public DateTime? PostingDate { get; set; }
    public TimeSpan? PostingTime { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? SerialNoId { get; set; }
    public string SerialNoNumber { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal ValuationRate { get; set; }
    public bool IsTotalRow { get; set; }
    public bool IsBalanceRow { get; set; }
}

public class IncorrectSerialNoValuationReportDto
{
    public List<IncorrectSerialNoValuationRowDto> Rows { get; set; } = new();
}

public class IncorrectSerialAndBatchBundleRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? VoucherType { get; set; }
    public DateTime? PostingDate { get; set; }
}

public class IncorrectSerialAndBatchBundleRowDto
{
    public Guid BundleId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public Guid VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid? VoucherDetailId { get; set; }
    public bool IsCancelled { get; set; }
    public string IssueType { get; set; } = string.Empty;
}

public class IncorrectSerialAndBatchBundleReportDto
{
    public List<IncorrectSerialAndBatchBundleRowDto> Rows { get; set; } = new();
}

public class FixSerialAndBatchBundlesInputDto
{
    public List<Guid> BundleIds { get; set; } = new();
}

public class FixSerialAndBatchBundlesResultDto
{
    public int CancelledCount { get; set; }
    public int RestoredCount { get; set; }
    public int SkippedCount { get; set; }
}

public class FifoQueueVsQtyComparisonRequestDto
{
    public Guid? CompanyId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class FifoQueueVsQtyComparisonRowDto
{
    public Guid StockLedgerEntryId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public TimeSpan PostingTime { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid? BatchId { get; set; }
    public bool UseBatchwiseValuation { get; set; }
    public decimal ActualQty { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public string StockQueue { get; set; } = string.Empty;
    public decimal FifoQueueQty { get; set; }
    public decimal FifoQtyDiff { get; set; }
    public decimal StockValue { get; set; }
    public decimal FifoStockValue { get; set; }
    public decimal FifoValueDiff { get; set; }
    public decimal ValuationRate { get; set; }
    public bool IsPrecedingRow { get; set; }
}

public class FifoQueueVsQtyComparisonReportDto
{
    public List<FifoQueueVsQtyComparisonRowDto> Rows { get; set; } = new();
}
