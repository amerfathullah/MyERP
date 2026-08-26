using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public interface ICurrencyExchangeSettingsAppService : IApplicationService
{
    Task<CurrencyExchangeSettingsDto> GetAsync();
    Task<CurrencyExchangeSettingsDto> UpdateAsync(UpdateCurrencyExchangeSettingsDto input);
    Task<TestCurrencyExchangeApiResponseDto> TestConnectionAsync(TestCurrencyExchangeApiRequestDto input);
}
