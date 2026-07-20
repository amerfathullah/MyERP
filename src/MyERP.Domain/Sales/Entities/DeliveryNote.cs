using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Delivery Note — records goods dispatched to customer.
/// Maps to ERPNext stock/doctype/delivery_note.
/// Links to Sales Order and subsequently to Sales Invoice.
/// </summary>
public class DeliveryNote : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string DeliveryNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Reference to Sales Order this delivery fulfills.</summary>
    public Guid? SalesOrderId { get; set; }

    /// <summary>Target warehouse from which goods are dispatched.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Shipping address (free text or link).</summary>
    public string? ShippingAddress { get; set; }

    /// <summary>Billing address (auto-resolved from SO or Customer).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Shipping address reference (auto-resolved from SO or Customer).</summary>
    public Guid? ShippingAddressId { get; set; }

    /// <summary>Transporter name or reference.</summary>
    public string? Transporter { get; set; }

    /// <summary>LR (Lorry Receipt) or tracking number.</summary>
    public string? TrackingNumber { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Whether this is a return delivery (reversal).</summary>
    public bool IsReturn { get; set; }

    /// <summary>If IsReturn, reference to the original delivery note.</summary>
    public Guid? ReturnAgainstId { get; set; }

    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Exchange rate for multi-currency deliveries (transaction → company currency).</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    // IAccountableDocument
    string IAccountableDocument.DocumentType => "DeliveryNote";
    Guid? IAccountableDocument.CustomerId => CustomerId;
    Guid? IAccountableDocument.SupplierId => null;

    private readonly List<DeliveryNoteItem> _items = new();
    public IReadOnlyList<DeliveryNoteItem> Items => _items.AsReadOnly();

    protected DeliveryNote() { }

    public DeliveryNote(Guid id, Guid companyId, Guid customerId, Guid warehouseId, string deliveryNumber, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        CustomerId = Check.NotDefaultOrNull<Guid>(customerId, nameof(customerId));
        WarehouseId = Check.NotDefaultOrNull<Guid>(warehouseId, nameof(warehouseId));
        DeliveryNumber = Check.NotNullOrWhiteSpace(deliveryNumber, nameof(deliveryNumber), 50);
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit", Guid? salesOrderItemId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per DO-NOT: returns must always have negative qty
        if (!IsReturn && quantity <= 0)
            throw new ArgumentException("Quantity must be positive for non-return delivery notes.", nameof(quantity));
        if (IsReturn && quantity >= 0)
            throw new ArgumentException("Quantity must be negative for return delivery notes.", nameof(quantity));

        _items.Add(new DeliveryNoteItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom, salesOrderItemId));

        RecalculateTotals();
    }

    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = DocumentStatus.Submitted;
        AddLocalEvent(new DeliveryNoteSubmittedEvent(this));
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
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
