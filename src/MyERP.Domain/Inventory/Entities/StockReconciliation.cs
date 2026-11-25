using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Reconciliation — adjusts physical stock quantities and valuations to match actual counts.
/// Creates SLEs with absolute qty (not delta). Standard Cost items require identical rates.
/// Maps to ERPNext stock/doctype/stock_reconciliation.
/// </summary>
public class StockReconciliation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? ReconciliationNumber { get; set; }
    public DateTime PostingDate { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }

    /// <summary>Expense account for valuation difference GL posting.</summary>
    public Guid? ExpenseAccountId { get; set; }
    public Guid? CostCenterId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public decimal DifferenceAmount { get; private set; }

    private readonly List<StockReconciliationItem> _items = new();
    public IReadOnlyList<StockReconciliationItem> Items => _items.AsReadOnly();

    protected StockReconciliation() { }

    public StockReconciliation(Guid id, Guid companyId, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, Guid warehouseId, decimal newQuantity, decimal newValuationRate,
        decimal currentQuantity = 0, decimal currentValuationRate = 0, string? stockUom = null,
        Guid? serialAndBatchBundleId = null, Guid? currentSerialAndBatchBundleId = null,
        bool allowZeroValuationRate = false)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var item = new StockReconciliationItem(Guid.NewGuid(), Id, itemId, warehouseId,
            newQuantity, newValuationRate, currentQuantity, currentValuationRate, stockUom,
            serialAndBatchBundleId, currentSerialAndBatchBundleId, allowZeroValuationRate);
        _items.Add(item);
        RecalculateDifference();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        RecalculateDifference();
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    private void RecalculateDifference()
    {
        DifferenceAmount = _items.Sum(i => i.DifferenceAmount);
    }

    /// <summary>
    /// Converts a buying Item Price rate to the item's Stock UOM for Stock Reconciliation valuation
    /// per ERPNext PR #67443 (<c>stock_reconciliation.py</c>):
    /// divides <paramref name="priceListRate"/> by <paramref name="priceUomConversionFactor"/> when positive,
    /// keeping the result UNROUNDED so sub-cent unit rates (e.g. 1 per Box of 1,000 Nos = 0.001) are not
    /// rounded down to zero.
    /// </summary>
    public static decimal ConvertBuyingItemPriceToStockUomRate(
        decimal priceListRate,
        decimal? priceUomConversionFactor)
    {
        if (priceUomConversionFactor.HasValue && priceUomConversionFactor.Value > 0m)
            return priceListRate / priceUomConversionFactor.Value;

        return priceListRate;
    }

    /// <summary>
    /// Resolves the valuation rate for a Stock Reconciliation row with a selected Serial and Batch Bundle
    /// per ERPNext PR #67699 (<c>stock_reconciliation.py</c>):
    /// keeps an explicit <c>0</c> valuation rate ONLY when <paramref name="allowZeroValuationRate"/> is true;
    /// otherwise when <paramref name="valuationRate"/> is unset or <c>0</c>, falls back to
    /// <paramref name="currentValuationRate"/>.
    /// </summary>
    public static decimal ResolveSelectedBundleValuationRate(
        decimal? valuationRate,
        decimal currentValuationRate,
        bool allowZeroValuationRate)
    {
        var keepZeroRate = valuationRate.HasValue && valuationRate.Value == 0m && allowZeroValuationRate;
        if ((!valuationRate.HasValue || valuationRate.Value == 0m) && !keepZeroRate)
        {
            return currentValuationRate;
        }

        return valuationRate!.Value;
    }

    /// <summary>
    /// Refreshes a Stock Reconciliation row's book qty and valuation rate when posting date/time changes
    /// per ERPNext PR #67442 (<c>stock_reconciliation.js: set_valuation_rate_and_qty</c>):
    /// - When <paramref name="keepCountedValues"/> is true, any field the user edited away from the prior
    ///   book value (compared at precision) is preserved, while untouched fields follow the new book value.
    /// - In scan mode, counted qty is preserved and <c>ResetSerialNo</c> is false; <c>ResetSerialNo</c> is
    ///   also false whenever counted qty was kept (<c>keepQty</c>).
    /// </summary>
    public static StockReconciliationPostingTimeRefreshResult ResolveReconciliationRowOnPostingTimeRefresh(
        decimal currentRowQty,
        decimal previousBookQty,
        decimal currentRowRate,
        decimal previousBookRate,
        decimal newBookQty,
        decimal newBookRate,
        bool isScanMode,
        bool keepCountedValues,
        bool isStockReconciliationPurpose = true,
        int qtyPrecision = 4,
        int ratePrecision = 4)
    {
        var qtyChanged = Math.Round(currentRowQty, qtyPrecision, MidpointRounding.AwayFromZero)
            != Math.Round(previousBookQty, qtyPrecision, MidpointRounding.AwayFromZero);
        var rateChanged = Math.Round(currentRowRate, ratePrecision, MidpointRounding.AwayFromZero)
            != Math.Round(previousBookRate, ratePrecision, MidpointRounding.AwayFromZero);

        var keepQty = isScanMode || (keepCountedValues && qtyChanged);
        var keepRate = keepCountedValues && rateChanged;

        var resolvedQty = keepQty ? currentRowQty : newBookQty;
        var resolvedRate = keepRate ? currentRowRate : newBookRate;
        var currentAmount = newBookRate * newBookQty;
        var amount = resolvedQty * resolvedRate;
        var resetSerialNo = isStockReconciliationPurpose && !isScanMode && !keepQty;

        return new StockReconciliationPostingTimeRefreshResult(
            Qty: resolvedQty,
            ValuationRate: resolvedRate,
            CurrentQty: newBookQty,
            CurrentValuationRate: newBookRate,
            CurrentAmount: currentAmount,
            Amount: amount,
            ResetSerialNo: resetSerialNo);
    }
}

public readonly record struct StockReconciliationPostingTimeRefreshResult(
    decimal Qty,
    decimal ValuationRate,
    decimal CurrentQty,
    decimal CurrentValuationRate,
    decimal CurrentAmount,
    decimal Amount,
    bool ResetSerialNo);
