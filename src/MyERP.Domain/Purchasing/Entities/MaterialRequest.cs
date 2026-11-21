using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Purchasing.Entities;

/// <summary>
/// Material Request — request for materials (purchase, transfer, issue, or manufacture).
/// Maps to ERPNext stock/doctype/material_request.
/// </summary>
public class MaterialRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string RequestNumber { get; set; } = null!;
    public MaterialRequestType RequestType { get; set; }
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }

    /// <summary>Source work order (if created from manufacturing).</summary>
    public Guid? WorkOrderId { get; set; }

    /// <summary>Source job card (if created from manufacturing job card transfer).</summary>
    public Guid? JobCardId { get; set; }

    /// <summary>Linked project for budgeting and tracking (ERPNext commit 9eab434ae8).</summary>
    public Guid? ProjectId { get; set; }

    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Source Warehouse is only used by Material Transfer, so it stays hidden and cleared otherwise.
    /// Per ERPNext commit 372155864e.
    /// </summary>
    public void ClearSourceWarehouseIfNotTransfer()
    {
        if (RequestType != MaterialRequestType.MaterialTransfer)
        {
            SourceWarehouseId = null;
        }
    }

    private readonly List<MaterialRequestItem> _items = new();
    public IReadOnlyList<MaterialRequestItem> Items => _items.AsReadOnly();

    /// <summary>Ordered completion %. MIN% formula per ERPNext StatusUpdater.
    /// Manufacture requests track OrderedQuantity in stock UOM (WorkOrder.Quantity), so denominator is StockQty.</summary>
    public decimal PerOrdered
    {
        get
        {
            if (!_items.Any()) return 0;
            return _items.Min(i =>
            {
                var targetQty = RequestType == MaterialRequestType.Manufacture && i.StockQty > 0
                    ? i.StockQty
                    : i.Quantity;
                return targetQty == 0 ? 100 : Math.Min(100, i.OrderedQuantity / targetQty * 100);
            });
        }
    }

    /// <summary>Received completion %. MIN% formula per ERPNext StatusUpdater.</summary>
    public decimal PerReceived
    {
        get
        {
            if (!_items.Any()) return 0;
            return _items.Min(i => i.Quantity == 0 ? 100 : Math.Min(100, i.ReceivedQuantity / i.Quantity * 100));
        }
    }

    protected MaterialRequest() { }

    public MaterialRequest(Guid id, Guid companyId, string requestNumber,
        MaterialRequestType requestType, DateTime requestDate, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        RequestNumber = Check.NotNullOrWhiteSpace(requestNumber, nameof(requestNumber), MaterialRequestConsts.MaxRequestNumberLength);
        RequestType = requestType;
        RequestDate = requestDate;
        TenantId = tenantId;
    }

    public void AddItem(Guid itemId, string itemName, decimal quantity, string uom,
        Guid? warehouseId = null, Guid? salesOrderId = null, Guid? salesOrderItemId = null, Guid? projectId = null,
        decimal conversionFactor = 1m, Guid? productionPlanId = null, Guid? productionPlanMrItemId = null,
        Guid? jobCardItemId = null, decimal priceListRate = 0m, decimal rate = 0m)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        var effectiveRate = rate != 0m ? rate : priceListRate;
        var effectivePriceListRate = priceListRate != 0m ? priceListRate : effectiveRate;

        _items.Add(new MaterialRequestItem(
            Guid.NewGuid(), Id, itemId, itemName, quantity, uom, warehouseId)
        {
            SalesOrderId = salesOrderId,
            SalesOrderItemId = salesOrderItemId,
            ProjectId = projectId ?? ProjectId,
            ConversionFactor = conversionFactor > 0 ? conversionFactor : 1m,
            ProductionPlanId = productionPlanId,
            ProductionPlanMrItemId = productionPlanMrItemId,
            JobCardItemId = jobCardItemId,
            PriceListRate = effectivePriceListRate,
            Rate = effectiveRate,
            Amount = Math.Round(effectiveRate * quantity, 2, MidpointRounding.AwayFromZero),
        });
    }

    /// <summary>
    /// Updates item rates from a Buying Price List resolver.
    /// Per ERPNext stock/doctype/material_request/material_request.py update_item_rates (commit e65c791473):
    /// updates PriceListRate, Rate, and Amount on each matching item.
    /// </summary>
    public void UpdateItemRatesFromPriceList(Func<MaterialRequestItem, decimal?> resolvePriceListRate)
    {
        Check.NotNull(resolvePriceListRate, nameof(resolvePriceListRate));
        foreach (var item in _items)
        {
            var rate = resolvePriceListRate(item);
            if (rate.HasValue)
            {
                item.SetPriceListRate(rate.Value);
            }
        }
    }

    public void ClearItems()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        _items.Clear();
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
        if (Status is DocumentStatus.Cancelled or DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Stops the Material Request. Per ERPNext: cancels pending procurement/fulfillment
    /// without reversing already ordered or transferred items.
    /// </summary>
    public void Stop()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Closed;
    }

    /// <summary>
    /// Reopens a stopped Material Request back to Submitted status.
    /// </summary>
    public void Reopen()
    {
        if (Status != DocumentStatus.Closed)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Submitted;
    }

    /// <summary>
    /// Updates the fulfillment status based on PerReceived and PerOrdered completion.
    /// Per ERPNext status_updater: transitions to Received (Completed) when PerReceived >= 99.99%.
    /// </summary>
    public void UpdateFulfillmentStatus()
    {
        if (Status is DocumentStatus.Draft or DocumentStatus.Cancelled or DocumentStatus.Closed)
            return;

        if (PerReceived >= 99.99m)
        {
            Status = DocumentStatus.Completed;
        }
        else if (PerOrdered >= 99.99m && RequestType != MaterialRequestType.Purchase)
        {
            Status = DocumentStatus.Completed;
        }
        else
        {
            Status = DocumentStatus.Submitted;
        }
    }

    /// <summary>
    /// Determines whether item rates on a Material Request should be refreshed from the Buying Price List on save:
    /// preserves user-entered item rates on first save (<paramref name="hasDocBeforeSave"/> == <c>false</c>) and
    /// refreshes rates only when an already-saved document changes its <c>buying_price_list</c> to a non-null value
    /// per ERPNext commit 6ce460dc78 (<c>material_request.py</c>).
    /// </summary>
    public static bool ShouldRefreshItemRatesOnPriceListChange(
        bool hasDocBeforeSave,
        Guid? buyingPriceListId,
        bool hasBuyingPriceListChanged)
    {
        return hasDocBeforeSave
            && buyingPriceListId.HasValue
            && buyingPriceListId.Value != Guid.Empty
            && hasBuyingPriceListChanged;
    }
}
