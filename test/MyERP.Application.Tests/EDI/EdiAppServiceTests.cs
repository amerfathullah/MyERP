using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.EDI;

public abstract class EdiAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICodeListAppService _codeListAppService;
    private readonly ICommonCodeAppService _commonCodeAppService;

    protected EdiAppServiceTests()
    {
        _codeListAppService = GetRequiredService<ICodeListAppService>();
        _commonCodeAppService = GetRequiredService<ICommonCodeAppService>();
    }

    [Fact]
    public async Task Edi_Services_Should_Perform_CRUD_Operations()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            // Create CodeList
            var codeList = await _codeListAppService.CreateAsync(new CreateUpdateCodeListDto
            {
                Title = "ISO 3166-1 Country Codes",
                CanonicalUri = "urn:iso:std:iso:3166:-1",
                Publisher = "ISO",
                IsActive = true
            });
            codeList.Id.ShouldNotBe(Guid.Empty);
            codeList.Title.ShouldBe("ISO 3166-1 Country Codes");

            // Create CommonCode
            var commonCode = await _commonCodeAppService.CreateAsync(new CreateUpdateCommonCodeDto
            {
                CodeListId = codeList.Id,
                Title = "Malaysia",
                Code = "MYS",
                Description = "Country of Malaysia",
                IsActive = true
            });
            commonCode.Id.ShouldNotBe(Guid.Empty);
            commonCode.Code.ShouldBe("MYS");

            // Query by CodeList
            var list = await _commonCodeAppService.GetListAsync(new GetCommonCodeListDto
            {
                CodeListId = codeList.Id
            });
            list.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
            list.Items.ShouldContain(x => x.Code == "MYS");
        });
    }
}
