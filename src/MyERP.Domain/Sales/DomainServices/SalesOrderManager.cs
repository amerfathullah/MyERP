using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for Sales Order business rules.
/// Validates cancel guards (dependent docs), close/reopen stock release.
/// Mirrors PurchaseOrderManager for purchasing parity.
/// </summary>
public class SalesOrderManager : DomainService
{
    private readonly IRepository<SalesOrder, Guid> _orderRepository;

    public SalesOrderManager(IRepository<SalesOrder, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    /// <summary>
    /// Validates an SO can be cancelled — blocks if submitted Delivery Notes or Sales Invoices exist.
    /// Per DO-NOT: must cancel children first before parent.
    /// </summary>
    public async Task ValidateCanCancelAsync(
        SalesOrder order,
        IRepository<DeliveryNote, Guid> dnRepository,
        IRepository<SalesInvoice, Guid> siRepository)
    {
        // Check for submitted Delivery Notes
        var dnQuery = await dnRepository.GetQueryableAsync();
        var hasSubmittedDN = dnQuery.Any(dn =>
            dn.SalesOrderId == order.Id
            && dn.Status != Core.DocumentStatus.Draft
            && dn.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedDN)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Delivery Note");
        }

        // Check for submitted Sales Invoices linked to this SO's items
        var siQuery = await siRepository.GetQueryableAsync();
        var orderItemIds = order.Items.Select(oi => oi.Id).ToList();
        var hasSubmittedSI = siQuery.Any(si =>
            si.Items.Any(i => i.SalesOrderItemId.HasValue && orderItemIds.Contains(i.SalesOrderItemId.Value))
            && si.Status != Core.DocumentStatus.Draft
            && si.Status != Core.DocumentStatus.Cancelled);

        if (hasSubmittedSI)
        {
            throw new BusinessException(MyERPDomainErrorCodes.CannotCancelWithSubmittedDependents)
                .WithData("documentType", "Sales Order")
                .WithData("dependent", "Sales Invoice");
        }
    }

    /// <summary>
    /// Validates over-delivery: DN item qty cannot exceed SO item's PendingDeliveryQty.
    /// Only applies to non-return DNs linked to a Sales Order.
    /// </summary>
    public void ValidateDeliveryQty(SalesOrder order, Guid itemId, decimal deliveryQty)
    {
        var soItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (soItem == null) return;

        if (deliveryQty > soItem.PendingDeliveryQty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverDelivery)
                .WithData("itemName", soItem.Description)
                .WithData("orderedQty", soItem.Quantity)
                .WithData("deliveredQty", soItem.DeliveredQty)
                .WithData("attemptedQty", deliveryQty);
        }
    }

    /// <summary>
    /// Updates SO DeliveredQty and fulfillment status after DN submit/cancel.
    /// </summary>
    public async Task UpdateLinkedOrderDeliveryAsync(
        Guid salesOrderId, Guid itemId, decimal qtyDelta)
    {
        var order = await _orderRepository.GetAsync(salesOrderId);
        var soItem = order.Items.FirstOrDefault(i => i.ItemId == itemId);
        if (soItem == null) return;

        soItem.DeliveredQty = Math.Max(0, soItem.DeliveredQty + qtyDelta);
        order.UpdateFulfillmentStatus();
        await _orderRepository.UpdateAsync(order);
    }
}
