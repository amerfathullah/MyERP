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

    public void UpdateReceivedStatus(decimal additionalReceivedQty = 0m)
    {
        if (!Items.Any()) return;
        // Per ERPNext commit ecb392f40b: count only usable qty up to required qty per line so excess on one item cannot inflate % received.
        // Per ERPNext commit cf5585b7f5: additional material rows (Quantity == 0) do not dilute required % received,
        // and receiving only additional material transitions status from Open to PartiallyReceived (Ongoing).
        var requiredItems = Items.Where(i => i.Quantity > 0).ToList();
        PerReceived = requiredItems.Count > 0
            ? Math.Round(
                requiredItems.Min(i => (Math.Min(i.ReceivedQty, i.Quantity) / i.Quantity) * 100m),
                2)
            : 0m;
        var totalReceived = Items.Sum(i => i.ReceivedQty) + additionalReceivedQty;
        if (requiredItems.Count > 0 && PerReceived >= 100m)
            Status = SubcontractingInwardOrderStatus.Completed;
        else if (totalReceived > 0m)
            Status = SubcontractingInwardOrderStatus.PartiallyReceived;
    }

    /// <summary>
    /// Resolves the ERPNext Subcontracting Inward Order lifecycle status from fulfillment percentages
    /// per ERPNext PR #68211 (commit 0187676e69): uses <c>&gt;= 100</c> (not <c>== 100</c>) for
    /// <paramref name="perReturned"/>, <paramref name="perDelivered"/>, and <paramref name="perProduced"/>
    /// so overproduction and over-delivery past 100% still advance the order status.
    /// </summary>
    public static string ResolveInwardOrderFulfillmentStatus(
        bool isSubmitted,
        string? currentStatus,
        decimal perReturned,
        decimal perDelivered,
        decimal perProduced,
        decimal perRawMaterialReceived)
    {
        if (!isSubmitted)
            return currentStatus ?? "Draft";

        if (string.Equals(currentStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            return "Open";
        if (perReturned >= 100m)
            return "Returned";
        if (perDelivered >= 100m)
            return "Delivered";
        if (perProduced >= 100m)
            return "Produced";
        if (perRawMaterialReceived > 0m)
            return "Ongoing";

        return "Open";
    }

    /// <summary>
    /// Proposes pending customer-provided material quantities to receive.
    /// Per ERPNext commit c31b8c9de6: keeps fully received customer-provided materials at 0 qty
    /// instead of skipping them, while still excluding rows with no required quantity.
    /// </summary>
    public IReadOnlyList<(Guid ItemId, Guid OrderItemId, decimal ProposedQty, Guid? WarehouseId)> GetProposedMaterialFromCustomerItems()
    {
        var proposals = new List<(Guid ItemId, Guid OrderItemId, decimal ProposedQty, Guid? WarehouseId)>();
        foreach (var item in Items)
        {
            if (item.Quantity <= 0m)
                continue;

            var proposedQty = Math.Max(0m, Math.Round(item.Quantity - item.ReceivedQty, 4));
            proposals.Add((item.ItemId, item.Id, proposedQty, item.WarehouseId));
        }
        return proposals;
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

    public void Reopen()
    {
        if (Status != SubcontractingInwardOrderStatus.Closed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only closed Subcontracting Inward Orders can be reopened.");
        }

        UpdateReceivedStatus();
        if (Status == SubcontractingInwardOrderStatus.Closed)
        {
            Status = SubcontractingInwardOrderStatus.Open;
        }
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

    /// <summary>
    /// Refreshes each item row's <see cref="SubcontractingInwardOrderItem.SubcontractingConversionFactor"/>
    /// from its linked Sales Order Item (<c>StockQty / FgItemQty</c>) whenever both quantities are non-zero,
    /// preserving the existing ratio when the Sales Order row quantity is zero
    /// per ERPNext commits 76c448206a and a36fc2fd7a (<c>set_subcontracting_conversion_factor</c>).
    /// </summary>
    public void SyncSubcontractingConversionFactorsFromSalesOrder(
        IReadOnlyDictionary<Guid, (decimal StockQty, decimal FgItemQty)> salesOrderItemsById)
    {
        if (salesOrderItemsById == null)
        {
            return;
        }

        foreach (var item in Items)
        {
            if (item.SalesOrderItemId.HasValue &&
                salesOrderItemsById.TryGetValue(item.SalesOrderItemId.Value, out var soItem) &&
                soItem.StockQty != 0m &&
                soItem.FgItemQty != 0m)
            {
                item.SubcontractingConversionFactor = soItem.StockQty / soItem.FgItemQty;
            }
        }
    }
}

/// <summary>FG item expected from subcontracting.</summary>
public class SubcontractingInwardOrderItem : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid SubcontractingInwardOrderId { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>Linked Sales Order Item ID (ERPNext sales_order_item).</summary>
    public Guid? SalesOrderItemId { get; set; }

    /// <summary>Ratio of Sales Order Item stock_qty to fg_item_qty (ERPNext subcontracting_conversion_factor, commit 76c448206a).</summary>
    public decimal SubcontractingConversionFactor { get; set; } = 1m;

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
