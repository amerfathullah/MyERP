using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Blanket Order — framework agreement for a fixed quantity/amount with a customer/supplier.
/// Sales Orders and Purchase Orders draw from the blanket order allocation.
/// Quantity cannot exceed (qty × (1 + allowance_pct / 100)).
/// Maps to ERPNext selling/doctype/blanket_order.
/// </summary>
public class BlanketOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OrderNumber { get; set; } = null!;

    /// <summary>Selling or Buying.</summary>
    public string OrderType { get; set; } = "Selling";

    /// <summary>Customer (for Selling) or Supplier (for Buying).</summary>
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<BlanketOrderItem> _items = new();
    public IReadOnlyList<BlanketOrderItem> Items => _items.AsReadOnly();

    protected BlanketOrder() { }

    public BlanketOrder(Guid id, Guid companyId, string orderNumber,
        string orderType, Guid partyId, DateTime fromDate, DateTime toDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        OrderNumber = orderNumber;
        OrderType = orderType;
        PartyId = partyId;
        FromDate = fromDate;
        ToDate = toDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, decimal qty, decimal rate, string? itemName = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new BlanketOrderItem(Guid.NewGuid(), Id, itemId, qty, rate, itemName));
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
        Status = DocumentStatus.Cancelled;
    }
}

public class BlanketOrderItem : FullAuditedEntity<Guid>
{
    public Guid BlanketOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }

    /// <summary>Qty already ordered against this line.</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>Remaining qty = Qty - OrderedQty.</summary>
    public decimal RemainingQty => Qty - OrderedQty;

    protected BlanketOrderItem() { }

    public BlanketOrderItem(Guid id, Guid blanketOrderId, Guid itemId,
        decimal qty, decimal rate, string? itemName) : base(id)
    {
        BlanketOrderId = blanketOrderId;
        ItemId = itemId;
        Qty = qty;
        Rate = rate;
        ItemName = itemName;
    }

    /// <summary>Record an order against this blanket line. Validates allowance.</summary>
    public void RecordOrder(decimal qty, decimal allowancePct = 0)
    {
        var maxAllowed = Qty * (1 + allowancePct / 100);
        if (OrderedQty + qty > maxAllowed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", $"Exceeds blanket order allowance. Max: {maxAllowed}, Current: {OrderedQty}, Requested: {qty}");
        OrderedQty += qty;
    }
}
