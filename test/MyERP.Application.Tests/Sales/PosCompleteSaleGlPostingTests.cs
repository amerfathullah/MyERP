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

            var invoice = await salesInvoiceRepository.GetAsync(result.Id);
            invoice.Status.ShouldBe(Core.DocumentStatus.Posted);

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
}
