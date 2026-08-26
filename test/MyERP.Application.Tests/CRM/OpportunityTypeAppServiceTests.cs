using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.CRM;

public abstract class OpportunityTypeAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IOpportunityTypeAppService _typeAppService;

    protected OpportunityTypeAppServiceTests()
    {
        _typeAppService = GetRequiredService<IOpportunityTypeAppService>();
    }

    [Fact]
    public async Task CreateAsync_And_GetListAsync_ShouldWork()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _typeAppService.CreateAsync(new CreateUpdateOpportunityTypeDto
            {
                Name = "Cloud Migration Services",
                Description = "Opportunity type for cloud infrastructure migration",
                IsActive = true
            });

            created.Id.ShouldNotBe(Guid.Empty);
            created.Name.ShouldBe("Cloud Migration Services");

            var list = await _typeAppService.GetListAsync(new GetOpportunityTypeListDto { Filter = "Cloud" });
            list.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
            list.Items.ShouldContain(x => x.Name == "Cloud Migration Services");
        });
    }
}
