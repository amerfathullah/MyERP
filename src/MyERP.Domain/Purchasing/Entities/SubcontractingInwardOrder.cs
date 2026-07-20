using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Subcontracting Inward Order (SCIO) — tracks receipt of subcontracted finished goods.
/// Created from Sales Order when items are subcontracted.
/// Blocks SO item updates while SCIO exists. SO close cascades to SCIO.
/// Maps to ERPNext subcontracting/doctype/subcontracting_inward_order.
/// </summary>
public class SubcontractingInwardOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>Linked Sales Order (SO close cascades to SCIO).</summary>
    public Guid? SalesOrderId { get; set; }

    /// <summary>Linked Subcontracting Order for RM supply tracking.</summary>
    public Guid? SubcontractingOrderId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1;

    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public SubcontractingInwardOrderStatus Status { get; private set; } = SubcontractingInwardOrderStatus.Draft;

    /// <summary>Percentage received (Min across items).</summary>
    public decimal PerReceived { get; set; }

    /// <summary>Percentage billed (Min across items).</summary>
    public decimal PerBilled { get; set; }

    public string? Notes { get; set; }

    public ICollection<SubcontractingInwardOrderItem> Items { get; private set; } = new List<SubcontractingInwardOrderItem>();

    protected SubcontractingInwardOrder() { }

    public SubcontractingInwardOrder(Guid id, Guid companyId, string orderNumber, DateTime orderDate,
        Guid supplierId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        OrderNumber = orderNumber;
        OrderDate = orderDate;
        TenantId = tenantId;
    }

    public void AddItem(SubcontractingInwardOrderItem item)
    {
        if (Status != SubcontractingInwardOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Items.Add(item);
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != SubcontractingInwardOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!Items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        Status = SubcontractingInwardOrderStatus.Open;
    }

    public void UpdateReceivedStatus()
    {
        if (!Items.Any()) return;
        PerReceived = Items.Min(i => i.Quantity > 0 ? (i.ReceivedQty / i.Quantity) * 100m : 0);
        if (PerReceived >= 100m)
            Status = SubcontractingInwardOrderStatus.Completed;
        else if (Items.Any(i => i.ReceivedQty > 0))
            Status = SubcontractingInwardOrderStatus.PartiallyReceived;
    }

    public void UpdateBilledStatus()
    {
        if (!Items.Any()) return;
        PerBilled = Items.Min(i => i.Quantity > 0 ? (i.BilledQty / i.Quantity) * 100m : 0);
    }

    public void Close()
    {
        if (Status == SubcontractingInwardOrderStatus.Draft ||
            Status == SubcontractingInwardOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingInwardOrderStatus.Closed;
    }

    public void Cancel()
    {
        if (Status == SubcontractingInwardOrderStatus.Draft ||
            Status == SubcontractingInwardOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingInwardOrderStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        NetTotal = Items.Sum(i => i.Amount);
        GrandTotal = NetTotal;
    }

    /// <summary>Max producible quantity for a given FG item based on available RM.</summary>
    public decimal GetMaxProducibleQty(Guid itemId)
    {
        var item = Items.FirstOrDefault(i => i.ItemId == itemId);
        return item?.Quantity ?? 0;
    }
}

/// <summary>FG item expected from subcontracting.</summary>
public class SubcontractingInwardOrderItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SubcontractingInwardOrderId { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>BOM used for manufacturing this item.</summary>
    public Guid? BomId { get; set; }

    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Quantity * Rate;

    /// <summary>Qty received so far.</summary>
    public decimal ReceivedQty { get; set; }

    /// <summary>Qty billed so far.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>Pending receipt qty.</summary>
    public decimal PendingReceiptQty => Math.Max(0, Quantity - ReceivedQty);

    /// <summary>Target warehouse for received goods.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Service cost per unit from subcontractor.</summary>
    public decimal ServiceCostPerQty { get; set; }

    protected SubcontractingInwardOrderItem() { }

    public SubcontractingInwardOrderItem(Guid id, Guid subcontractingInwardOrderId, Guid itemId,
        decimal quantity, decimal rate, Guid? tenantId = null)
        : base(id)
    {
        SubcontractingInwardOrderId = subcontractingInwardOrderId;
        ItemId = itemId;
        Quantity = quantity;
        Rate = rate;
        TenantId = tenantId;
    }
}

public enum SubcontractingInwardOrderStatus
{
    Draft = 0,
    Open = 1,
    PartiallyReceived = 2,
    Completed = 3,
    Closed = 4,
    Cancelled = 5
}
