using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Order — confirmed order from customer.
/// Maps to ERPNext selling/doctype/sales_order.
/// Flow: Quotation → SalesOrder → SalesInvoice
/// </summary>
public class SalesOrder : FullAuditedAggregateRoot<Guid>, IMultiTenant, IAmendable
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Shipping term (e.g. FOB, CIF, EXW) — who bears cost/risk at each stage.</summary>
    public Guid? IncotermId { get; set; }

    public string OrderNumber { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Customer's own PO reference number.</summary>
    public string? CustomerPoNumber { get; set; }

    public string CurrencyCode { get; set; } = "MYR";
    public decimal NetTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Selling Price List — defaults from Customer.DefaultPriceListId, overridable per order.</summary>
    public Guid? PriceListId { get; set; }

    /// <summary>Primary billing address (auto-resolved from Customer on create).</summary>
    public Guid? BillingAddressId { get; set; }

    /// <summary>Shipping/delivery address (auto-resolved from Customer on create).</summary>
    public Guid? ShippingAddressId { get; set; }

    /// <summary>Billing contact person (Contact ID).</summary>
    public Guid? ContactPersonId { get; set; }

    /// <summary>Shipping contact person (Contact ID) per ERPNext PR #58159.</summary>
    public Guid? ShippingContactPersonId { get; set; }

    /// <summary>Shipping charge calculated from ShippingRule.</summary>
    public decimal ShippingCharge { get; set; }

    /// <summary>Cost center for departmental tracking.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Project for project-wise tracking.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Total advance payment received against this order.</summary>
    public decimal AdvancePaid { get; set; }

    /// <summary>Percentage of advance paid: (AdvancePaid / GrandTotal) × 100.</summary>
    public decimal PerAdvancePaid => GrandTotal > 0 ? Math.Round(AdvancePaid / GrandTotal * 100m, 2) : 0;

    public string? Terms { get; set; }
    public string? Notes { get; set; }
    /// <summary>Coupon code applied at creation — persisted so cancellation can reverse the usage count.</summary>
    public string? CouponCode { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // Amendment (cancel-and-amend workflow)
    public Guid? AmendedFromId { get; set; }
    public int AmendmentIndex { get; set; }

    /// <summary>Source quotation (if converted from quotation).</summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// Percentage of total qty delivered (0-100). Excludes closed rows and service items.
    /// If all items skip delivery or are closed, returns 100%.
    /// </summary>
    public decimal PerDelivered
    {
        get
        {
            var deliverable = _items.Where(i => !i.SkipDelivery && !i.IsClosed).ToList();
            if (deliverable.Count == 0) return _items.Count > 0 ? 100m : 0m;
            return Math.Round(deliverable.Min(i => i.Quantity > 0 ? i.DeliveredQty / i.Quantity * 100 : 100m), 2);
        }
    }

    /// <summary>Percentage of total amount billed (0-100). Excludes closed rows.</summary>
    public decimal PerBilled
    {
        get
        {
            var openItems = _items.Where(i => !i.IsClosed).ToList();
            var openTotal = openItems.Sum(i => i.LineTotal);
            return openTotal > 0
                ? Math.Round(openItems.Sum(i => i.BilledQty * i.UnitPrice) / openTotal * 100, 2)
                : (_items.Count > 0 && _items.All(i => i.IsClosed) ? 100m : 0m);
        }
    }

    private readonly List<SalesOrderItem> _items = new();
    public IReadOnlyList<SalesOrderItem> Items => _items.AsReadOnly();

    protected SalesOrder() { }

    public SalesOrder(Guid id, Guid companyId, Guid customerId, string orderNumber, DateTime orderDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        CustomerId = Check.NotDefaultOrNull<Guid>(customerId, nameof(customerId));
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber), SalesOrderConsts.MaxOrderNumberLength);
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
        _items.Add(new SalesOrderItem(Guid.NewGuid(), Id, itemId, description, quantity, unitPrice, taxAmount, uom));
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

    /// <summary>
    /// Removes a single item row from a submitted order (post-submit editing).
    /// Caller must validate the row is safe to delete (ChildItemUpdateService) before calling this.
    /// </summary>
    public void RemoveItem(Guid itemRowId)
    {
        if (Status == DocumentStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        var item = _items.FirstOrDefault(i => i.Id == itemRowId);
        if (item == null)
            return;
        _items.Remove(item);
        RecalculateTotals();
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft || !_items.Any())
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        ValidateDeliveryDates();

        // Auto-correct conversion factor when UOM equals StockUOM (gotcha #6171)
        foreach (var item in _items)
        {
            if (!string.IsNullOrEmpty(item.Uom) && !string.IsNullOrEmpty(item.StockUom)
                && string.Equals(item.Uom, item.StockUom, StringComparison.OrdinalIgnoreCase)
                && item.ConversionFactor != 1.0m)
            {
                item.ConversionFactor = 1.0m;
            }
        }

        if (PerDelivered >= 100m)
            Status = DocumentStatus.ToBill;
        else
            Status = DocumentStatus.ToDeliverAndBill;
    }

    /// <summary>
    /// Validates delivery dates and syncs header delivery date to max of item delivery dates (gotcha #462).
    /// </summary>
    public void ValidateDeliveryDates()
    {
        var itemDates = _items.Where(i => i.DeliveryDate.HasValue).Select(i => i.DeliveryDate!.Value).ToList();

        foreach (var item in _items)
        {
            if (item.DeliveryDate.HasValue)
            {
                if (item.DeliveryDate.Value.Date < OrderDate.Date)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Item '{item.Description}' delivery date ({item.DeliveryDate.Value:yyyy-MM-dd}) cannot be earlier than order date ({OrderDate:yyyy-MM-dd}).");
                }
            }
            else if (DeliveryDate.HasValue)
            {
                item.DeliveryDate = DeliveryDate;
            }
        }

        if (itemDates.Count > 0)
        {
            DeliveryDate = itemDates.Max();
        }
        else if (DeliveryDate.HasValue && DeliveryDate.Value.Date < OrderDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Delivery date ({DeliveryDate.Value:yyyy-MM-dd}) cannot be earlier than order date ({OrderDate:yyyy-MM-dd}).");
        }
    }

    /// <summary>
    /// Per ERPNext on_cancel(): a Closed SO cannot be cancelled directly — it must be
    /// reopened first. Without this guard, cancelling a Closed order would bypass whatever
    /// the close/reopen cycle is meant to gate (e.g. re-checking credit limit on reopen).
    /// </summary>
    public void Cancel()
    {
        if (Status == DocumentStatus.Cancelled || Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Manually close the SO (stops further fulfillment without cancelling).
    /// Used when remaining items won't be delivered/billed (short-close).
    /// </summary>
    public void Close()
    {
        if (Status == DocumentStatus.Draft || Status == DocumentStatus.Cancelled || Status == DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>
    /// Reopen a closed SO for further fulfillment.
    /// </summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        UpdateFulfillmentStatus(); // recalculate correct status from qty fields
    }

    /// <summary>
    /// Recalculates fulfillment status based on delivered/billed quantities.
    /// Called after Delivery Note or Sales Invoice submission.
    /// </summary>
    public void UpdateFulfillmentStatus()
    {
        if (Status == DocumentStatus.Cancelled || Status == DocumentStatus.Draft)
            return;

        var fullyDelivered = PerDelivered >= 100m;
        var fullyBilled = PerBilled >= 100m;

        if (fullyDelivered && fullyBilled)
            Status = DocumentStatus.Completed;
        else if (fullyDelivered)
            Status = DocumentStatus.ToBill;
        else if (fullyBilled)
            Status = DocumentStatus.ToDeliver;
        else
            Status = DocumentStatus.ToDeliverAndBill;
    }

    public void RecalculateTotals()
    {
        NetTotal = _items.Sum(i => i.LineTotal);
        TaxAmount = _items.Sum(i => i.TaxAmount);
        GrandTotal = NetTotal + TaxAmount;
    }

    /// <summary>Closes an individual item row (per ERPNext PR #57596).</summary>
    public void CloseItem(Guid itemRowId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemRowId);
        if (item == null || item.IsClosed) return;
        item.IsClosed = true;
        UpdateFulfillmentStatus();
    }

    /// <summary>Reopens an individual item row.</summary>
    public void ReopenItem(Guid itemRowId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemRowId);
        if (item == null || !item.IsClosed) return;
        item.IsClosed = false;
        UpdateFulfillmentStatus();
    }
}

public class SalesOrderItem : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = null!;
    public string Uom { get; set; } = "Unit";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;

    /// <summary>Whether this individual row is closed (per ERPNext PR #57596).</summary>
    public bool IsClosed { get; set; }

    /// <summary>Item's stock UOM (e.g., "Unit"). From Item master.</summary>
    public string StockUom { get; set; } = "Unit";

    /// <summary>Conversion factor: transaction UOM → stock UOM. e.g., 1 Dozen = 12 Units → factor = 12.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    /// <summary>Quantity in stock UOM = Quantity × ConversionFactor.</summary>
    public decimal StockQty => Quantity * ConversionFactor;

    /// <summary>Rate per stock UOM = UnitPrice / ConversionFactor (gotcha #198).</summary>
    public decimal StockUomRate => ConversionFactor > 0 ? Math.Round(UnitPrice / ConversionFactor, 4) : UnitPrice;

    /// <summary>Quantity already delivered via Delivery Notes.</summary>
    public decimal DeliveredQty { get; set; }

    /// <summary>Quantity already invoiced via Sales Invoices.</summary>
    public decimal BilledQty { get; set; }

    /// <summary>Quantity returned via sales returns (Delivery Note return).</summary>
    public decimal ReturnedQty { get; set; }

    /// <summary>Quantity requested via Material Requests created from this Sales Order line.</summary>
    public decimal RequestedQty { get; set; }

    /// <summary>Quantity ordered via Purchase Orders created from this Sales Order line (e.g. drop-ship).</summary>
    public decimal OrderedQty { get; set; }

    /// <summary>
    /// Billable quantity accounting for deliveries, returns, and re-deliveries.
    /// Per ERPNext PR #114ba42850:
    /// min(ordered_qty, max(ordered_qty - returned_qty, delivered_qty))
    /// </summary>
    public decimal BillableQty => Math.Min(Quantity, Math.Max(Quantity - ReturnedQty, DeliveredQty));

    /// <summary>For service/non-stock items when SkipDeliveryNoteForServiceItems is active or Maintenance order.</summary>
    public bool SkipDelivery { get; set; }

    /// <summary>Remaining qty to deliver.</summary>
    public decimal PendingDeliveryQty => (SkipDelivery || IsClosed) ? 0 : Math.Max(0, Quantity - DeliveredQty);

    /// <summary>Remaining qty to bill accounting for returns and re-deliveries.</summary>
    public decimal PendingBillingQty => IsClosed ? 0 : Math.Max(0, BillableQty - BilledQty);

    /// <summary>Target warehouse for this item (for stock reservation).</summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>Whether this item is fulfilled directly by supplier to customer (no warehouse involvement).</summary>
    public bool DeliveredBySupplier { get; set; }

    /// <summary>Drop-ship supplier for this item (required when DeliveredBySupplier=true).</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Per-item delivery date (overrides parent SO DeliveryDate). Per ERPNext: each SO item can have its own delivery_date.</summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Blanket Order this line draws from (deducts qty from the blanket allocation on submit).</summary>
    public Guid? BlanketOrderId { get; set; }

    protected SalesOrderItem() { }
    public SalesOrderItem(Guid id, Guid salesOrderId, Guid itemId, string description, decimal quantity, decimal unitPrice, decimal taxAmount, string uom)
        : base(id)
    {
        SalesOrderId = salesOrderId; ItemId = itemId; Description = description;
        Quantity = quantity; UnitPrice = unitPrice; TaxAmount = taxAmount; Uom = uom;
    }
}

/// <summary>
/// Sales Order Delivery Schedule — planned delivery windows for partial fulfillment.
/// Per ERPNext: SO delivery_schedule child table enables splitting large orders into
/// multiple delivery dates. Each row tracks planned vs actual delivered qty.
/// Frequency-based auto-generation (Weekly/Monthly/Quarterly/Yearly) supported.
/// Per gotcha #108: SO has a dialog to create frequency-based split deliveries.
/// </summary>
public class DeliveryScheduleEntry : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SalesOrderId { get; set; }
    public Guid SalesOrderItemId { get; set; }

    /// <summary>Planned delivery date for this schedule row.</summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>Planned quantity to deliver on this date.</summary>
    public decimal ScheduledQty { get; set; }

    /// <summary>Quantity actually delivered against this schedule row.</summary>
    public decimal DeliveredQty { get; set; }

    /// <summary>Pending delivery qty = Scheduled - Delivered.</summary>
    public decimal PendingQty => Math.Max(0, ScheduledQty - DeliveredQty);

    /// <summary>True when all scheduled qty has been delivered.</summary>
    public bool IsFullyDelivered => DeliveredQty >= ScheduledQty;

    protected DeliveryScheduleEntry() { }

    public DeliveryScheduleEntry(Guid id, Guid salesOrderId, Guid salesOrderItemId,
        DateTime scheduledDate, decimal scheduledQty, Guid? tenantId = null) : base(id)
    {
        SalesOrderId = salesOrderId;
        SalesOrderItemId = salesOrderItemId;
        ScheduledDate = scheduledDate;
        ScheduledQty = scheduledQty;
        TenantId = tenantId;
    }

    /// <summary>Record delivery against this schedule row.</summary>
    public void RecordDelivery(decimal qty)
    {
        DeliveredQty += qty;
    }
}

