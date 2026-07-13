using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Purchase Order — order placed to supplier.
/// Maps to ERPNext buying/doctype/purchase_order.
/// Flow: PurchaseOrder → PurchaseInvoice
/// </summary>
public class PurchaseOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public Guid SupplierId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Primary billing address (auto-resolved from Supplier on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Total advance payment made against this order.</summary>
    public decimal AdvancePaid { get; set; }

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Percentage of total qty received (0-100). Uses min per-item completion (all items must be received).</summary>
    public decimal PerReceived => _items.Count > 0
        ? Math.Round(_items.Min(i => i.Quantity > 0 ? i.ReceivedQty / i.Quantity * 100 : 100m), 2)
        : 0;

    /// <summary>Percentage of total amount billed (0-100).</summary>
    public decimal PerBilled => NetTotal > 0
        ? Math.Round(_items.Sum(i => i.BilledQty * i.UnitPrice) / NetTotal * 100, 2)
        : 0;

    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyList<PurchaseOrderItem> Items => _items.AsReadOnly();

    protected PurchaseOrder() { }

    public PurchaseOrder(Guid id, Guid companyId, Guid supplierId, string orderNumber, DateTime orderDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber), PurchaseOrderConsts.MaxOrderNumberLength);
        OrderDate = orderDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        _items.Add(new PurchaseOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.ToDeliverAndBill; // "To Receive and Bill"
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Manually close the PO (stops further fulfillment without cancelling).
    /// </summary>
    public void Close()
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled || Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>
    /// Reopen a closed PO for further fulfillment.
    /// </summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        UpdateFulfillmentStatus();
    }

    /// <summary>
    /// Recalculates fulfillment status based on received/billed quantities.
    /// Called after Purchase Receipt or Purchase Invoice submission.
    /// </summary>
    public void UpdateFulfillmentStatus()
    {
        if (Status == DocumentStatus.Cancelled || Status == DocumentStatus.Draft)
            return;

        var fullyReceived = PerReceived >= 100m;
        var fullyBilled = PerBilled >= 100m;

        if (fullyReceived && fullyBilled)
            Status = DocumentStatus.Completed;
        else if (fullyReceived)
            Status = DocumentStatus.ToBill;
        else if (fullyBilled)
            Status = DocumentStatus.ToDeliver; // "To Receive"
        else
            Status = DocumentStatus.ToDeliverAndBill;
    }

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
    }
}

public class PurchaseOrderItem : CreationAuditedEntity<Guid>
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Quantity already received via Purchase Receipts.</summary>
    public decimal ReceivedQty { get; set; }

    /// <summary>Quantity already billed via Purchase Invoices.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>Remaining qty to receive.</summary>
    public decimal PendingReceiptQty => Math.Max(0, Quantity - ReceivedQty);

    /// <summary>Remaining qty to bill.</summary>
    public decimal PendingBillingQty => Math.Max(0, Quantity - BilledQty);

    /// <summary>Target warehouse for receipt.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Link to Material Request item (for MR fulfillment tracking).</summary>
    public Guid? MaterialRequestItemId { get; set; }

    protected PurchaseOrderItem() { }
    public PurchaseOrderItem(Guid id, Guid purchaseOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        PurchaseOrderId = purchaseOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
