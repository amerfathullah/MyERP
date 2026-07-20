using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.DomainServices;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Purchase Receipt — records goods received from supplier.
/// Maps to ERPNext stock/doctype/purchase_receipt.
/// Links to Purchase Order; subsequently linked by Purchase Invoice.
/// On submit: increases warehouse stock via stock ledger.
/// </summary>
public class PurchaseReceipt : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAccountableDocument, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }

    public string ReceiptNumber { get; set; } = null!;
    public DateTime PostingDate { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>Reference to Purchase Order this receipt fulfills.</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Target warehouse where goods are received.</summary>
    public Guid WarehouseId { get; set; }

    /// <summary>Supplier's delivery note / DO number.</summary>
    public string? SupplierDeliveryNote { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Whether this is a return receipt (reversal).</summary>
    public bool IsReturn { get; set; }

    /// <summary>If IsReturn, reference to the original purchase receipt.</summary>
    public Guid? ReturnAgainstId { get; set; }

    /// <summary>Whether this receipt is for a subcontracted purchase order.</summary>
    public bool IsSubcontracted { get; set; }

    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Exchange rate for multi-currency receipts (transaction → company currency).</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    // IAccountableDocument
    string IAccountableDocument.DocumentType => "PurchaseReceipt";
    Guid? IAccountableDocument.CustomerId => null;
    Guid? IAccountableDocument.SupplierId => SupplierId;

    private readonly List<PurchaseReceiptItem> _items = new();
    public IReadOnlyList<PurchaseReceiptItem> Items => _items.AsReadOnly();

    protected PurchaseReceipt() { }

    public PurchaseReceipt(Guid id, Guid companyId, Guid supplierId, Guid warehouseId, string receiptNumber, DateTime postingDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        WarehouseId = Check.NotDefaultOrNull<Guid>(warehouseId, nameof(warehouseId));
        ReceiptNumber = Check.NotNullOrWhiteSpace(receiptNumber, nameof(receiptNumber), 50);
        PostingDate = postingDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit", Guid? purchaseOrderItemId = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        // Per DO-NOT: returns must always have negative qty
        if (!IsReturn && quantity <= 0)
            throw new ArgumentException("Quantity must be positive for non-return receipts.", nameof(quantity));
        if (IsReturn && quantity >= 0)
            throw new ArgumentException("Quantity must be negative for return receipts.", nameof(quantity));

        _items.Add(new PurchaseReceiptItem(
            Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom, purchaseOrderItemId));

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
