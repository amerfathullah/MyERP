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
public class PurchaseOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
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

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

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
        _items.Add(new PurchaseOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
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

    protected PurchaseOrderItem() { }
    public PurchaseOrderItem(Guid id, Guid purchaseOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        PurchaseOrderId = purchaseOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
