using System;
using System.Collections.Generic;

namespace MyERP.Inventory;

public class StockLedgerInvariantCheckFilterDto
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public bool ShowIncorrectEntries { get; set; }
}

public class StockLedgerInvariantCheckReportDto
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? StockUom { get; set; }
    public string ValuationMethod { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public int TotalEntries { get; set; }
    public int ViolationCount { get; set; }
    public int? FirstViolationIndex { get; set; }
    public List<StockLedgerInvariantRowDto> Rows { get; set; } = new();
}

public class StockLedgerInvariantRowDto
{
    public Guid SleId { get; set; }
    public DateTime PostingDateTime { get; set; }
    public DateTime CreationTime { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherNo { get; set; }
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }
    public Guid? SerialAndBatchBundleId { get; set; }
    public bool UseBatchwiseValuation { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal IncomingRate { get; set; }
    public decimal ConsumptionRate { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public decimal ExpectedQtyAfterTransaction { get; set; }
    public decimal DifferenceInQty { get; set; }
    public string? StockQueue { get; set; }
    public decimal? FifoQueueQty { get; set; }
    public decimal? FifoQtyDiff { get; set; }
    public decimal StockValue { get; set; }
    public decimal? FifoStockValue { get; set; }
    public decimal? FifoValueDiff { get; set; }
    public decimal StockValueDifference { get; set; }
    public decimal StockValueFromDiff { get; set; }
    public decimal DiffValueDiff { get; set; }
    public decimal? FifoStockDiff { get; set; }
    public decimal? FifoDifferenceDiff { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal? FifoValuationRate { get; set; }
    public decimal? FifoValuationDiff { get; set; }
    public decimal? BalanceValueByQty { get; set; }
    public decimal? ValuationDiff { get; set; }
    public bool IsInvariantViolated { get; set; }
}

public class StockLedgerVarianceFilterDto
{
    public Guid? CompanyId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? ItemId { get; set; }
    public bool ShowOnlyViolations { get; set; } = true;
}

public class StockLedgerVarianceReportDto
{
    public int TotalViolations { get; set; }
    public List<StockLedgerVarianceRowDto> Rows { get; set; } = new();
}

public class StockLedgerVarianceRowDto
{
    public Guid SleId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string ValuationMethod { get; set; } = string.Empty;
    public DateTime PostingDateTime { get; set; }
    public DateTime CreationTime { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public string? VoucherNo { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal QtyAfterTransaction { get; set; }
    public decimal ExpectedQtyAfterTransaction { get; set; }
    public decimal DifferenceInQty { get; set; }
    public decimal StockValue { get; set; }
    public decimal StockValueFromDiff { get; set; }
    public decimal DiffValueDiff { get; set; }
    public decimal ValuationRate { get; set; }
    public decimal? BalanceValueByQty { get; set; }
    public decimal? ValuationDiff { get; set; }
    public bool HasViolation { get; set; }
}
