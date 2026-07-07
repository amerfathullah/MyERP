using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Ledger Entry — immutable record of every stock movement.
/// Maps to ERPNext stock/doctype/stock_ledger_entry.
/// This is the source of truth for stock balances (calculated by summing ledger entries).
/// </summary>
public class StockLedgerEntry : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    public DateTime PostingDate { get; set; }

    /// <summary>Positive = stock in, Negative = stock out.</summary>
    public decimal QuantityChange { get; set; }

    /// <summary>Valuation rate per unit at time of posting.</summary>
    public decimal ValuationRate { get; set; }

    /// <summary>QuantityChange * ValuationRate.</summary>
    public decimal StockValue { get; set; }

    /// <summary>Running balance after this entry (denormalized for performance).</summary>
    public decimal BalanceQuantity { get; set; }

    /// <summary>Running stock value after this entry.</summary>
    public decimal BalanceValue { get; set; }

    /// <summary>Source document type (e.g., "StockEntry", "SalesInvoice").</summary>
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }

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
        QuantityChange = quantityChange;
        ValuationRate = valuationRate;
        StockValue = quantityChange * valuationRate;
        BalanceQuantity = balanceQuantity;
        BalanceValue = balanceValue;
        TenantId = tenantId;
    }
}
