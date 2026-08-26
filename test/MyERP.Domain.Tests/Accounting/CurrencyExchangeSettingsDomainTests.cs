using System;
using MyERP.Accounting.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Accounting;

public class CurrencyExchangeSettingsDomainTests
{
    [Fact]
    public void Should_Create_Valid_CurrencyExchangeSettings()
    {
        var id = Guid.NewGuid();
        var settings = new CurrencyExchangeSettings(
            id,
            serviceProvider: "frankfurter.dev",
            apiEndpoint: "https://api.frankfurter.dev/v1/{transaction_date}");

        settings.Id.ShouldBe(id);
        settings.ServiceProvider.ShouldBe("frankfurter.dev");
        settings.ApiEndpoint.ShouldBe("https://api.frankfurter.dev/v1/{transaction_date}");
        settings.Disabled.ShouldBeFalse();

        settings.AddParam(Guid.NewGuid(), "base", "{from_currency}");
        settings.AddResultKey(Guid.NewGuid(), "rates");

        settings.ReqParams.Count.ShouldBe(1);
        settings.ResultKeys.Count.ShouldBe(1);
    }
}
