using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class SubscriptionSettingsAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISubscriptionSettingsAppService _settingsAppService;

    protected SubscriptionSettingsAppServiceTests()
    {
        _settingsAppService = GetRequiredService<ISubscriptionSettingsAppService>();
    }

    [Fact]
    public async Task SubscriptionSettings_Should_Get_And_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settings = await _settingsAppService.GetAsync();
            settings.ShouldNotBeNull();
            settings.GracePeriod.ShouldBe(1);

            var updated = await _settingsAppService.UpdateAsync(new UpdateSubscriptionSettingsDto
            {
                GracePeriod = 10,
                CancelAfterGrace = true,
                Prorate = true
            });

            updated.GracePeriod.ShouldBe(10);
            updated.CancelAfterGrace.ShouldBeTrue();
            updated.Prorate.ShouldBeTrue();
        });
    }
}
