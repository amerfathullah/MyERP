using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Subcontracting Order — order placed to a subcontractor to manufacture finished goods
/// using raw materials supplied by the company.
/// Flow: PO (with subcontract items) → SCO → Supply RM → Receive FG via SubcontractingReceipt.
/// </summary>
public class SubcontractingOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>Source Purchase Order (if created from PO).</summary>
    public Guid? PurchaseOrderId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal GrandTotal { get; set; }

    public SubcontractingOrderStatus Status { get; private set; } = SubcontractingOrderStatus.Draft;

    /// <summary>Percentage of items received (0-100).</summary>
    public decimal PerReceived { get; set; }

    public string? Notes { get; set; }

    private readonly List<SubcontractingOrderItem> _items = new();
    public IReadOnlyList<SubcontractingOrderItem> Items => _items.AsReadOnly();

    private readonly List<SubcontractingOrderSuppliedItem> _suppliedItems = new();
    public IReadOnlyList<SubcontractingOrderSuppliedItem> SuppliedItems => _suppliedItems.AsReadOnly();

    protected SubcontractingOrder() { }

    public SubcontractingOrder(Guid id, Guid companyId, string orderNumber, DateTime orderDate,
        Guid supplierId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        OrderNumber = orderNumber;
        OrderDate = orderDate;
        SupplierId = supplierId;
        TenantId = tenantId;
    }

    public void AddItem(SubcontractingOrderItem item)
    {
        if (Status != SubcontractingOrderStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(item);
        RecalculateTotals();
    }

    public void AddSuppliedItem(SubcontractingOrderSuppliedItem item)
    {
        _suppliedItems.Add(item);
    }

    public void Submit()
    {
        if (Status != SubcontractingOrderStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingOrderStatus.Open;
    }

    public void Cancel()
    {
        if (Status == SubcontractingOrderStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingOrderStatus.Cancelled;
    }

    public void Close()
    {
        if (Status is not (SubcontractingOrderStatus.Open or SubcontractingOrderStatus.PartiallyReceived))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingOrderStatus.Closed;
    }

    public void MarkPartiallyReceived()
    {
        if (Status is not SubcontractingOrderStatus.Open)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingOrderStatus.PartiallyReceived;
    }

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.Amount);
        GrandTotal = NetTotal;
    }
}

/// <summary>Finished good item to be manufactured by subcontractor.</summary>
public class SubcontractingOrderItem : Entity<Guid>
{
    public Guid SubcontractingOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;
    public decimal ReceivedQty { get; set; }
    public Guid? BomId { get; set; }
    public Guid? WarehouseId { get; set; }

    protected SubcontractingOrderItem() { }
    public SubcontractingOrderItem(Guid id, Guid scoId, Guid itemId, string itemName, decimal qty, decimal rate)
        : base(id)
    {
        SubcontractingOrderId = scoId; ItemId = itemId; ItemName = itemName; Qty = qty; Rate = rate;
    }
}

/// <summary>Raw material supplied to subcontractor.</summary>
public class SubcontractingOrderSuppliedItem : Entity<Guid>
{
    public Guid SubcontractingOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal RequiredQty { get; set; }
    public decimal TransferredQty { get; set; }
    public decimal ConsumedQty { get; set; }
    public Guid? ReserveWarehouseId { get; set; }

    protected SubcontractingOrderSuppliedItem() { }
    public SubcontractingOrderSuppliedItem(Guid id, Guid scoId, Guid itemId, string itemName, decimal requiredQty)
        : base(id)
    {
        SubcontractingOrderId = scoId; ItemId = itemId; ItemName = itemName; RequiredQty = requiredQty;
    }
}

public enum SubcontractingOrderStatus
{
    Draft = 0,
    Open = 1,
    PartiallyReceived = 2,
    Completed = 3,
    Closed = 4,
    Cancelled = 5,
}
