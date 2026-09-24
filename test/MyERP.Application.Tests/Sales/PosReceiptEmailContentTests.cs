using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

public abstract class PosReceiptEmailContentTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetReceiptEmailContentAsync_WithoutProfileTemplate_ReturnsDefaultText()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Receipt Test Co 1"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Walk-in"), autoSave: true);

            var invoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-2026-0001", DateTime.UtcNow.Date)
            {
                IsPos = true
            };
            invoice.AddItem(Guid.NewGuid(), "Item 1", 1m, 100m, 0m);
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            var content = await posAppService.GetReceiptEmailContentAsync(invoice.Id);

            content.Subject.ShouldBe("POS Invoice: POS-2026-0001");
            content.Message.ShouldBe("POS Invoice: POS-2026-0001");
        });
    }

    [Fact]
    public async Task GetReceiptEmailContentAsync_WithPosProfileTemplate_SubstitutesVariables()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var templateRepository = GetRequiredService<IRepository<EmailTemplate, Guid>>();
            var profileRepository = GetRequiredService<IRepository<PosProfile, Guid>>();
            var invoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Receipt Test Co 2"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Jane Doe"), autoSave: true);

            var template = new EmailTemplate(
                Guid.NewGuid(),
                "POS Receipt Email Template",
                "Receipt {{ doc.name }}",
                "Thanks {{ doc.customer }}, total {{ doc.grand_total }}",
                null);
            await templateRepository.InsertAsync(template, autoSave: true);

            var profile = new PosProfile(Guid.NewGuid(), company.Id, "Main Counter", Guid.NewGuid())
            {
                ReceiptEmailTemplateId = template.Id
            };
            await profileRepository.InsertAsync(profile, autoSave: true);

            var invoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-2026-0002", DateTime.UtcNow.Date)
            {
                IsPos = true,
                PosProfileId = profile.Id
            };
            invoice.AddItem(Guid.NewGuid(), "Item A", 2m, 25m, 0m);
            await invoiceRepository.InsertAsync(invoice, autoSave: true);

            var content = await posAppService.GetReceiptEmailContentAsync(invoice.Id);

            content.Subject.ShouldBe("Receipt POS-2026-0002");
            content.Message.ShouldBe("Thanks Jane Doe, total 50.00");
        });
    }
}
