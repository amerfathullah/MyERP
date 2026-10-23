using System;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Core.DomainServices;

/// <summary>
/// Child Item Update Service — validates child item deletion and updates on orders and quotations.
/// Per ERPNext validate_child_on_delete() (gotcha #6206):
/// - Sales Order item: cannot delete if delivered_qty > 0, work_order_qty > 0, or billed_qty > 0.
/// - Purchase Order item: cannot delete if received_qty > 0 or billed_qty > 0.
/// - Quotation item: cannot delete if already converted to an ordered Sales Order item.
/// </summary>
public class ChildItemUpdateService : DomainService
{
    /// <summary>
    /// Validates whether a Sales Order child row can be deleted or removed.
    /// Throws BusinessException if the item has already been partially delivered or billed.
    /// </summary>
    public void ValidateSalesOrderItemDeletion(SalesOrderItem item, bool hasKeptStockReservation = false)
    {
        if (item.IsClosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' because it is closed. Reopen the row first.");
        }

        if (hasKeptStockReservation)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' because stock is reserved. Unreserve stock before deleting the item.");
        }

        if (item.DeliveredQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' — {item.DeliveredQty} units have already been delivered.");
        }

        if (item.BilledQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Sales Order item '{item.Description}' — {item.BilledQty} units have already been billed.");
        }
    }

    /// <summary>
    /// Validates whether a Purchase Order child row can be deleted or removed.
    /// Throws BusinessException if the item has already been partially received or billed, or is closed.
    /// </summary>
    public void ValidatePurchaseOrderItemDeletion(PurchaseOrderItem item)
    {
        if (item.IsClosed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Purchase Order item '{item.Description}' because it is closed. Reopen the row first.");
        }

        if (item.ReceivedQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Purchase Order item '{item.Description}' — {item.ReceivedQty} units have already been received.");
        }

        if (item.BilledQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Purchase Order item '{item.Description}' — {item.BilledQty} units have already been billed.");
        }
    }

    /// <summary>
    /// Validates whether a Sales Order child row can be modified.
    /// Per ERPNext PR #58609: closed rows cannot be modified without reopening first.
    /// </summary>
    public void ValidateSalesOrderItemUpdate(SalesOrderItem item, decimal newQty, decimal newRate)
    {
        if (item.IsClosed && (item.Quantity != newQty || item.UnitPrice != newRate))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot modify Sales Order item '{item.Description}' because it is closed. Reopen the row first.");
        }
    }

    /// <summary>
    /// Validates whether a Purchase Order child row can be modified.
    /// Per ERPNext PR #58609: closed rows cannot be modified without reopening first.
    /// </summary>
    public void ValidatePurchaseOrderItemUpdate(PurchaseOrderItem item, decimal newQty, decimal newRate)
    {
        if (item.IsClosed && (item.Quantity != newQty || item.UnitPrice != newRate))
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot modify Purchase Order item '{item.Description}' because it is closed. Reopen the row first.");
        }
    }

    /// <summary>
    /// Validates requested quantity and conversion factor against already delivered and reserved quantity in stock UOM (per ERPNext PR #58603 &amp; PR #61634).
    /// </summary>
    public void ValidateSalesOrderItemStockQty(SalesOrderItem item, decimal newQty, decimal? newConversionFactor = null, decimal stockReservedQty = 0m)
    {
        var factor = newConversionFactor.HasValue && newConversionFactor.Value > 0 ? newConversionFactor.Value : item.ConversionFactor;
        var requestedStockQty = newQty * factor;
        var deliveredStockQty = item.DeliveredQty * item.ConversionFactor;

        if (requestedStockQty < deliveredStockQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot set quantity less than delivered quantity ({deliveredStockQty} in stock UOM).");
        }

        if (stockReservedQty > 0 && requestedStockQty < deliveredStockQty + stockReservedQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot set quantity less than delivered and reserved quantity ({deliveredStockQty + stockReservedQty} in stock UOM). Unreserve stock before reducing the quantity.");
        }
    }

    /// <summary>
    /// Validates requested quantity and conversion factor against already received quantity in stock UOM (per ERPNext PR #58603).
    /// </summary>
    public void ValidatePurchaseOrderItemStockQty(PurchaseOrderItem item, decimal newQty, decimal? newConversionFactor = null)
    {
        var factor = newConversionFactor.HasValue && newConversionFactor.Value > 0 ? newConversionFactor.Value : item.ConversionFactor;
        var requestedStockQty = newQty * factor;
        var receivedStockQty = item.ReceivedQty * item.ConversionFactor;

        if (requestedStockQty < receivedStockQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot set quantity less than received quantity ({receivedStockQty} in stock UOM).");
        }
    }

    /// <summary>
    /// Validates whether a Quotation child row can be modified.
    /// Per ERPNext commit c755e24731 / PR #58603: rate cannot be changed once ordered.
    /// </summary>
    public void ValidateQuotationItemUpdate(QuotationItem item, decimal newQty, decimal newRate)
    {
        if (item.OrderedQty > 0 && item.UnitPrice != newRate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot change rate for item '{item.Description}' as it is already ordered.");
        }
    }

    /// <summary>
    /// Validates requested quantity and conversion factor against already ordered quantity in stock UOM (per ERPNext commit c755e24731 / PR #58603).
    /// </summary>
    public void ValidateQuotationItemStockQty(QuotationItem item, decimal newQty, decimal? newConversionFactor = null)
    {
        var factor = newConversionFactor.HasValue && newConversionFactor.Value > 0 ? newConversionFactor.Value : item.ConversionFactor;
        var requestedStockQty = newQty * factor;
        var orderedStockQty = item.OrderedQty;

        if (requestedStockQty < orderedStockQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot set quantity less than ordered quantity ({orderedStockQty} in stock UOM).");
        }
    }

    /// <summary>
    /// Validates whether a Quotation child row can be deleted or removed.
    /// </summary>
    public void ValidateQuotationItemDeletion(QuotationItem item, bool isOrdered)
    {
        if (isOrdered || item.OrderedQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Quotation item '{item.Description}' — quotation has already been converted to an order.");
        }
    }

    public void ValidateQuotationItemDeletion(QuotationItem item) => ValidateQuotationItemDeletion(item, item.OrderedQty > 0);

    /// <summary>
    /// Validates whether a Supplier Quotation child row can be deleted or removed.
    /// Per ERPNext: cannot delete if already ordered.
    /// </summary>
    public void ValidateSupplierQuotationItemDeletion(SupplierQuotationItem item)
    {
        if (item.OrderedQty > 0)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot delete Supplier Quotation item '{item.ItemName}' — quotation has already been ordered ({item.OrderedQty} in stock UOM).");
        }
    }

    /// <summary>
    /// Validates whether a Supplier Quotation child row can be modified.
    /// Per ERPNext commit c755e24731 / PR #58603: rate cannot be changed once ordered.
    /// </summary>
    public void ValidateSupplierQuotationItemUpdate(SupplierQuotationItem item, decimal newQty, decimal newRate)
    {
        if (item.OrderedQty > 0 && item.Rate != newRate)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot change rate for item '{item.ItemName}' as it is already ordered.");
        }
    }

    /// <summary>
    /// Validates requested quantity and conversion factor against already ordered quantity in stock UOM (per ERPNext commit c755e24731 / PR #58603).
    /// </summary>
    public void ValidateSupplierQuotationItemStockQty(SupplierQuotationItem item, decimal newQty, decimal? newConversionFactor = null)
    {
        var factor = newConversionFactor.HasValue && newConversionFactor.Value > 0 ? newConversionFactor.Value : item.ConversionFactor;
        var requestedStockQty = newQty * factor;
        var orderedStockQty = item.OrderedQty;

        if (requestedStockQty < orderedStockQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Cannot set quantity less than ordered quantity ({orderedStockQty} in stock UOM).");
        }
    }

    /// <summary>
    /// Validates whether an order's document status permits post-submit item updates.
    /// Per ERPNext PR #59832 (commit 008fb0c12e):
    /// - Rejects Draft and Cancelled ("Only submitted orders can have items updated. Use Edit for draft orders.").
    /// - Rejects Closed and OnHold ("Cannot update items on a Closed/OnHold order.").
    /// </summary>
    public void ValidateOrderStatusForItemUpdate(DocumentStatus status, string orderType, string? orderNumber = null)
    {
        if (status == DocumentStatus.Draft || status == DocumentStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", orderType)
                .WithData("status", status.ToString())
                .WithData("detail", "Only submitted orders can have items updated. Use Edit for draft orders.");
        }

        if (status == DocumentStatus.Closed)
        {
            var refLabel = string.IsNullOrWhiteSpace(orderNumber) ? orderType : $"{orderType} {orderNumber}";
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("documentType", orderType)
                .WithData("status", status.ToString())
                .WithData("detail", $"Cannot update items on {refLabel} because it is {status}.");
        }
    }

    /// <summary>
    /// Validates that UOM and conversion factor are not changed after an order item has already been
    /// partially or fully fulfilled (received on Purchase Order or delivered on Sales Order).
    /// Per ERPNext PR #59832 (commit 008fb0c12e):
    /// "Cannot change the UOM or conversion factor of Item after part of it was received or delivered".
    /// </summary>
    public void ValidateUomUnchangedAfterFulfilment(
        string itemDescription,
        decimal fulfilledQty,
        string currentUom,
        string? newUom,
        decimal currentConversionFactor,
        decimal? newConversionFactor,
        bool hasKeptStockReservation = false)
    {
        var uomChanged = !string.IsNullOrWhiteSpace(newUom)
            && !string.Equals(currentUom, newUom, StringComparison.OrdinalIgnoreCase);

        var factorChanged = newConversionFactor.HasValue
            && newConversionFactor.Value > 0
            && newConversionFactor.Value != currentConversionFactor;

        if ((uomChanged || factorChanged) && hasKeptStockReservation)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("item", itemDescription)
                .WithData("detail", $"Cannot change the UOM or conversion factor of Item '{itemDescription}' while stock is reserved. Unreserve stock first.");
        }

        if (fulfilledQty <= 0)
            return;

        if (uomChanged || factorChanged)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("item", itemDescription)
                .WithData("fulfilledQty", fulfilledQty)
                .WithData("detail", $"Cannot change the UOM or conversion factor of Item '{itemDescription}' after part of it was received or delivered.");
        }
    }
}
