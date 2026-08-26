using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class CurrencyExchangeSettingsAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICurrencyExchangeSettingsAppService _settingsAppService;

    protected CurrencyExchangeSettingsAppServiceTests()
    {
        _settingsAppService = GetRequiredService<ICurrencyExchangeSettingsAppService>();
    }

    [Fact]
    public async Task CurrencyExchangeSettings_Should_Get_And_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settings = await _settingsAppService.GetAsync();
            settings.ShouldNotBeNull();
            settings.ServiceProvider.ShouldBe("frankfurter.dev");

            var updated = await _settingsAppService.UpdateAsync(new UpdateCurrencyExchangeSettingsDto
            {
                ServiceProvider = "Custom",
                ApiEndpoint = "https://custom-rates.example.com/api/v1/rates",
                AccessKey = "test_key_123",
                ReqParams = new List<CreateUpdateCurrencyExchangeSettingsDetailDto>
                {
                    new() { Key = "source", Value = "{from_currency}" },
                    new() { Key = "target", Value = "{to_currency}" }
                },
                ResultKeys = new List<CreateUpdateCurrencyExchangeSettingsResultDto>
                {
                    new() { Key = "rate" }
                }
            });

            updated.ServiceProvider.ShouldBe("Custom");
            updated.ApiEndpoint.ShouldBe("https://custom-rates.example.com/api/v1/rates");
            updated.AccessKey.ShouldBe("test_key_123");
            updated.ReqParams.Count.ShouldBe(2);
            updated.ResultKeys.Count.ShouldBe(1);
        });
    }
}
