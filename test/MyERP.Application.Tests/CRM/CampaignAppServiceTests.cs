using System;
using System.Threading.Tasks;
using MyERP.CRM;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class CampaignAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_And_GetUtmCampaignAsync_ReturnsCampaignName()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var service = GetRequiredService<ICampaignAppService>();

            var created = await service.CreateAsync(new CreateUpdateCampaignDto
            {
                CampaignName = "Winter_Promotions_2026",
                Description = "Winter discounts and promos"
            });

            created.CampaignName.ShouldBe("Winter_Promotions_2026");

            // Per ERPNext PR #59906 / commit 8afb4a6e39: get_utm_campaign resolves campaign name for lead filtering
            var utmCampaign = await service.GetUtmCampaignAsync(created.Id);
            utmCampaign.ShouldBe("Winter_Promotions_2026");
        });
    }

    [Fact]
    public async Task DeleteAsync_DeletesCampaign()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var service = GetRequiredService<ICampaignAppService>();

            var created = await service.CreateAsync(new CreateUpdateCampaignDto
            {
                CampaignName = "Temporary_Flash_Sale",
                Description = "Temporary flash sale campaign"
            });

            await service.DeleteAsync(created.Id);

            var list = await service.GetListAsync(new GetCampaignListDto { Filter = "Temporary_Flash_Sale" });
            list.TotalCount.ShouldBe(0);
        });
    }
}
