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
public class SalesOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
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

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Source quotation (if converted from quotation).</summary>
    public Guid? QuotationId { get; set; }

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
        _items.Add(new SalesOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
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

    protected SalesOrderItem() { }
    public SalesOrderItem(Guid id, Guid salesOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        SalesOrderId = salesOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
