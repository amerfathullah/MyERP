using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for ProformaInvoiceAppService.SendEmailAsync: the detail page already
/// rendered an "Emailed to X on Y" banner for SentOn/EmailedTo, but had no action anywhere to ever
/// populate those fields — the banner could never appear. Domain-level MarkEmailed had test coverage
/// (UpstreamJuly24Tests) but the AppService round-trip (repository fetch/update) did not. Added a
/// "Send Email" panel to the detail page; this test covers the AppService layer.
/// </summary>
public abstract class ProformaInvoiceSendEmailTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SendEmailAsync_RecordsRecipientsAndTimestamp()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var proformaRepository = GetRequiredService<IRepository<ProformaInvoice, Guid>>();
            var proformaAppService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Proforma Email Test Co"), autoSave: true);

            var proforma = new ProformaInvoice(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date)
            {
                ProformaNumber = "PFI-TEST-001",
            };
            proforma.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", "Proforma Test Item", 1m, 100m);
            proforma.Submit();
            await proformaRepository.InsertAsync(proforma, autoSave: true);

            await proformaAppService.SendEmailAsync(proforma.Id, new SendProformaEmailDto { Recipients = "customer@example.com" });

            var reloaded = await proformaRepository.GetAsync(proforma.Id);
            reloaded.EmailedTo.ShouldBe("customer@example.com");
            reloaded.SentOn.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task SendEmailAsync_OnCancelledProforma_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var proformaRepository = GetRequiredService<IRepository<ProformaInvoice, Guid>>();
            var proformaAppService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Proforma Email Test Co 2"), autoSave: true);

            var proforma = new ProformaInvoice(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date)
            {
                ProformaNumber = "PFI-TEST-001",
            };
            proforma.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", "Proforma Test Item", 1m, 100m);
            proforma.Submit();
            proforma.Cancel();
            await proformaRepository.InsertAsync(proforma, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                proformaAppService.SendEmailAsync(proforma.Id, new SendProformaEmailDto { Recipients = "customer@example.com" }));
        });
    }
}
