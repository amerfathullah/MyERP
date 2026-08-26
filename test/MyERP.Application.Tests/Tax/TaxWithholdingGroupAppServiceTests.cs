using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Tax;

public abstract class TaxWithholdingGroupAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITaxWithholdingGroupAppService _groupAppService;

    protected TaxWithholdingGroupAppServiceTests()
    {
        _groupAppService = GetRequiredService<ITaxWithholdingGroupAppService>();
    }

    [Fact]
    public async Task CreateAsync_And_GetListAsync_ShouldWork()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _groupAppService.CreateAsync(new CreateUpdateTaxWithholdingGroupDto
            {
                GroupName = "Section 107A Corporate",
                Description = "Withholding group for resident companies",
                IsActive = true
            });

            created.Id.ShouldNotBe(Guid.Empty);
            created.GroupName.ShouldBe("Section 107A Corporate");

            var list = await _groupAppService.GetListAsync(new GetTaxWithholdingGroupListDto { Filter = "107A" });
            list.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
            list.Items.ShouldContain(x => x.GroupName == "Section 107A Corporate");
        });
    }
}
