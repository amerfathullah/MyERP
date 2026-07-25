using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Pick List — warehouse picking document for SO→DN fulfillment or MR→SE transfer.
/// Supports partial transfers (transferred_qty per row, multi-SE support).
/// Maps to ERPNext stock/doctype/pick_list.
/// </summary>
public class PickList : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string? PickListNumber { get; set; }

    /// <summary>Purpose: Delivery, Material Transfer, Material Transfer for Manufacture.</summary>
    public string Purpose { get; set; } = "Delivery";

    /// <summary>Parent Sales Order (for Delivery purpose).</summary>
    public Guid? SalesOrderId { get; set; }

    /// <summary>Parent Material Request (for Transfer purpose).</summary>
    public Guid? MaterialRequestId { get; set; }

    /// <summary>Parent Work Order (for Manufacture purpose).</summary>
    public Guid? WorkOrderId { get; set; }

    /// <summary>Customer for the pick list (used when creating DN without SO reference).</summary>
    public Guid? CustomerId { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<PickListItem> _items = new();
    public IReadOnlyList<PickListItem> Items => _items.AsReadOnly();

    protected PickList() { }

    public PickList(Guid id, Guid companyId, string purpose, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Purpose = purpose;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, Guid warehouseId, decimal qty,
        decimal stockQty = 0, string? itemName = null, Guid? batchId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new PickListItem(Guid.NewGuid(), Id, itemId, warehouseId, qty, stockQty, itemName, batchId));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        // Cannot cancel if any item has been transferred
        if (_items.Any(i => i.TransferredQty > 0))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Cannot cancel: items already transferred");
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>Check if all items are fully transferred.</summary>
    public bool IsFullyTransferred => _items.All(i => i.TransferredQty >= i.Qty);

    /// <summary>Check if partially transferred.</summary>
    public bool IsPartiallyTransferred => _items.Any(i => i.TransferredQty > 0) && !IsFullyTransferred;
}

public class PickListItem : FullAuditedEntity<Guid>
{
    public Guid PickListId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BatchId { get; set; }

    public decimal Qty { get; set; }
    public decimal StockQty { get; set; }

    /// <summary>Qty already transferred to Stock Entry (supports partial).</summary>
    public decimal TransferredQty { get; set; }

    /// <summary>Pending = Qty - TransferredQty (for next SE creation).</summary>
    public decimal PendingQty => Qty - TransferredQty;

    /// <summary>Source document row link (SO Item, MR Item).</summary>
    public Guid? SourceDocumentItemId { get; set; }

    protected PickListItem() { }

    public PickListItem(Guid id, Guid pickListId, Guid itemId, Guid warehouseId,
        decimal qty, decimal stockQty, string? itemName, Guid? batchId) : base(id)
    {
        PickListId = pickListId;
        ItemId = itemId;
        WarehouseId = warehouseId;
        Qty = qty;
        StockQty = stockQty > 0 ? stockQty : qty;
        ItemName = itemName;
        BatchId = batchId;
    }

    public void RecordTransfer(decimal qty)
    {
        if (qty <= 0) throw new ArgumentException("Qty must be positive.");
        if (TransferredQty + qty > Qty)
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("pending", PendingQty).WithData("requested", qty);
        TransferredQty += qty;
    }
}
