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
        decimal currentQuantity = 0, decimal currentValuationRate = 0)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var item = new StockReconciliationItem(Guid.NewGuid(), Id, itemId, warehouseId,
            newQuantity, newValuationRate, currentQuantity, currentValuationRate);
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
}
