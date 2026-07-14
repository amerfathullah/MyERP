using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Request for Quotation — sent to multiple suppliers to collect competitive quotes.
/// Maps to ERPNext buying/doctype/request_for_quotation.
/// Flow: Material Request → RFQ → Supplier Quotation → Purchase Order.
/// </summary>
public class RequestForQuotation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string RfqNumber { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public string? MessageForSupplier { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    private readonly List<RfqItem> _items = new();
    public IReadOnlyList<RfqItem> Items => _items.AsReadOnly();

    private readonly List<RfqSupplier> _suppliers = new();
    public IReadOnlyList<RfqSupplier> Suppliers => _suppliers.AsReadOnly();

    protected RequestForQuotation() { }

    public RequestForQuotation(Guid id, Guid companyId, string rfqNumber, DateTime transactionDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        RfqNumber = Check.NotNullOrWhiteSpace(rfqNumber, nameof(rfqNumber), 50);
        TransactionDate = transactionDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal qty, string uom, Guid? warehouseId = null, Guid? materialRequestItemId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (qty < 0) throw new ArgumentException("Quantity cannot be negative.", nameof(qty));

        _items.Add(new RfqItem(Guid.NewGuid(), Id, itemId, description, qty, uom)
        {
            WarehouseId = warehouseId,
            MaterialRequestItemId = materialRequestItemId,
        });
    }

    /// <summary>
    /// Adds a supplier to receive this RFQ. No duplicate suppliers allowed.
    /// </summary>
    public void AddSupplier(Guid supplierId, string supplierName, string? email = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        if (_suppliers.Any(s => s.SupplierId == supplierId))
            throw new BusinessException("MyERP:04010")
                .WithData("supplierName", supplierName);

        _suppliers.Add(new RfqSupplier(Guid.NewGuid(), Id, supplierId, supplierName)
        {
            Email = email,
        });
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any() || !_suppliers.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }
}

public class RfqItem : CreationAuditedEntity<Guid>
{
    public Guid RequestForQuotationId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public Guid? WarehouseId { get; set; }
    public Guid? MaterialRequestItemId { get; set; }

    protected RfqItem() { }
    public RfqItem(Guid id, Guid rfqId, Guid itemId, string description, decimal qty, string uom) : base(id)
    {
        RequestForQuotationId = rfqId;
        ItemId = itemId;
        Description = description;
        Qty = qty;
        Uom = uom;
    }
}

public class RfqSupplier : CreationAuditedEntity<Guid>
{
    public Guid RequestForQuotationId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? Email { get; set; }
    public bool EmailSent { get; set; }
    public string QuoteStatus { get; set; } = "Pending";

    /// <summary>Marks quote received when a Supplier Quotation is submitted against this RFQ.</summary>
    public void MarkQuoteReceived() => QuoteStatus = "Received";

    protected RfqSupplier() { }
    public RfqSupplier(Guid id, Guid rfqId, Guid supplierId, string supplierName) : base(id)
    {
        RequestForQuotationId = rfqId;
        SupplierId = supplierId;
        SupplierName = supplierName;
    }
}
