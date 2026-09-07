using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Regression coverage for BankReconciliationAppService.ImportTransactionAsync, found while
/// auditing the Angular side for zero-caller proxy methods: importTransaction had no UI path at
/// all — the bank reconciliation page could bulk-import via CSV/MT940 or auto-match, but had no
/// way to manually record a single transaction the bank statement missed. Added an "Add
/// Transaction" panel; this test covers the backend it now actually reaches (previously
/// untested).
/// </summary>
public abstract class BankTransactionManualImportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ImportTransactionAsync_CreatesUnreconciledTransaction()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var bankReconciliationAppService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Bank Import Test Co"), autoSave: true);
            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9BI01", "Test Bank", AccountType.Asset), autoSave: true);

            var dto = await bankReconciliationAppService.ImportTransactionAsync(new ImportBankTransactionDto
            {
                CompanyId = company.Id,
                BankAccountId = bankAccount.Id,
                TransactionDate = DateTime.Today,
                Description = "Manual entry — missed statement line",
                Amount = 1500m,
                ReferenceNumber = "REF-001",
            });

            dto.Id.ShouldNotBe(Guid.Empty);
            dto.Amount.ShouldBe(1500m);
            dto.IsReconciled.ShouldBeFalse();

            var stored = await transactionRepository.GetAsync(dto.Id);
            stored.BankAccountId.ShouldBe(bankAccount.Id);
            stored.ReferenceNumber.ShouldBe("REF-001");
        });
    }

    [Fact]
    public async Task CreateJournalEntryFromTransactionAsync_Deposit_Should_Create_Balanced_JE_And_Reconcile()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var bankAccountRepository = GetRequiredService<IRepository<BankAccount, Guid>>();
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var bankReconciliationAppService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JE Recon Co 1"), autoSave: true);
            var bankGl = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110", "Main Bank GL", AccountType.Asset), autoSave: true);
            var interestIncome = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4100", "Interest Income", AccountType.Revenue), autoSave: true);
            var bankAccount = await bankAccountRepository.InsertAsync(
                new BankAccount(Guid.NewGuid(), company.Id, "Main Bank Account", bankGl.Id, "Maybank"), autoSave: true);

            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "JE Series", "JournalEntry", "JE-"), autoSave: true);

            var tx = await transactionRepository.InsertAsync(new BankTransaction(
                Guid.NewGuid(), company.Id, bankAccount.Id, DateTime.Today, "Interest deposit", 250m)
            {
                Deposit = 250m,
                ReferenceNumber = "DEP-REF-101",
            }, autoSave: true);

            var result = await bankReconciliationAppService.CreateJournalEntryFromTransactionAsync(new CreateJEFromTransactionDto
            {
                BankTransactionId = tx.Id,
                CompanyId = company.Id,
                SecondAccountId = interestIncome.Id,
                VoucherType = JournalEntryVoucherType.BankEntry,
                Narration = "Monthly interest income"
            });

            result.ShouldNotBeNull();
            result.JournalEntryId.ShouldNotBe(Guid.Empty);
            result.Amount.ShouldBe(250m);
            result.IsReconciled.ShouldBeTrue();

            var je = await journalEntryRepository.GetAsync(result.JournalEntryId);
            je.Status.ShouldBe(Core.DocumentStatus.Posted);
            je.TotalDebit.ShouldBe(250m);
            je.TotalCredit.ShouldBe(250m);
            je.ClearanceDate.ShouldBe(DateTime.Today);

            var reloadedTx = await transactionRepository.GetAsync(tx.Id);
            reloadedTx.IsReconciled.ShouldBeTrue();
            reloadedTx.JournalEntryId.ShouldBe(je.Id);
            reloadedTx.MatchedDocumentRef.ShouldBe(je.EntryNumber);
        });
    }

    [Fact]
    public async Task CreateJournalEntryFromTransactionAsync_Withdrawal_Should_Create_Balanced_JE_And_Reconcile()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var bankAccountRepository = GetRequiredService<IRepository<BankAccount, Guid>>();
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var bankReconciliationAppService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JE Recon Co 2"), autoSave: true);
            var bankGl = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1111", "Operating Bank GL", AccountType.Asset), autoSave: true);
            var bankCharges = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5200", "Bank Charges", AccountType.Expense), autoSave: true);
            var bankAccount = await bankAccountRepository.InsertAsync(
                new BankAccount(Guid.NewGuid(), company.Id, "Operating Account", bankGl.Id, "CIMB"), autoSave: true);

            var tx = await transactionRepository.InsertAsync(new BankTransaction(
                Guid.NewGuid(), company.Id, bankAccount.Id, DateTime.Today, "Monthly service fee", -35m)
            {
                Withdrawal = 35m,
                ReferenceNumber = "WDL-REF-202",
            }, autoSave: true);

            var result = await bankReconciliationAppService.CreateJournalEntryFromTransactionAsync(new CreateJEFromTransactionDto
            {
                BankTransactionId = tx.Id,
                CompanyId = company.Id,
                SecondAccountId = bankCharges.Id,
                VoucherType = JournalEntryVoucherType.BankEntry,
                Narration = "Monthly account service fee"
            });

            result.ShouldNotBeNull();
            result.JournalEntryId.ShouldNotBe(Guid.Empty);
            result.Amount.ShouldBe(35m);
            result.IsReconciled.ShouldBeTrue();

            var je = await journalEntryRepository.GetAsync(result.JournalEntryId);
            je.Status.ShouldBe(Core.DocumentStatus.Posted);
            je.TotalDebit.ShouldBe(35m);
            je.TotalCredit.ShouldBe(35m);
            je.ClearanceDate.ShouldBe(DateTime.Today);

            var reloadedTx = await transactionRepository.GetAsync(tx.Id);
            reloadedTx.IsReconciled.ShouldBeTrue();
            reloadedTx.JournalEntryId.ShouldBe(je.Id);
        });
    }
}
