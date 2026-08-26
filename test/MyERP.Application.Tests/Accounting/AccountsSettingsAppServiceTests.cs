using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class AccountsSettingsAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAccountsSettingsAppService _settingsAppService;

    protected AccountsSettingsAppServiceTests()
    {
        _settingsAppService = GetRequiredService<IAccountsSettingsAppService>();
    }

    [Fact]
    public async Task AccountsSettings_Should_Get_And_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settings = await _settingsAppService.GetAsync();
            settings.ShouldNotBeNull();
            settings.EnableSubscription.ShouldBeTrue();

            var updated = await _settingsAppService.UpdateAsync(new UpdateAccountsSettingsDto
            {
                UnlinkPaymentOnCancellationOfInvoice = false,
                EnableSubscription = true,
                OverBillingAllowance = 15.5m,
                CreditControllerRole = "Accounts Manager",
                DefaultAgeingRange = "15, 30, 45, 60",
                StaleDays = 3
            });

            updated.UnlinkPaymentOnCancellationOfInvoice.ShouldBeFalse();
            updated.OverBillingAllowance.ShouldBe(15.5m);
            updated.CreditControllerRole.ShouldBe("Accounts Manager");
            updated.DefaultAgeingRange.ShouldBe("15, 30, 45, 60");
            updated.StaleDays.ShouldBe(3);
        });
    }
}
