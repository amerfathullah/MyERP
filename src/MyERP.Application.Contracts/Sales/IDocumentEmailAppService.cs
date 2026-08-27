using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Sales;

public interface IDocumentEmailAppService : IApplicationService
{
    Task SendSalesInvoiceEmailAsync(SendInvoiceEmailDto input);
    Task SendQuotationEmailAsync(SendQuotationEmailDto input);
    Task<EmailPreviewDto> PreviewEmailAsync(PreviewEmailInput input);
    Task SendSalesOrderEmailAsync(SendSalesOrderEmailDto input);
    Task SendPurchaseOrderEmailAsync(SendPurchaseOrderEmailDto input);
    Task SendDeliveryNoteEmailAsync(SendDeliveryNoteEmailDto input);
    Task SendStatementEmailAsync(SendStatementEmailDto input);
}
