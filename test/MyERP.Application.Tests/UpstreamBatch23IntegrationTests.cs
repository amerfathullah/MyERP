using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

/// <summary>
/// End-to-end Application + EF Core SQLite integration tests for Batch 23 upstream parity:
/// 1) General Ledger Foreign Currency Account without ConvertAtReportingDateRate shows account currency (PR #65016 / commit e8ae93e729).
/// 2) General Ledger Foreign Currency Account with ConvertAtReportingDateRate converts at ToDate rate (PR #65016 / commit e8ae93e729).
/// 3) POS Closing Entry Cancellation restores Serial and Batch Bundles to source POS invoices (PR #64756 / commit e553c17711).
/// 4) Cash Flow Statement with AccumulatedValues preserves opening cash across periods in the same fiscal year (PR #64924 &amp; #64939).
/// </summary>
public abstract class UpstreamBatch23IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GeneralLedger_ForeignCurrencyAccount_WithoutConvertAtReportingDateRate_ShowsAccountCurrency()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var currencyExchangeRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var glService = GetRequiredService<IGeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "GL Test Co B23") { CurrencyCode = "MYR" }, autoSave: true);

            // Foreign currency bank account (USD)
            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110-USD", "USD Bank Account", AccountType.Asset)
            {
                Currency = "USD",
                AccountSubType = AccountSubType.BankAccount
            }, autoSave: true);

            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1100-MYR", "Cash", AccountType.Asset)
            {
                Currency = "MYR",
                AccountSubType = AccountSubType.CashAccount
            }, autoSave: true);

            // Post JE 10 days ago: Debit USD Bank 100 USD (exchange rate 4.5 -> Amount = 450 MYR, AmountInAccountCurrency = 100 USD).
            // Credit Cash 450 MYR.
            var today = DateTime.UtcNow.Date;
            var postingDate = today.AddDays(-10);

            var fiscalYearRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var fiscalYear = await fiscalYearRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
                autoSave: true);

            var je = new JournalEntry(Guid.NewGuid(), company.Id, fiscalYear.Id, postingDate);
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, usdAccount.Id, 450m, true, "USD", 100m, 4.5m, "Deposit USD"));
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, cashAccount.Id, 450m, false, "Cash Outflow"));
            je.Post();
            await journalRepo.InsertAsync(je, autoSave: true);

            // Add CurrencyExchange on ToDate (today): 1 USD = 5.0 MYR
            await currencyExchangeRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 5.0m, today), autoSave: true);

            // Query GL report: FromDate = 5 days ago, ToDate = today (so JE is in Opening Balance)
            var report = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = today.AddDays(-5),
                ToDate = today,
                ConvertAtReportingDateRate = false
            });

            // Without ConvertAtReportingDateRate, opening debit shows the account currency amount (100 USD), not company currency (450 MYR)
            report.OpeningBalance.ShouldBe(100m);
            report.PresentationCurrency.ShouldBe("USD");
        });
    }

    [Fact]
    public async Task GeneralLedger_ForeignCurrencyAccount_ShowAmountInCompanyCurrency_KeepsCompanyCurrency()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var glService = GetRequiredService<IGeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "GL Company Ccy Co B23") { CurrencyCode = "MYR" }, autoSave: true);
            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110-USD-CC", "USD Account CC", AccountType.Asset)
            {
                Currency = "USD",
                AccountSubType = AccountSubType.BankAccount
            }, autoSave: true);
            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1100-MYR-CC", "Cash CC", AccountType.Asset)
            {
                Currency = "MYR",
                AccountSubType = AccountSubType.CashAccount
            }, autoSave: true);

            var today = DateTime.UtcNow.Date;
            var fiscalYear = await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY CC", today.AddYears(-1), today.AddYears(1)),
                autoSave: true);

            // 100 USD booked at 4.5 = 450 MYR, inside the report period.
            var je = new JournalEntry(Guid.NewGuid(), company.Id, fiscalYear.Id, today.AddDays(-2));
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, usdAccount.Id, 450m, true, "USD", 100m, 4.5m, "Deposit USD"));
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, cashAccount.Id, 450m, false, "Cash Outflow"));
            je.Post();
            await journalRepo.InsertAsync(je, autoSave: true);

            var filter = new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = today.AddDays(-5),
                ToDate = today,
            };

            // Default: the foreign currency account is presented in its own currency.
            var inAccountCurrency = await glService.GetReportAsync(filter);
            inAccountCurrency.TotalDebit.ShouldBe(100m);
            inAccountCurrency.PresentationCurrency.ShouldBe("USD");

            // Opt-out: company currency amounts.
            filter.ShowAmountInCompanyCurrency = true;
            var inCompanyCurrency = await glService.GetReportAsync(filter);
            inCompanyCurrency.TotalDebit.ShouldBe(450m);
            inCompanyCurrency.PresentationCurrency.ShouldBe("MYR");

            // The opt-out and a foreign presentation currency contradict each other.
            filter.PresentationCurrency = "USD";
            var exception = await Should.ThrowAsync<BusinessException>(() => glService.GetReportAsync(filter));
            exception.Code.ShouldBe(MyERPDomainErrorCodes.PresentationCurrencyNotCompanyCurrency);
        });
    }

    [Fact]
    public async Task GeneralLedger_ForeignCurrencyAccount_WithConvertAtReportingDateRate_ConvertsAtToDateRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var currencyExchangeRepo = GetRequiredService<IRepository<CurrencyExchange, Guid>>();
            var glService = GetRequiredService<IGeneralLedgerAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "GL Reporting Rate Co B23") { CurrencyCode = "MYR" }, autoSave: true);

            var usdAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1110-USD-RR", "USD Account RR", AccountType.Asset)
            {
                Currency = "USD",
                AccountSubType = AccountSubType.BankAccount
            }, autoSave: true);

            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1100-MYR-RR", "Cash RR", AccountType.Asset)
            {
                Currency = "MYR",
                AccountSubType = AccountSubType.CashAccount
            }, autoSave: true);

            var today = DateTime.UtcNow.Date;
            var postingDate = today.AddDays(-10);

            var fiscalYearRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var fiscalYear = await fiscalYearRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
                autoSave: true);

            var je = new JournalEntry(Guid.NewGuid(), company.Id, fiscalYear.Id, postingDate);
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, usdAccount.Id, 450m, true, "USD", 100m, 4.5m, "Deposit USD"));
            je.AddLine(new JournalEntryLine(Guid.NewGuid(), je.Id, cashAccount.Id, 450m, false, "Cash Outflow"));
            je.Post();
            await journalRepo.InsertAsync(je, autoSave: true);

            // Exchange rate at ToDate: 1 USD = 5.0 MYR
            await currencyExchangeRepo.InsertAsync(new CurrencyExchange(Guid.NewGuid(), "USD", "MYR", 5.0m, today), autoSave: true);

            var report = await glService.GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = company.Id,
                AccountId = usdAccount.Id,
                FromDate = today.AddDays(-5),
                ToDate = today,
                ConvertAtReportingDateRate = true
            });

            // With ConvertAtReportingDateRate, 450 MYR is converted at today's rate (5.0 MYR/USD) = 90 USD
            report.OpeningBalance.ShouldBe(90m);
            report.PresentationCurrency.ShouldBe("USD");
        });
    }

    [Fact]
    public async Task PosClosing_CancelAsync_RestoresBundlesAndReversesConsolidation()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var profileRepo = GetRequiredService<IRepository<PosProfile, Guid>>();
            var openingRepo = GetRequiredService<IRepository<PosOpeningEntry, Guid>>();
            var invoiceRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var closingRepo = GetRequiredService<IRepository<PosClosingEntry, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var closingAppService = GetRequiredService<IPosClosingAppService>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "POS Closing Co B23") { CurrencyCode = "MYR" }, autoSave: true);

            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var costCenter = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Main CC POS"), autoSave: true);
            var receivable = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1310-REC-B23", "Receivable B23", AccountType.Asset), autoSave: true);
            var income = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "4110-INC-B23", "Sales Revenue B23", AccountType.Revenue), autoSave: true);
            company.DefaultReceivableAccountId = receivable.Id;
            company.DefaultIncomeAccountId = income.Id;
            company.DefaultCostCenterId = costCenter.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            await fyRepo.InsertAsync(new FiscalYear(Guid.NewGuid(), company.Id, "FY2026-POS", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "JournalEntry", "JournalEntry", "JV-POS-"), autoSave: true);

            var ruleRepo = GetRequiredService<IRepository<AccountingRule, Guid>>();
            await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI DR Rec", "SalesInvoice", true, AccountSource.CustomerReceivable, AmountSource.GrandTotal) { SortOrder = 1 }, autoSave: true);
            await ruleRepo.InsertAsync(new AccountingRule(Guid.NewGuid(), company.Id, "SI CR Inc", "SalesInvoice", false, AccountSource.FixedAccount, AmountSource.GrandTotal) { SortOrder = 2, FixedAccountId = income.Id }, autoSave: true);
            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "POS Customer B23"), autoSave: true);
            var warehouse = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "POS Store B23"), autoSave: true);
            var profile = await profileRepo.InsertAsync(new PosProfile(Guid.NewGuid(), company.Id, "Main POS Profile B23", warehouse.Id), autoSave: true);
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SalesInvoice", "SalesInvoice", "SINV-2026-"), autoSave: true);

            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-BATCH-01", "Batch Tracked Item", ItemType.Goods)
            {
                MaintainStock = true
            }, autoSave: true);

            var userId = Guid.NewGuid();
            var opening = await openingRepo.InsertAsync(new PosOpeningEntry(Guid.NewGuid(), company.Id, profile.Id, userId), autoSave: true);

            // Source POS Invoice
            var posInvoice = new SalesInvoice(Guid.NewGuid(), company.Id, customer.Id, "POS-INV-001", DateTime.UtcNow.Date);
            posInvoice.IsPos = true;
            posInvoice.AddItem(item.Id, "Batch Item", 2m, 50m, 0m);
            posInvoice.Submit();
            await invoiceRepo.InsertAsync(posInvoice, autoSave: true);

            // Create bundle for this POS Invoice
            var bundle = new SerialAndBatchBundle(
                Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
                BundleTransactionType.Outward, "SalesInvoice", posInvoice.Id, DateTime.UtcNow.Date);
            bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 2m, 50m));
            await bundleRepo.InsertAsync(bundle, autoSave: true);

            var mopRepo = GetRequiredService<IRepository<ModeOfPayment, Guid>>();
            var mop = await mopRepo.InsertAsync(new ModeOfPayment(Guid.NewGuid(), "Cash", "Cash") { DefaultAccountId = receivable.Id }, autoSave: true);

            // Create POS Closing Entry
            var closingEntry = new PosClosingEntry(Guid.NewGuid(), company.Id, profile.Id, opening.Id, userId);
            closingEntry.AddInvoice(posInvoice.Id, posInvoice.InvoiceNumber, posInvoice.GrandTotal);
            closingEntry.AddPayment(mop.Id, "Cash", 100m, 100m);
            await closingRepo.InsertAsync(closingEntry, autoSave: true);

            opening.Close(closingEntry.Id);
            await openingRepo.UpdateAsync(opening, autoSave: true);

            // Submit closing entry (creates consolidated SI and updates bundle voucher)
            await closingAppService.SubmitAsync(closingEntry.Id);

            var submittedEntry = await closingRepo.GetAsync(closingEntry.Id);
            submittedEntry.Status.ShouldBe(PosClosingStatus.Submitted);
            submittedEntry.ConsolidatedSalesInvoiceId.ShouldNotBeNull();
            var consolidatedSiId = submittedEntry.ConsolidatedSalesInvoiceId.Value;

            // Bundle now references consolidated SI
            var consolidatedBundle = await bundleRepo.GetAsync(bundle.Id);
            consolidatedBundle.VoucherId.ShouldBe(consolidatedSiId);

            // Cancel closing entry: must restore bundle voucherId back to posInvoice.Id
            await closingAppService.CancelAsync(closingEntry.Id);

            var cancelledEntry = await closingRepo.GetAsync(closingEntry.Id);
            cancelledEntry.Status.ShouldBe(PosClosingStatus.Cancelled);
            cancelledEntry.ConsolidatedSalesInvoiceId.ShouldBeNull();

            var restoredBundle = await bundleRepo.GetAsync(bundle.Id);
            restoredBundle.VoucherId.ShouldBe(posInvoice.Id);
            restoredBundle.IsCancelled.ShouldBeFalse();

            var cancelledConsolidatedSi = await invoiceRepo.FindAsync(consolidatedSiId);
            cancelledConsolidatedSi?.Status.ShouldBe(DocumentStatus.Cancelled);
        });
    }

    [Fact]
    public async Task CashFlow_Statement_AccumulatedValues_EndToEnd()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var journalRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var cashFlowService = GetRequiredService<ICashFlowStatementAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "CF Co B23") { CurrencyCode = "MYR" }, autoSave: true);

            var fy = await fyRepo.InsertAsync(new FiscalYear(
                Guid.NewGuid(), company.Id, "FY2026-CF", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var cashAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "1100-CF", "Cash Account", AccountType.Asset)
            {
                AccountSubType = AccountSubType.CashAccount
            }, autoSave: true);

            var equityAccount = await accountRepo.InsertAsync(new Account(Guid.NewGuid(), company.Id, "3100-CF", "Capital Stock", AccountType.Equity), autoSave: true);

            // Entry 1: 2026-01-05 (outside range if range starts 2026-04-01)
            var je1 = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 1, 5));
            je1.AddLine(new JournalEntryLine(Guid.NewGuid(), je1.Id, cashAccount.Id, 1000m, true, "Capital Q1"));
            je1.AddLine(new JournalEntryLine(Guid.NewGuid(), je1.Id, equityAccount.Id, 1000m, false, "Equity Q1"));
            je1.Post();
            await journalRepo.InsertAsync(je1, autoSave: true);

            // Entry 2: 2026-04-10 (inside range 2026-04-01 to 2026-12-31)
            var je2 = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 4, 10));
            je2.AddLine(new JournalEntryLine(Guid.NewGuid(), je2.Id, cashAccount.Id, 500m, true, "Capital Q2"));
            je2.AddLine(new JournalEntryLine(Guid.NewGuid(), je2.Id, equityAccount.Id, 500m, false, "Equity Q2"));
            je2.Post();
            await journalRepo.InsertAsync(je2, autoSave: true);

            var cfResult = await cashFlowService.GetCashFlowStatementAsync(new CashFlowRequestDto
            {
                CompanyId = company.Id,
                FromDate = new DateTime(2026, 4, 1),
                ToDate = new DateTime(2026, 12, 31),
                Periodicity = "Quarterly",
                AccumulatedValues = true,
                ShowOpeningAndClosingBalance = true
            });

            // Opening balance before 2026-04-01 was 1000
            cfResult.OpeningCashBalance.ShouldBe(1000m);
            // In accumulated date range, movement counted within range is 500 (financing)
            cfResult.FinancingTotal.ShouldBe(500m);
            cfResult.NetCashChange.ShouldBe(500m);
            cfResult.ClosingCashBalance.ShouldBe(1500m);
        });
    }
}
