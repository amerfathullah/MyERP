using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface IOpeningBalanceAppService : IApplicationService
{
    Task<OpeningBalanceResultDto> CreateOpeningJournalEntryAsync(CreateOpeningJournalEntryDto input);
    Task<OpeningInvoiceResultDto> CreateOpeningSalesInvoicesAsync(CreateOpeningInvoicesDto input);
    Task<OpeningInvoiceResultDto> CreateOpeningPurchaseInvoicesAsync(CreateOpeningInvoicesDto input);
    Task<OpeningStatusDto> GetOpeningStatusAsync(Guid companyId);
}
