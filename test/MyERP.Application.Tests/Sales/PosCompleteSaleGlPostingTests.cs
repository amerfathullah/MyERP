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
/// Regression coverage for PosAppService.CompleteSaleAsync's GL posting: it called
/// invoice.Submit()/invoice.Post() directly but never GlRepostService.RebuildSalesInvoiceGlAsync
/// (the call SalesInvoiceAppService.PostAsync makes) — SalesInvoice.Post() only flips status and
/// raises SalesInvoicePostedEvent, which has no handler anywhere in the codebase. Every POS sale
/// reached "Posted" status with zero AR/revenue journal entries. Fixed in the same session that
/// added this test — see SubscriptionCatchUpInvoiceTests.SeedGlFixtureAsync for the rationale
/// behind the accounts/rules/fiscal-year/cost-center fixture this needs.
/// </summary>
public abstract class PosCompleteSaleGlPostingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CompleteSaleAsync_PostsBalancedJournalEntry_WithReceivableAndRevenueLines()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS GL Test Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "POS GL Cust"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POSGL-1", "POS GL Item", ItemType.Goods), autoSave: true);

            var receivableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-POS", "Test Receivable", AccountType.Asset), autoSave: true);
            var incomeAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-POS", "Test Revenue", AccountType.Revenue), autoSave: true);
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

            // CompleteSaleAsync requires an active (Open) POS session for the company.
            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid()), autoSave: true);

            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "POS Series", "POS", "POS-"), autoSave: true);

            var result = await posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                // No WarehouseId: keeps this test scoped to GL posting, not stock valuation.
                Items =
                {
                    new PosLineItemDto { ItemId = item.Id, Description = "POS GL Item", Quantity = 2, UnitPrice = 50m, TaxAmount = 0m },
                },
                AmountReceived = 100m,
            });

            result.Status.ShouldBe("Posted");
            result.AmountReceived.ShouldBe(100m);
            result.Change.ShouldBe(0m);
            result.BaseChange.ShouldBe(0m);

            var invoice = await salesInvoiceRepository.GetAsync(result.Id);
            invoice.Status.ShouldBe(Core.DocumentStatus.Posted);
            invoice.AmountPaid.ShouldBe(100m);
            invoice.OutstandingAmount.ShouldBe(0m);

            var journal = (await journalRepository.GetQueryableAsync())
                .SingleOrDefault(j => j.ReferenceType == "SalesInvoice" && j.ReferenceId == invoice.Id);
            journal.ShouldNotBeNull();

            var lines = journal!.Lines.ToList();
            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));

            var receivableLine = lines.SingleOrDefault(l => l.AccountId == receivableAccount.Id && l.IsDebit);
            receivableLine.ShouldNotBeNull();
            receivableLine!.Amount.ShouldBe(100m);

            var revenueLine = lines.SingleOrDefault(l => l.AccountId == incomeAccount.Id && !l.IsDebit);
            revenueLine.ShouldNotBeNull();
            revenueLine!.Amount.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CompleteSaleAsync_MultiCurrency_ComputesBaseChangeAndMaintainsGlBalance()
    {
        // Per ERPNext PR #58599 / commit f16f249a38:
        // When POS sale is multi-currency, change is netted in company base currency:
        // BaseChange = Change * ExchangeRate.
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var ruleRepository = GetRequiredService<IRepository<AccountingRule, Guid>>();
            var costCenterRepository = GetRequiredService<IRepository<CostCenter, Guid>>();
            var posOpeningRepository = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var posAppService = GetRequiredService<IPosAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "POS Multi-Curr Co") { CurrencyCode = "MYR" }, autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "POS Multi-Curr Cust"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "POSMC-1", "POS Multi-Curr Item", ItemType.Goods), autoSave: true);

            var receivableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130-POSMC", "Test Receivable MC", AccountType.Asset), autoSave: true);
            var incomeAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-POSMC", "Test Revenue MC", AccountType.Revenue), autoSave: true);
            var costCenter = await costCenterRepository.InsertAsync(
                new CostCenter(Guid.NewGuid(), company.Id, "Test Cost Center MC"), autoSave: true);

            company.DefaultReceivableAccountId = receivableAccount.Id;
            company.DefaultIncomeAccountId = incomeAccount.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY Test MC", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)),
                autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Receivable MC", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 },
                autoSave: true);
            await ruleRepository.InsertAsync(
                new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Revenue MC", "SalesInvoice", false, AccountSource.ItemIncome, AmountSource.NetTotal) { SortOrder = 2 },
                autoSave: true);

            await posOpeningRepository.InsertAsync(
                new PosOpeningEntry(Guid.NewGuid(), company.Id, Guid.NewGuid(), Guid.NewGuid()), autoSave: true);

            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "POS Series MC", "POS", "POS-"), autoSave: true);

            // Sale in USD (100 USD total), exchange rate = 50, amount received = 150 USD.
            // Change = 50 USD, BaseChange = 2500 MYR.
            var result = await posAppService.CompleteSaleAsync(new CreatePosInvoiceDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                CurrencyCode = "USD",
                ExchangeRate = 50m,
                Items =
                {
                    new PosLineItemDto { ItemId = item.Id, Description = "POS Multi-Curr Item", Quantity = 2, UnitPrice = 50m, TaxAmount = 0m },
                },
                AmountReceived = 150m,
            });

            result.Status.ShouldBe("Posted");
            result.AmountReceived.ShouldBe(150m);
            result.Change.ShouldBe(50m);
            result.BaseChange.ShouldBe(2500m);

            var invoice = await salesInvoiceRepository.GetAsync(result.Id);
            invoice.Status.ShouldBe(Core.DocumentStatus.Posted);
            invoice.CurrencyCode.ShouldBe("USD");
            invoice.ExchangeRate.ShouldBe(50m);
            invoice.GrandTotal.ShouldBe(100m);
            invoice.BaseGrandTotal.ShouldBe(5000m);
            invoice.AmountPaid.ShouldBe(100m);
            invoice.OutstandingAmount.ShouldBe(0m);

            var journal = (await journalRepository.GetQueryableAsync())
                .SingleOrDefault(j => j.ReferenceType == "SalesInvoice" && j.ReferenceId == invoice.Id);
            journal.ShouldNotBeNull();

            var lines = journal!.Lines.ToList();
            lines.Sum(l => l.IsDebit ? l.Amount : 0m).ShouldBe(lines.Sum(l => !l.IsDebit ? l.Amount : 0m));

            // In company base currency (MYR), 100 USD * 50 = 5000 MYR
            var receivableLine = lines.SingleOrDefault(l => l.AccountId == receivableAccount.Id && l.IsDebit);
            receivableLine.ShouldNotBeNull();
            receivableLine!.Amount.ShouldBe(5000m);

            var revenueLine = lines.SingleOrDefault(l => l.AccountId == incomeAccount.Id && !l.IsDebit);
            revenueLine.ShouldNotBeNull();
            revenueLine!.Amount.ShouldBe(5000m);
        });
    }
}
