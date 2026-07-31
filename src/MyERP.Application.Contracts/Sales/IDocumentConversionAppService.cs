using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

/// <summary>
/// Handles document-to-document conversion along the sales and purchase pipelines.
/// Mirrors ERPNext's "Make" button functionality.
/// </summary>
public interface IDocumentConversionAppService : IApplicationService
{
    /// <summary>Convert a submitted Quotation into a Sales Order.</summary>
    Task<SalesOrderDto> ConvertQuotationToSalesOrderAsync(Guid quotationId);

    /// <summary>Convert a submitted Sales Order into a Delivery Note. When selectedItems is empty, delivers all pending items.</summary>
    Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteAsync(Guid salesOrderId, List<PartialDeliveryItemDto>? selectedItems = null);

    /// <summary>Convert SO items due by a cutoff date into a Delivery Note (scheduled delivery filter).</summary>
    /// <param name="salesOrderId">Sales Order ID</param>
    /// <param name="untilDeliveryDate">Only include items with DeliveryDate on or before this date. Per ERPNext SO→DN mapper: delivery_date cutoff filter for partial scheduled deliveries.</param>
    Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteByDateAsync(Guid salesOrderId, DateTime untilDeliveryDate);

    /// <summary>Convert a submitted Sales Order into a Sales Invoice.</summary>
    Task<SalesInvoiceDto> ConvertSalesOrderToSalesInvoiceAsync(Guid salesOrderId);

    /// <summary>Convert a submitted Delivery Note into a Sales Invoice.</summary>
    Task<SalesInvoiceDto> ConvertDeliveryNoteToSalesInvoiceAsync(Guid deliveryNoteId);
}

/// <summary>
/// DTO for partial delivery item selection.
/// Per ERPNext: users select which items and quantities to deliver from an order.
/// </summary>
public class PartialDeliveryItemDto
{
    /// <summary>Sales Order Item ID to deliver.</summary>
    public Guid SalesOrderItemId { get; set; }

    /// <summary>Quantity to deliver (must be > 0 and ≤ PendingDeliveryQty).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Optional warehouse override (defaults to SO item warehouse).</summary>
    public Guid? WarehouseId { get; set; }
}
