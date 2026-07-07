using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Quotation — sales proposal to customer.
/// Maps to ERPNext selling/doctype/quotation.
/// Flow: Quotation → SalesOrder → SalesInvoice
/// </summary>
public class Quotation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string QuotationNumber { get; set; } = null!;
    public DateTime IssueDate { get; set; }
    public DateTime? ValidUntil { get; set; }

    public Guid CustomerId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Reference to converted SalesOrder (if converted).</summary>
    public Guid? ConvertedToSalesOrderId { get; set; }

    private readonly List<QuotationItem> _items = new();
    public IReadOnlyList<QuotationItem> Items => _items.AsReadOnly();

    protected Quotation() { }

    public Quotation(Guid id, Guid companyId, Guid customerId, string quotationNumber, DateTime issueDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        QuotationNumber = Check.NotNullOrWhiteSpace(quotationNumber, nameof(quotationNumber), QuotationConsts.MaxQuotationNumberLength);
        IssueDate = issueDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Add(new QuotationItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
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

public class QuotationItem : CreationAuditedEntity<Guid>
{
    public Guid QuotationId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    protected QuotationItem() { }
    public QuotationItem(Guid id, Guid quotationId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        QuotationId = quotationId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
