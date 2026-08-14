using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.EInvoice;

public interface IEInvoiceSettingsAppService : IApplicationService
{
    Task<EInvoiceConnectionStatusDto> GetConnectionStatusAsync();
    Task SaveCredentialsAsync(SaveEInvoiceCredentialsDto input);
    Task<EInvoiceConnectResultDto> ConnectAsync();
    Task SaveCertificateAsync(SaveEInvoiceCertificateDto input);
    Task<TaxpayerSearchResultDto> SearchTaxpayerAsync(string idType, string idValue);
}
