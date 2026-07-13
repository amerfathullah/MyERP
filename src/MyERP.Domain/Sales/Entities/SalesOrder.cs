using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Order — confirmed order from customer.
/// Maps to ERPNext selling/doctype/sales_order.
/// Flow: Quotation → SalesOrder → SalesInvoice
/// </summary>
public class SalesOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Customer's own PO reference number.</summary>
    public string? CustomerPoNumber { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Primary billing address (auto-resolved from Customer on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Shipping/delivery address (auto-resolved from Customer on create).</summary>
    public Guid? ShippingAddressId { get; set; }

    /// <summary>Total advance payment received against this order.</summary>
    public decimal AdvancePaid { get; set; }

    /// <summary>Percentage of advance paid: (AdvancePaid / GrandTotal) × 100.</summary>
    public decimal PerAdvancePaid => GrandTotal > 0 ? Math.Round(AdvancePaid / GrandTotal * 100m, 2) : 0;

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // Amendment (cancel-and-amend workflow)
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    /// <summary>Source quotation (if converted from quotation).</summary>
    public Guid? QuotationId { get; set; }

    /// <summary>Percentage of total qty delivered (0-100). Uses min per-item completion (all items must be fulfilled).</summary>
    public decimal PerDelivered => _items.Count > 0
        ? Math.Round(_items.Min(i => i.Quantity > 0 ? i.DeliveredQty / i.Quantity * 100 : 100m), 2)
        : 0;

    /// <summary>Percentage of total amount billed (0-100).</summary>
    public decimal PerBilled => NetTotal > 0
        ? Math.Round(_items.Sum(i => i.BilledQty * i.UnitPrice) / NetTotal * 100, 2)
        : 0;

    private readonly List<SalesOrderItem> _items = new();
    public IReadOnlyList<SalesOrderItem> Items => _items.AsReadOnly();

    protected SalesOrder() { }

    public SalesOrder(Guid id, Guid companyId, Guid customerId, string orderNumber, DateTime orderDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber), SalesOrderConsts.MaxOrderNumberLength);
        OrderDate = orderDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        _items.Add(new SalesOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.ToDeliverAndBill;
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Manually close the SO (stops further fulfillment without cancelling).
    /// Used when remaining items won't be delivered/billed (short-close).
    /// </summary>
    public void Close()
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled || Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>
    /// Reopen a closed SO for further fulfillment.
    /// </summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        UpdateFulfillmentStatus(); // recalculate correct status from qty fields
    }

    /// <summary>
    /// Recalculates fulfillment status based on delivered/billed quantities.
    /// Called after Delivery Note or Sales Invoice submission.
    /// </summary>
    public void UpdateFulfillmentStatus()
    {
        if (Status == DocumentStatus.Cancelled || Status == DocumentStatus.Draft)
            return;

        var fullyDelivered = PerDelivered >= 100m;
        var fullyBilled = PerBilled >= 100m;

        if (fullyDelivered && fullyBilled)
            Status = DocumentStatus.Completed;
        else if (fullyDelivered)
            Status = DocumentStatus.ToBill;
        else if (fullyBilled)
            Status = DocumentStatus.ToDeliver;
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

public class SalesOrderItem : CreationAuditedEntity<Guid>
{
    public Guid SalesOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Quantity already delivered via Delivery Notes.</summary>
    public decimal DeliveredQty { get; set; }

    /// <summary>Quantity already invoiced via Sales Invoices.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>Remaining qty to deliver.</summary>
    public decimal PendingDeliveryQty => Math.Max(0, Quantity - DeliveredQty);

    /// <summary>Remaining qty to bill.</summary>
    public decimal PendingBillingQty => Math.Max(0, Quantity - BilledQty);

    /// <summary>Target warehouse for this item (for stock reservation).</summary>
    public Guid? WarehouseId { get; set; }

    protected SalesOrderItem() { }
    public SalesOrderItem(Guid id, Guid salesOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        SalesOrderId = salesOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
