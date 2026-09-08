using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Tax;

/// <summary>
/// Regression coverage for a gap found while auditing the whole codebase for round-120/122's
/// missing-eager-load bug class: TaxChargesTemplate.Rows has no EF AutoInclude, and every read
/// path in TaxChargesTemplateAppService used a plain GetAsync/GetQueryableAsync — so every
/// template, everywhere (list, single get, the default-template lookup Sales/Purchase Invoice
/// forms auto-apply on create, and the enable/disable toggle), always came back with an empty
/// Rows collection. Selecting or auto-applying a Tax Charges Template silently added zero tax
/// lines to the invoice.
/// </summary>
public abstract class TaxChargesTemplateAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetDefaultAsync_ReturnsTemplateWithItsRows()
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var templateAppService = GetRequiredService<ITaxChargesTemplateAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Tax Template Test Co"), autoSave: true);
        var taxAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "21TAX", "Test Output Tax", AccountType.Liability), autoSave: true);

        var created = await templateAppService.CreateAsync(new CreateTaxChargesTemplateDto
        {
            CompanyId = company.Id,
            Name = "Standard Sales Tax",
            TemplateType = TaxTemplateType.Selling,
            IsDefault = true,
            Rows =
            {
                new CreateTaxChargesTemplateRowDto { ChargeType = "On Net Total", Rate = 6m, AccountId = taxAccount.Id },
            },
        });
        created.Rows.Count.ShouldBe(1); // sanity: the in-memory object from Create already has it

        // The bug specifically manifested on a FRESH read, not the object handed back by Create.
        var fetched = await templateAppService.GetAsync(created.Id);
        fetched.Rows.Count.ShouldBe(1);
        fetched.Rows[0].Rate.ShouldBe(6m);

        var defaultTemplate = await templateAppService.GetDefaultAsync(company.Id, TaxTemplateType.Selling);
        defaultTemplate.ShouldNotBeNull();
        defaultTemplate!.Rows.Count.ShouldBe(1);

        var active = await templateAppService.GetActiveTemplatesAsync(company.Id, TaxTemplateType.Selling);
        active.ShouldContain(t => t.Id == created.Id && t.Rows.Count == 1);

        var toggled = await templateAppService.ToggleEnabledAsync(created.Id);
        toggled.Rows.Count.ShouldBe(1);
    }
}
