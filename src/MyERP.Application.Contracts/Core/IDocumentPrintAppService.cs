using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface IDocumentPrintAppService : IApplicationService
{
    Task<DocumentPrintResult> GetSalesInvoicePrintAsync(Guid invoiceId);
    Task<DocumentPrintResult> GetPurchaseOrderPrintAsync(Guid orderId);
    Task<DocumentPrintResult> GetQuotationPrintAsync(Guid quotationId);
    Task<DocumentPrintResult> GetDeliveryNotePrintAsync(Guid deliveryNoteId);
}
