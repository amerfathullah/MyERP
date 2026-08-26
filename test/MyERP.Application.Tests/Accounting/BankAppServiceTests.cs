using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class BankAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBankAppService _bankAppService;

    protected BankAppServiceTests()
    {
        _bankAppService = GetRequiredService<IBankAppService>();
    }

    [Fact]
    public async Task CreateAsync_And_GetListAsync_ShouldWork()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _bankAppService.CreateAsync(new CreateUpdateBankDto
            {
                BankName = "CIMB Bank",
                SwiftNumber = "CIBBMYKL",
                Website = "https://www.cimb.com.my",
                IsActive = true
            });

            created.Id.ShouldNotBe(Guid.Empty);
            created.BankName.ShouldBe("CIMB Bank");

            var list = await _bankAppService.GetListAsync(new GetBankListDto { Filter = "CIMB" });
            list.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
            list.Items.ShouldContain(x => x.BankName == "CIMB Bank");
        });
    }
}
