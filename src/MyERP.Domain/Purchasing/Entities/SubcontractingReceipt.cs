using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Subcontracting Receipt — receipt of finished goods from subcontractor.
/// Triggers: SLE for FG stock-in, consumed RM stock-out, cost calculation.
/// </summary>
public class SubcontractingReceipt : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }
    public Guid SubcontractingOrderId { get; set; }

    public Guid? WarehouseId { get; set; }

    public decimal NetTotal { get; set; }

    public SubcontractingReceiptStatus Status { get; private set; } = SubcontractingReceiptStatus.Draft;

    private readonly List<SubcontractingReceiptItem> _items = new();
    public IReadOnlyList<SubcontractingReceiptItem> Items => _items.AsReadOnly();

    protected SubcontractingReceipt() { }

    public SubcontractingReceipt(Guid id, Guid companyId, string receiptNumber, DateTime postingDate,
        Guid supplierId, Guid scoId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        ReceiptNumber = receiptNumber;
        PostingDate = postingDate;
        SupplierId = supplierId;
        SubcontractingOrderId = scoId;
        TenantId = tenantId;
    }

    public void AddItem(SubcontractingReceiptItem item)
    {
        if (Status != SubcontractingReceiptStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(item);
        NetTotal = _items.Sum(i => i.Amount);
    }

    public void Submit()
    {
        if (Status != SubcontractingReceiptStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingReceiptStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == SubcontractingReceiptStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = SubcontractingReceiptStatus.Cancelled;
    }
}

public class SubcontractingReceiptItem : Entity<Guid>
{
    public Guid SubcontractingReceiptId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;
    public Guid? WarehouseId { get; set; }

    protected SubcontractingReceiptItem() { }
    public SubcontractingReceiptItem(Guid id, Guid scrId, Guid itemId, string itemName, decimal qty, decimal rate)
        : base(id)
    {
        SubcontractingReceiptId = scrId; ItemId = itemId; ItemName = itemName; Qty = qty; Rate = rate;
    }
}

public enum SubcontractingReceiptStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2,
}
