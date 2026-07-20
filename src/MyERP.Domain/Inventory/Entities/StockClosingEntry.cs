using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Closing Entry — period-end snapshot of stock balances for reporting optimization.
/// Stores pre-aggregated stock balances (qty, value, FIFO queue) per item+warehouse
/// so that Stock Balance reports can use the closing as opening instead of scanning
/// all SLE from the beginning of time.
/// 
/// Per ERPNext:
/// - Incremental: each closing builds on the previous one (reads prior closing + delta SLE)
/// - Blocks reposting for dates covered by the closing
/// - Submit/Cancel lifecycle
/// - Stock Balance Report detects the latest closing before from_date for optimization
/// 
/// Source: erpnext/stock/doctype/stock_closing_entry/stock_closing_entry.py
/// </summary>
public class StockClosingEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>The date through which stock is closed (inclusive).</summary>
    public DateTime ToDate { get; set; }

    /// <summary>Document status.</summary>
    public StockClosingStatus Status { get; private set; } = StockClosingStatus.Draft;

    /// <summary>Total number of item+warehouse combinations captured.</summary>
    public int TotalEntries { get; set; }

    /// <summary>Total stock value across all entries.</summary>
    public decimal TotalStockValue { get; set; }

    /// <summary>Reference to the previous closing entry this was built from (incremental).</summary>
    public Guid? PreviousClosingEntryId { get; set; }

    /// <summary>Date from which SLE was scanned (previous closing + 1 day, or beginning of time).</summary>
    public DateTime? ScannedFromDate { get; set; }

    /// <summary>Snapshot balances (one per item+warehouse combination).</summary>
    public ICollection<StockClosingBalance> Balances { get; private set; }
        = new List<StockClosingBalance>();

    protected StockClosingEntry() { }

    public StockClosingEntry(Guid id, Guid companyId, DateTime toDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ToDate = toDate;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a balance snapshot for an item+warehouse combination.
    /// </summary>
    public void AddBalance(
        Guid itemId,
        Guid warehouseId,
        decimal qty,
        decimal stockValue,
        decimal valuationRate,
        string? fifoQueue = null)
    {
        if (Status != StockClosingStatus.Draft)
            throw new BusinessException("MyERP:01001");

        Balances.Add(new StockClosingBalance(
            Guid.NewGuid(), Id, itemId, warehouseId, qty, stockValue, valuationRate, fifoQueue));
    }

    /// <summary>
    /// Submit the closing entry. Freezes the data and blocks future reposting for covered dates.
    /// </summary>
    public void Submit()
    {
        if (Status != StockClosingStatus.Draft)
            throw new BusinessException("MyERP:01001");

        if (!Balances.Any())
            throw new BusinessException("MyERP:05028")
                .WithData("detail", "Stock closing entry has no balance entries.");

        TotalEntries = Balances.Count;
        TotalStockValue = Balances.Sum(b => b.StockValue);
        Status = StockClosingStatus.Submitted;
    }

    /// <summary>
    /// Cancel the closing entry. Allows reposting for the previously-covered dates.
    /// </summary>
    public void Cancel()
    {
        if (Status != StockClosingStatus.Submitted)
            throw new BusinessException("MyERP:01001");

        Status = StockClosingStatus.Cancelled;
    }

    /// <summary>
    /// Check if a given date is covered by this closing entry.
    /// </summary>
    public bool CoversDate(DateTime date)
    {
        return Status == StockClosingStatus.Submitted && date <= ToDate;
    }
}

/// <summary>
/// Pre-aggregated stock balance for a specific item+warehouse at the closing date.
/// Used as opening balance by the Stock Balance report.
/// </summary>
public class StockClosingBalance : Entity<Guid>
{
    public Guid StockClosingEntryId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    /// <summary>Stock quantity at closing date.</summary>
    public decimal Qty { get; set; }

    /// <summary>Total stock value at closing date.</summary>
    public decimal StockValue { get; set; }

    /// <summary>Valuation rate (value / qty, or weighted average).</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>FIFO queue state as JSON (for FIFO/LIFO items). Null for Moving Average.</summary>
    public string? FifoQueue { get; set; }

    protected StockClosingBalance() { }

    public StockClosingBalance(Guid id, Guid closingEntryId, Guid itemId, Guid warehouseId,
        decimal qty, decimal stockValue, decimal valuationRate, string? fifoQueue = null)
        : base(id)
    {
        StockClosingEntryId = closingEntryId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        Qty = qty;
        StockValue = stockValue;
        ValuationRate = valuationRate;
        FifoQueue = fifoQueue;
    }
}

public enum StockClosingStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2
}
