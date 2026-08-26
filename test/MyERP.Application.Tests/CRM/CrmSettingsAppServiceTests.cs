using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class CrmSettingsAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICrmSettingsAppService _settingsAppService;

    protected CrmSettingsAppServiceTests()
    {
        _settingsAppService = GetRequiredService<ICrmSettingsAppService>();
    }

    [Fact]
    public async Task CrmSettings_Should_Get_And_Update()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var settings = await _settingsAppService.GetAsync();
            settings.ShouldNotBeNull();
            settings.CampaignNamingBy.ShouldBe("Campaign Name");

            var updated = await _settingsAppService.UpdateAsync(new UpdateCrmSettingsDto
            {
                CampaignNamingBy = "Naming Series",
                AllowLeadDuplicationBasedOnEmails = true,
                AutoCreationOfContact = true,
                CloseOpportunityAfterDays = 45,
                DefaultQuotationValidityDays = 60
            });

            updated.CampaignNamingBy.ShouldBe("Naming Series");
            updated.CloseOpportunityAfterDays.ShouldBe(45);
            updated.DefaultQuotationValidityDays.ShouldBe(60);
        });
    }
}
