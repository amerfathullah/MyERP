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

    [Fact]
    public async Task Edi_CodeList_Should_Resolve_By_Uri_And_Version()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var canonicalUri = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

            // Create Version 2.0
            await _codeListAppService.CreateAsync(new CreateUpdateCodeListDto
            {
                Title = "Credit Note Codes v2.0",
                CanonicalUri = canonicalUri,
                Version = "2.0",
                DefaultCommonCode = "381",
                IsActive = true
            });

            // Create Version 2.1 (later version)
            await _codeListAppService.CreateAsync(new CreateUpdateCodeListDto
            {
                Title = "Credit Note Codes v2.1",
                CanonicalUri = canonicalUri,
                Version = "2.1",
                DefaultCommonCode = "383",
                IsActive = true
            });

            // Resolve by URI -> Should resolve to v2.1
            var resolved = await _codeListAppService.ResolveAsync(canonicalUri);
            resolved.ShouldNotBeNull();
            resolved.Version.ShouldBe("2.1");
            resolved.DefaultCommonCode.ShouldBe("383");

            // Resolve by Title -> Should resolve exact match
            var resolvedByTitle = await _codeListAppService.ResolveAsync("Credit Note Codes v2.0");
            resolvedByTitle.ShouldNotBeNull();
            resolvedByTitle.Version.ShouldBe("2.0");

            // GetDefaultCodeAsync by URI -> Should return 383
            var defaultCode = await _codeListAppService.GetDefaultCodeAsync(canonicalUri);
            defaultCode.ShouldBe("383");
        });
    }
}
