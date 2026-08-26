using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class ChequePrintTemplateAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IChequePrintTemplateAppService _templateAppService;

    protected ChequePrintTemplateAppServiceTests()
    {
        _templateAppService = GetRequiredService<IChequePrintTemplateAppService>();
    }

    [Fact]
    public async Task CreateAsync_And_GeneratePreviewAsync_ShouldWork()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _templateAppService.CreateAsync(new CreateUpdateChequePrintTemplateDto
            {
                BankName = "Public Bank",
                ChequeSize = ChequeSize.Regular,
                ChequeWidth = 20.00m,
                ChequeHeight = 9.00m,
                IsAccountPayable = true,
                MessageToShow = "Acc. Payee Only"
            });

            created.Id.ShouldNotBe(Guid.Empty);
            created.BankName.ShouldBe("Public Bank");

            var preview = await _templateAppService.GeneratePreviewAsync(created.Id);
            preview.HtmlContent.ShouldNotBeNullOrWhiteSpace();
            preview.HtmlContent.ShouldContain("Acc. Payee Only");
            preview.HtmlContent.ShouldContain("{{ party_name }}");
            preview.HtmlContent.ShouldContain("{{ amount_in_words }}");
        });
    }
}
