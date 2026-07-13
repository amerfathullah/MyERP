using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Supplier Quotation — supplier's response to an RFQ with quoted rates.
/// Used in the procurement cycle: RFQ → SQ → PO.
/// Maps to ERPNext buying/doctype/supplier_quotation.
/// </summary>
public class SupplierQuotation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }

    public string? QuotationNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? ValidTill { get; set; }

    /// <summary>Currency of the quotation.</summary>
    public string Currency { get; set; } = "MYR";
    public decimal ExchangeRate { get; set; } = 1;

    /// <summary>Link to Request for Quotation (if created from RFQ).</summary>
    public Guid? RequestForQuotationId { get; set; }

    public decimal NetTotal { get; private set; }
    public decimal GrandTotal { get; private set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;
    public string? Notes { get; set; }

    private readonly List<SupplierQuotationItem> _items = new();
    public IReadOnlyList<SupplierQuotationItem> Items => _items.AsReadOnly();

    protected SupplierQuotation() { }

    public SupplierQuotation(Guid id, Guid companyId, Guid supplierId,
        DateTime transactionDate, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        SupplierId = supplierId;
        TransactionDate = transactionDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, decimal qty, decimal rate, string? itemName = null, string? uom = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new SupplierQuotationItem(Guid.NewGuid(), Id, itemId, qty, rate, itemName, uom));
        RecalculateTotals();
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

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.Amount);
        GrandTotal = NetTotal;
    }
}

public class SupplierQuotationItem : FullAuditedEntity<Guid>
{
    public Guid SupplierQuotationId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;

    /// <summary>Link to Material Request item (if applicable).</summary>
    public Guid? MaterialRequestItemId { get; set; }

    protected SupplierQuotationItem() { }

    public SupplierQuotationItem(Guid id, Guid sqId, Guid itemId,
        decimal qty, decimal rate, string? itemName, string? uom) : base(id)
    {
        SupplierQuotationId = sqId;
        ItemId = itemId;
        Qty = qty;
        Rate = rate;
        ItemName = itemName;
        Uom = uom;
    }
}
