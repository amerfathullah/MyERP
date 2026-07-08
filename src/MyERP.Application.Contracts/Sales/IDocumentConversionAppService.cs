using System;
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

    /// <summary>Convert a submitted Sales Order into a Delivery Note.</summary>
    Task<DeliveryNoteDto> ConvertSalesOrderToDeliveryNoteAsync(Guid salesOrderId);

    /// <summary>Convert a submitted Sales Order into a Sales Invoice.</summary>
    Task<SalesInvoiceDto> ConvertSalesOrderToSalesInvoiceAsync(Guid salesOrderId);

    /// <summary>Convert a submitted Delivery Note into a Sales Invoice.</summary>
    Task<SalesInvoiceDto> ConvertDeliveryNoteToSalesInvoiceAsync(Guid deliveryNoteId);
}
