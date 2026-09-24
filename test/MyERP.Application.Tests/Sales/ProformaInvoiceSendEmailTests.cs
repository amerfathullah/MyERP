using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.SettingManagement;
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

    [Fact]
    public async Task GetEmailContentAsync_WithoutTemplate_ReturnsDefaultText()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var proformaRepository = GetRequiredService<IRepository<ProformaInvoice, Guid>>();
            var proformaAppService = GetRequiredService<IProformaInvoiceAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Proforma Default Content Co"), autoSave: true);

            var proforma = new ProformaInvoice(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date)
            {
                ProformaNumber = "PRO-TEST-0001",
            };
            proforma.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", "Proforma Test Item", 1m, 100m);
            proforma.Submit();
            await proformaRepository.InsertAsync(proforma, autoSave: true);

            var content = await proformaAppService.GetEmailContentAsync(proforma.Id);

            content.Subject.ShouldBe("Proforma Invoice PRO-TEST-0001");
            content.Message.ShouldBe("Please find attached the proforma invoice PRO-TEST-0001.");
        });
    }

    [Fact]
    public async Task GetEmailContentAsync_WithTemplate_SubstitutesVariables()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var proformaRepository = GetRequiredService<IRepository<ProformaInvoice, Guid>>();
            var templateRepository = GetRequiredService<IRepository<EmailTemplate, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var proformaAppService = GetRequiredService<IProformaInvoiceAppService>();
            var settingManager = GetRequiredService<Volo.Abp.SettingManagement.ISettingManager>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Proforma Template Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Acme Corp"), autoSave: true);
            var so = new SalesOrder(Guid.NewGuid(), company.Id, customer.Id, "SO-TEST-0001", DateTime.UtcNow.Date)
            {
                CurrencyCode = "MYR"
            };
            await soRepository.InsertAsync(so, autoSave: true);

            var template = new EmailTemplate(
                Guid.NewGuid(),
                "Proforma Customer Template",
                "Proforma {{ doc.name }}",
                "Advance payment for {{ doc.sales_order }} to {{ doc.customer_name }}",
                null);
            await templateRepository.InsertAsync(template, autoSave: true);

            await settingManager.SetGlobalAsync(Settings.MyERPSettings.Selling.ProformaEmailTemplate, template.Name);

            var proforma = new ProformaInvoice(Guid.NewGuid(), company.Id, so.Id, customer.Id, DateTime.UtcNow.Date)
            {
                ProformaNumber = "PRO-TEST-0002",
            };
            proforma.AddItem(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", "Proforma Test Item", 2m, 50m);
            proforma.Submit();
            await proformaRepository.InsertAsync(proforma, autoSave: true);

            var content = await proformaAppService.GetEmailContentAsync(proforma.Id);

            content.Subject.ShouldBe("Proforma PRO-TEST-0002");
            content.Message.ShouldBe("Advance payment for SO-TEST-0001 to Acme Corp");
        });
    }
}
