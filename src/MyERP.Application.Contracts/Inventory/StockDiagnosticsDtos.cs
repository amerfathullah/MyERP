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

public static class StockValuationComparisonShowModes
{
    public const string FirstDifferencePerItemWarehouse = "First Difference per Item-Warehouse";
    public const string AllDifferences = "All Differences";
    public const string AllEntries = "All Entries";
}

public class StockValuationComparisonRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Show { get; set; }
    public decimal? Tolerance { get; set; }
    public bool ShowAdjustedDifferences { get; set; }
}

public class StockValuationComparisonRowDto
{
    public Guid StockLedgerEntryId { get; set; }
    public DateTime PostingDate { get; set; }
    public TimeSpan PostingTime { get; set; }
    public DateTime PostingDateTime { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public Guid? VoucherId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public Guid? SerialAndBatchBundleId { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public decimal ActualQty { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public decimal ExpectedQtyAfterTransaction { get; set; }
    public decimal QtyDifference { get; set; }
    public decimal StockValueDifference { get; set; }
    public decimal ExpectedStockValueDifference { get; set; }
    public decimal StockValueDifferenceDifference { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal ExpectedValuationRate { get; set; }
    public decimal ValuationRateDifference { get; set; }
    public decimal StockValue { get; set; }
    public decimal ExpectedStockValue { get; set; }
    public decimal StockValueDifferenceInBalance { get; set; }
    public string ExpectedBasis { get; set; } = string.Empty;
    public bool HasDifference { get; set; }
    public string? AdjustedBy { get; set; }
    public Guid? PendingRepostId { get; set; }
}

public class StockValuationComparisonReportDto
{
    public Guid CompanyId { get; set; }
    public decimal Tolerance { get; set; }
    public string Show { get; set; } = StockValuationComparisonShowModes.FirstDifferencePerItemWarehouse;
    public int TotalEntriesEvaluated { get; set; }
    public int DifferenceCount { get; set; }
    public List<StockValuationComparisonRowDto> Rows { get; set; } = new();
}

public class StockValuationComparisonRepostRequestDto
{
    public Guid CompanyId { get; set; }
    public List<Guid> StockLedgerEntryIds { get; set; } = new();
}

public class StockValuationComparisonRepostPreviewRowDto
{
    public Guid StockLedgerEntryId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public TimeSpan PostingTime { get; set; }
    public bool IsInClosedPeriod { get; set; }
    public Guid? PendingRepostId { get; set; }
}

public class StockValuationComparisonRepostItemResultDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public Guid? RepostId { get; set; }
    public string? Reason { get; set; }
}

public class StockValuationComparisonRepostResultDto
{
    public List<StockValuationComparisonRepostItemResultDto> Created { get; set; } = new();
    public List<StockValuationComparisonRepostItemResultDto> Skipped { get; set; } = new();
}
