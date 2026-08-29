using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for SubscriptionAppService.GenerateCatchUpInvoicesAsync: had zero callers
/// anywhere, backend or Angular, and zero test coverage, despite its own doc comment describing it
/// as reachable via "background job or manual trigger". No background job called it either — the
/// only path was a manual trigger that had no button. Added a "Generate Catch-Up Invoices" button
/// to the subscription detail page; this test covers the backend it now actually reaches.
/// </summary>
public abstract class SubscriptionCatchUpInvoiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GenerateCatchUpInvoicesAsync_GeneratesOneInvoicePerMissedPeriod_AndAdvancesToCurrentPeriod()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Catch-Up Test Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Subscription Catch-Up Cust"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCATCH-1", "Subscription Catch-Up Item", ItemType.Goods), autoSave: true);

            // Generated invoices now Submit+Post (see SubscriptionAppService.SubmitAndPostInvoiceAsync),
            // so GL posting needs a real accounts/rules/fiscal-year fixture, same as any other
            // AppService test that exercises GlRepostService.RebuildSalesInvoiceGlAsync.
            await SeedGlFixtureAsync(companyRepository, company);

            var today = DateTime.UtcNow.Date;
            var start = today.AddMonths(-2);

            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", start, "Monthly")
            {
                SubscriptionNumber = "SUB-CATCHUP-001",
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 100m, itemName: "Subscription Catch-Up Item");
            sub.CurrentInvoiceStart = start;
            sub.CurrentInvoiceEnd = start.AddMonths(1).AddDays(-1); // one full period, ends well before today
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            var results = await subscriptionAppService.GenerateCatchUpInvoicesAsync(sub.Id);

            // Two full monthly periods were missed between start and today.
            results.Count.ShouldBe(2);
            results.ShouldAllBe(r => r.GrandTotal == 100m);

            var invoiceCount = (await salesInvoiceRepository.GetQueryableAsync())
                .Count(i => i.CompanyId == company.Id && i.CustomerId == customer.Id);
            invoiceCount.ShouldBe(2);

            var reloaded = await subscriptionRepository.GetAsync(sub.Id);
            reloaded.CurrentInvoiceEnd.ShouldBe(today.AddMonths(1).AddDays(-1));
            reloaded.Status.ShouldBe(SubscriptionStatus.Active);
        });
    }

    [Fact]
    public async Task GenerateCatchUpInvoicesAsync_NoMissedPeriods_ReturnsEmpty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Catch-Up Test Co 2"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Subscription Catch-Up Cust 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCATCH-2", "Subscription Catch-Up Item 2", ItemType.Goods), autoSave: true);

            var today = DateTime.UtcNow.Date;

            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", today, "Monthly")
            {
                SubscriptionNumber = "SUB-CATCHUP-002",
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 50m, itemName: "Subscription Catch-Up Item 2");
            sub.CurrentInvoiceStart = today;
            sub.CurrentInvoiceEnd = today.AddMonths(1).AddDays(-1); // current period, not yet due
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            var results = await subscriptionAppService.GenerateCatchUpInvoicesAsync(sub.Id);

            results.ShouldBeEmpty();
        });
    }

    /// <summary>
    /// Minimal accounts/rules/fiscal-year fixture a SalesInvoice needs to actually Post() —
    /// GlRepostService.RebuildSalesInvoiceGlAsync -&gt; AccountingRuleEngine.PostDocumentAsync needs
    /// a matching AccountingRule row per document type (companies created via the real
    /// ICompanyAppService get 11 of these seeded automatically; a bare `new Company(...)` here
    /// does not). Only DR Receivable and CR Revenue are needed: "SI CR Tax" resolves to a
    /// zero-amount line for these subscription test invoices (TaxAmount is always 0) and the rule
    /// engine skips zero-amount lines entirely, so no tax account setup is required.
    /// </summary>
    private async Task SeedGlFixtureAsync(IRepository<Company, Guid> companyRepository, Company company)
    {
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
        var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();

        var receivableAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "1130-" + company.Id.ToString("N")[..6], "Test Receivable", AccountType.Asset), autoSave: true);
        var incomeAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, "4000-" + company.Id.ToString("N")[..6], "Test Revenue", AccountType.Revenue), autoSave: true);
        // Every P&L (Revenue/Expense) GL line needs a cost center — AccountingDimensionService
        // falls back to Company.DefaultCostCenterId when the line itself doesn't carry one, but
        // throws CostCenterRequiredForPlAccount if neither is set.
        var costCenter = await costCenterRepository.InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, "Test Cost Center"), autoSave: true);

        company.DefaultReceivableAccountId = receivableAccount.Id;
        company.DefaultIncomeAccountId = incomeAccount.Id;
        company.DefaultCostCenterId = costCenter.Id;
        await companyRepository.UpdateAsync(company, autoSave: true);

        await fiscalYearRepository.InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, "FY Test", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
            autoSave: true);

        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Receivable", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 },
            autoSave: true);
        await ruleRepository.InsertAsync(
            new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Revenue", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 },
            autoSave: true);
    }
}
