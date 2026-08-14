using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICurrencyExchangeAppService : IApplicationService
{
    Task<PagedResultDto<CurrencyExchangeDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<CurrencyExchangeDto> CreateAsync(CreateCurrencyExchangeDto input);
    Task DeleteAsync(Guid id);
    Task<ExchangeRateResultDto> GetRateAsync(string fromCurrency, string toCurrency, DateTime? transactionDate = null);
}
