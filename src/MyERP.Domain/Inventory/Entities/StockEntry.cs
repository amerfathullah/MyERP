using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Stock Entry — records stock movements (receipt, issue, transfer, adjustment).
/// Maps to ERPNext stock/doctype/stock_entry.
/// </summary>
public class StockEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? EntryNumber { get; set; }
    public StockEntryType EntryType { get; set; }
    public DateTime PostingDate { get; set; }
    DateTime IAccountableDocument.PostingDate => PostingDate;

    /// <summary>Source document type (e.g., "SalesInvoice", "PurchaseInvoice").</summary>
    public string? ReferenceType { get; set; }

    /// <summary>Linked Work Order ID (for material transfer/manufacture stock entries).</summary>
    public Guid? WorkOrderId { get; set; }

    /// <summary>Linked Subcontracting Order ID (for SendToSubcontractor stock entries).</summary>
    public Guid? SubcontractingOrderId { get; set; }

    /// <summary>Source Manufacture Stock Entry ID (for Disassemble purpose — links to the original production).</summary>
    public Guid? SourceStockEntryId { get; set; }

    /// <summary>Finished goods quantity produced/consumed (Manufacture/Disassemble).</summary>
    public decimal FgCompletedQty { get; set; }

    /// <summary>Process loss quantity in this stock entry.</summary>
    public decimal ProcessLossQty { get; set; }

    /// <summary>Process loss percentage (for Manufacture/Repack).</summary>
    public decimal ProcessLossPercentage { get; set; }

    // IAccountableDocument implementation
    string IAccountableDocument.DocumentType => "StockEntry";
    public string CurrencyCode { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1m;
    decimal IAccountableDocument.NetTotal => _items.Sum(i => i.Quantity * (i.ValuationRate ?? 0));
    decimal IAccountableDocument.GrandTotal => _items.Sum(i => i.Quantity * (i.ValuationRate ?? 0));
    decimal IAccountableDocument.TaxAmount => 0;
    Guid? IAccountableDocument.CustomerId => null;
    Guid? IAccountableDocument.SupplierId => null;
    public Guid? ReferenceId { get; set; }

    public string? Notes { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Total value of incoming (stock-in) items. Computed from items with TargetWarehouseId.</summary>
    public decimal TotalIncomingValue => _items
        .Where(i => i.TargetWarehouseId.HasValue)
        .Sum(i => i.Quantity * (i.ValuationRate ?? 0));

    /// <summary>Total value of outgoing (stock-out) items. Computed from items with SourceWarehouseId.</summary>
    public decimal TotalOutgoingValue => _items
        .Where(i => i.SourceWarehouseId.HasValue)
        .Sum(i => i.Quantity * (i.ValuationRate ?? 0));

    /// <summary>Net value difference (incoming - outgoing). Zero for balanced transfers.</summary>
    public decimal TotalValueDifference => TotalIncomingValue - TotalOutgoingValue;

    private readonly List<StockEntryItem> _items = new();
    public IReadOnlyList<StockEntryItem> Items => _items.AsReadOnly();

    protected StockEntry() { }

    public StockEntry(Guid id, Guid companyId, StockEntryType entryType, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        EntryType = entryType;
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, decimal quantity, Guid? sourceWarehouseId, Guid? targetWarehouseId, decimal? valuationRate = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        _items.Add(new StockEntryItem(
            Guid.NewGuid(), Id, itemId, quantity, sourceWarehouseId, targetWarehouseId, valuationRate));
    }

    /// <summary>Clear all items (Draft only). Used during edit to replace items.</summary>
    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Validate source and target warehouses cannot be identical (gotcha #6179)
        foreach (var item in _items)
        {
            if (item.SourceWarehouseId.HasValue && item.TargetWarehouseId.HasValue
                && item.SourceWarehouseId.Value == item.TargetWarehouseId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", "Source Warehouse and Target Warehouse cannot be the same.");
            }
        }

        Status = DocumentStatus.Submitted;
    }

    public void Post()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Posted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Posted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}
