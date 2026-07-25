using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Ledger Entry — immutable record of every stock movement.
/// Maps to ERPNext stock/doctype/stock_ledger_entry.
/// This is the source of truth for stock balances (calculated by summing ledger entries).
/// Per gotcha #649: posting_datetime composed from posting_date + posting_time.
/// Per gotcha #1246: PostgreSQL partial index on (company, posting_datetime, creation) WHERE is_cancelled=0.
/// </summary>
public class StockLedgerEntry : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    public DateTime PostingDate { get; set; }

    /// <summary>
    /// Posting time for same-date tie-breaking. Per gotcha #649:
    /// SLE ordering is by (posting_datetime, creation) — tie-breaking by creation is critical for FIFO.
    /// Format: TimeOnly stored as TimeSpan for EF Core compatibility.
    /// </summary>
    public TimeSpan PostingTime { get; set; }

    /// <summary>
    /// Combined posting_date + posting_time for ordering. Per gotcha #649:
    /// This is the primary sort field for SLE processing.
    /// </summary>
    public DateTime PostingDateTime { get; set; }

    /// <summary>Positive = stock in, Negative = stock out.</summary>
    public decimal QuantityChange { get; set; }

    /// <summary>Valuation rate per unit at time of posting.</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>QuantityChange * ValuationRate.</summary>
    public decimal StockValue { get; set; }

    /// <summary>
    /// Stock value difference from previous entry for this item+warehouse.
    /// Used by stock ageing and reporting. Per gotcha #358.
    /// </summary>
    public decimal StockValueDifference { get; set; }

    /// <summary>Running balance after this entry (denormalized for performance).</summary>
    public decimal BalanceQuantity { get; set; }

    /// <summary>Running stock value after this entry.</summary>
    public decimal BalanceValue { get; set; }

    /// <summary>JSON-serialized FIFO/LIFO queue state after this entry. Format: [[qty, rate], ...]</summary>
    public string? StockQueue { get; set; }

    /// <summary>Source document type (e.g., "StockEntry", "SalesInvoice").</summary>
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }

    /// <summary>
    /// Links to specific child item row of the source document.
    /// Per gotcha #192: GL merge uses voucher_detail_no as merge key.
    /// </summary>
    public Guid? VoucherDetailNo { get; set; }

    /// <summary>Batch reference for batch-tracked items.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>Serial number reference for serialized items.</summary>
    public Guid? SerialNoId { get; set; }

    /// <summary>Serial and Batch Bundle reference (v16 tracking).</summary>
    public Guid? SerialAndBatchBundleId { get; set; }

    /// <summary>
    /// Rate-only entry (no physical stock movement). Per gotcha #224:
    /// is_adjustment_entry=1 or via_landed_cost_voucher=1 → skip bundle validation.
    /// </summary>
    public bool IsAdjustmentEntry { get; set; }

    /// <summary>Set when entry is created via Landed Cost Voucher (rate-only revaluation).</summary>
    public bool ViaLandedCostVoucher { get; set; }

    /// <summary>Cancelled entries are kept for audit but excluded from balance calculations.</summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// Incoming rate for stock-in entries (used when recalculate_rate=true).
    /// Per gotcha #314: normal receipt items DON'T trigger FIFO/MA recalc — uses pre-computed rate.
    /// </summary>
    public decimal IncomingRate { get; set; }

    /// <summary>
    /// Outgoing rate for stock-out entries.
    /// Per gotcha #326: FIFO negative stock bin uses outgoing_rate or last consumed bin's rate.
    /// </summary>
    public decimal OutgoingRate { get; set; }

    /// <summary>
    /// When true, SLE processing recalculates valuation rate (FIFO/MA/LIFO).
    /// Per gotcha #314: only subcontracted items and internal transfers set this.
    /// </summary>
    public bool RecalculateRate { get; set; }

    /// <summary>Stock UOM for this item (denormalized from Item master per gotcha #292).</summary>
    public string? StockUom { get; set; }

    /// <summary>Has batch number flag (auto-synced from Item master per gotcha #292).</summary>
    public bool HasBatchNo { get; set; }

    /// <summary>Has serial number flag (auto-synced from Item master per gotcha #292).</summary>
    public bool HasSerialNo { get; set; }

    /// <summary>Fiscal year for this entry's posting date.</summary>
    public string? FiscalYear { get; set; }

    protected StockLedgerEntry() { }

    public StockLedgerEntry(
        Guid id, Guid companyId, Guid itemId, Guid warehouseId,
        DateTime postingDate, decimal quantityChange, decimal valuationRate,
        decimal balanceQuantity, decimal balanceValue, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        PostingDate = postingDate;
        PostingTime = postingDate.TimeOfDay;
        PostingDateTime = postingDate;
        QuantityChange = quantityChange;
        ValuationRate = valuationRate;
        StockValue = quantityChange * valuationRate;
        BalanceQuantity = balanceQuantity;
        BalanceValue = balanceValue;
        TenantId = tenantId;
    }

    public StockLedgerEntry(
        Guid id, Guid companyId, Guid itemId, Guid warehouseId,
        DateTime postingDate, TimeSpan postingTime,
        decimal quantityChange, decimal valuationRate,
        decimal balanceQuantity, decimal balanceValue,
        string? voucherType = null, Guid? voucherId = null,
        Guid? voucherDetailNo = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        PostingDate = postingDate;
        PostingTime = postingTime;
        PostingDateTime = postingDate.Date + postingTime;
        QuantityChange = quantityChange;
        ValuationRate = valuationRate;
        StockValue = quantityChange * valuationRate;
        StockValueDifference = quantityChange * valuationRate;
        BalanceQuantity = balanceQuantity;
        BalanceValue = balanceValue;
        VoucherType = voucherType;
        VoucherId = voucherId;
        VoucherDetailNo = voucherDetailNo;
        TenantId = tenantId;
    }
}
