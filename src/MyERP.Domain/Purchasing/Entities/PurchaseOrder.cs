using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Purchase Order — order placed to supplier.
/// Maps to ERPNext buying/doctype/purchase_order.
/// Flow: PurchaseOrder → PurchaseInvoice
/// </summary>
public class PurchaseOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Shipping term (e.g. FOB, CIF, EXW) — who bears cost/risk at each stage.</summary>
    public Guid? IncotermId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public Guid SupplierId { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Primary billing address (auto-resolved from Supplier on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Total advance payment made against this order.</summary>
    public decimal AdvancePaid { get; set; }

    /// <summary>Percentage of advance paid vs grand total (0-100).</summary>
    public decimal PerAdvancePaid => GrandTotal > 0
        ? Math.Round(AdvancePaid / GrandTotal * 100, 2)
        : 0;

    /// <summary>Cost center for departmental tracking.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise tracking.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Whether this PO is for subcontracted items (links to SCO).</summary>
    public bool IsSubcontracted { get; set; }

    // --- Supplier Confirmation Tracking (per ERPNext PO supplier acknowledgment workflow) ---

    /// <summary>Supplier's own reference/confirmation number for this order.</summary>
    public string? SupplierConfirmationNumber { get; set; }

    /// <summary>Date when supplier confirmed/acknowledged the order.</summary>
    public DateTime? SupplierConfirmationDate { get; set; }

    /// <summary>Supplier's promised delivery date (may differ from our expected date).</summary>
    public DateTime? SupplierPromisedDate { get; set; }

    /// <summary>Whether supplier has confirmed/acknowledged this order.</summary>
    public bool IsSupplierConfirmed => SupplierConfirmationDate.HasValue;

    /// <summary>Records supplier confirmation with optional reference number and promised date.</summary>
    public void RecordSupplierConfirmation(string? confirmationNumber, DateTime confirmationDate, DateTime? promisedDate)
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled)
            throw new BusinessException("MyERP:01001").WithData("documentType", "PurchaseOrder").WithData("status", Status.ToString());

        SupplierConfirmationNumber = confirmationNumber;
        SupplierConfirmationDate = confirmationDate;
        SupplierPromisedDate = promisedDate;
    }

    /// <summary>Source Supplier Quotation that this PO was created from (if any).</summary>
    public Guid? SupplierQuotationId { get; set; }

    /// <summary>Inter-company Sales Order that triggered this PO creation (if any).</summary>
    public Guid? InterCompanySalesOrderId { get; set; }

    /// <summary>Exchange rate: transaction currency → company currency. Default 1.0 for same-currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    // Amendment support
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    /// <summary>Percentage of total qty received (0-100). Uses min per-item completion (all items must be received).</summary>
    public decimal PerReceived => _items.Count > 0
        ? Math.Round(_items.Min(i => i.Quantity > 0 ? i.ReceivedQty / i.Quantity * 100 : 100m), 2)
        : 0;

    /// <summary>Percentage of total amount billed (0-100).</summary>
    public decimal PerBilled => NetTotal > 0
        ? Math.Round(_items.Sum(i => i.BilledQty * i.UnitPrice) / NetTotal * 100, 2)
        : 0;

    private readonly List<PurchaseOrderItem> _items = new();
    public IReadOnlyList<PurchaseOrderItem> Items => _items.AsReadOnly();

    protected PurchaseOrder() { }

    public PurchaseOrder(Guid id, Guid companyId, Guid supplierId, string orderNumber, DateTime orderDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        SupplierId = Check.NotDefaultOrNull<Guid>(supplierId, nameof(supplierId));
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber), PurchaseOrderConsts.MaxOrderNumberLength);
        OrderDate = orderDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom = "Unit")
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Check.NotDefaultOrNull<Guid>(itemId, nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        _items.Add(new PurchaseOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
        RecalculateTotals();
    }

    /// <summary>Clear all items (Draft only). Used during edit to replace items.</summary>
    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.ToDeliverAndBill; // "To Receive and Bill"
    }

    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Manually close the PO (stops further fulfillment without cancelling).
    /// </summary>
    public void Close()
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled || Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>
    /// Reopen a closed PO for further fulfillment.
    /// </summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        UpdateFulfillmentStatus();
    }

    /// <summary>
    /// Recalculates fulfillment status based on received/billed quantities.
    /// Called after Purchase Receipt or Purchase Invoice submission.
    /// </summary>
    public void UpdateFulfillmentStatus()
    {
        if (Status == DocumentStatus.Cancelled || Status == DocumentStatus.Draft)
            return;

        var fullyReceived = PerReceived >= 100m;
        var fullyBilled = PerBilled >= 100m;

        if (fullyReceived && fullyBilled)
            Status = DocumentStatus.Completed;
        else if (fullyReceived)
            Status = DocumentStatus.ToBill;
        else if (fullyBilled)
            Status = DocumentStatus.ToDeliver; // "To Receive"
        else
            Status = DocumentStatus.ToDeliverAndBill;
    }

    private void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
    }

    /// <summary>Count of items past their expected delivery date with pending receipt.</summary>
    public int GetOverdueItemCount(DateTime asOfDate) =>
        _items.Count(i => i.IsOverdue(asOfDate, ExpectedDeliveryDate));

    /// <summary>Whether any item is past expected delivery date.</summary>
    public bool HasOverdueItems(DateTime asOfDate) =>
        _items.Any(i => i.IsOverdue(asOfDate, ExpectedDeliveryDate));

    /// <summary>Maximum days overdue across all items.</summary>
    public int GetMaxDaysOverdue(DateTime asOfDate) =>
        _items.Count > 0 ? _items.Max(i => i.DaysOverdue(asOfDate, ExpectedDeliveryDate)) : 0;

    /// <summary>Count of items confirmed by supplier.</summary>
    public int ConfirmedItemCount => _items.Count(i => i.IsSupplierConfirmed);

    /// <summary>Whether all items have been confirmed by supplier.</summary>
    public bool IsFullyConfirmed => _items.Count > 0 && _items.All(i => i.IsSupplierConfirmed);

    /// <summary>Percentage of items confirmed by supplier (0-100).</summary>
    public decimal PerConfirmed => _items.Count > 0
        ? Math.Round((decimal)_items.Count(i => i.IsSupplierConfirmed) / _items.Count * 100, 0)
        : 0;
}

public class PurchaseOrderItem : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Item's stock UOM. From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Quantity already received via Purchase Receipts.</summary>
    public decimal ReceivedQty { get; set; }

    /// <summary>Date of first receipt against this item (for lead time tracking).</summary>
    public DateTime? FirstReceiptDate { get; set; }

    /// <summary>Date of last/most recent receipt (for delivery performance tracking).</summary>
    public DateTime? LastReceiptDate { get; set; }

    /// <summary>Actual delivery lead time in days (from PO date to first receipt). Null if not yet received.</summary>
    public int? ActualLeadTimeDays(DateTime orderDate) =>
        FirstReceiptDate.HasValue ? (int)(FirstReceiptDate.Value.Date - orderDate.Date).TotalDays : null;

    /// <summary>Quantity already billed via Purchase Invoices.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>Remaining qty to receive.</summary>
    public decimal PendingReceiptQty => Math.Max(0, Quantity - ReceivedQty);

    /// <summary>Remaining qty to bill.</summary>
    public decimal PendingBillingQty => Math.Max(0, Quantity - BilledQty);

    /// <summary>Target warehouse for receipt.</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Link to Material Request item (for MR fulfillment tracking).</summary>
    public Guid? MaterialRequestItemId { get; set; }

    /// <summary>Per-item expected delivery date (overrides parent PO ExpectedDeliveryDate).
    /// Per ERPNext: each PO item can have its own expected_delivery_date.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    /// <summary>Supplier's promised delivery date for this specific item.
    /// Per ERPNext: suppliers can confirm different dates per item via portal.</summary>
    public DateTime? SupplierPromisedDate { get; set; }

    /// <summary>Whether supplier has confirmed this item's delivery.</summary>
    public bool IsSupplierConfirmed { get; set; }

    /// <summary>Effective expected date: supplier promised (if confirmed) → per-item → parent PO.</summary>
    public DateTime? GetEffectiveExpectedDate(DateTime? parentExpectedDate) =>
        (IsSupplierConfirmed && SupplierPromisedDate.HasValue)
            ? SupplierPromisedDate
            : ExpectedDeliveryDate ?? parentExpectedDate;

    /// <summary>Whether this item is overdue for delivery (past effective expected date + not fully received).</summary>
    public bool IsOverdue(DateTime asOfDate, DateTime? parentExpectedDate)
    {
        var effectiveDate = GetEffectiveExpectedDate(parentExpectedDate);
        return effectiveDate.HasValue && effectiveDate.Value.Date < asOfDate.Date && PendingReceiptQty > 0;
    }

    /// <summary>Days overdue for this item (0 if not overdue).</summary>
    public int DaysOverdue(DateTime asOfDate, DateTime? parentExpectedDate)
    {
        var effectiveDate = GetEffectiveExpectedDate(parentExpectedDate);
        if (!effectiveDate.HasValue || effectiveDate.Value.Date >= asOfDate.Date || PendingReceiptQty <= 0)
            return 0;
        return (int)(asOfDate.Date - effectiveDate.Value.Date).TotalDays;
    }

    /// <summary>Records supplier confirmation for this item with promised delivery date.</summary>
    public void ConfirmBySupplier(DateTime promisedDate)
    {
        IsSupplierConfirmed = true;
        SupplierPromisedDate = promisedDate;
    }

    protected PurchaseOrderItem() { }
    public PurchaseOrderItem(Guid id, Guid purchaseOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        PurchaseOrderId = purchaseOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}
