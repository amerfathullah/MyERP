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

    /// <summary>
    /// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext throws
    /// "already fully reconciled" / "over-allocated" before letting reconcile_vouchers() re-link a
    /// bank transaction or double-allocate a voucher (bank_transaction.py add_payment_entries /
    /// allocate_payment_entries). ReconcileAsync had neither guard — the same Payment Entry could
    /// be manually reconciled against two different bank transactions, and an already-reconciled
    /// transaction could be silently re-pointed at a different voucher.
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_SameVoucherTwice_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var paymentEntryRepository = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var bankReconciliationAppService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Double Alloc Co"), autoSave: true);
            var bankGl = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1120", "Double Alloc Bank GL", AccountType.Asset), autoSave: true);
            var receivable = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200", "Receivable", AccountType.Asset), autoSave: true);

            var pe = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                DateTime.Today, 500m, receivable.Id, bankGl.Id);
            pe.Submit();
            pe.Post();
            await paymentEntryRepository.InsertAsync(pe, autoSave: true);

            var tx1 = await transactionRepository.InsertAsync(new BankTransaction(
                Guid.NewGuid(), company.Id, bankGl.Id, DateTime.Today, "Customer receipt", 500m), autoSave: true);
            var tx2 = await transactionRepository.InsertAsync(new BankTransaction(
                Guid.NewGuid(), company.Id, bankGl.Id, DateTime.Today, "Duplicate statement line", 500m), autoSave: true);

            await bankReconciliationAppService.ReconcileAsync(new ReconcileBankTransactionDto
            {
                TransactionId = tx1.Id,
                PaymentEntryId = pe.Id,
            });

            // Same PE, a different bank transaction — must be rejected, not silently re-linked.
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                bankReconciliationAppService.ReconcileAsync(new ReconcileBankTransactionDto
                {
                    TransactionId = tx2.Id,
                    PaymentEntryId = pe.Id,
                }));

            // Reconciling tx1 again (even against itself) must also be rejected — it's already reconciled.
            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                bankReconciliationAppService.ReconcileAsync(new ReconcileBankTransactionDto
                {
                    TransactionId = tx1.Id,
                    PaymentEntryId = pe.Id,
                }));
        });
    }

    /// <summary>
    /// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
    /// bank_transaction.on_cancel()/remove_from_bank_transaction() delinks a Bank Transaction when
    /// its matched voucher is cancelled. MyERP's PaymentEntry.Cancel()/JournalEntry.Cancel() raised
    /// PaymentEntryCancelledEvent/JournalEntryCancelledEvent, but nothing subscribed — cancelling a
    /// reconciled voucher left the Bank Transaction showing IsReconciled=true against a cancelled,
    /// GL-reversed voucher forever. Added BankTransactionUnreconcileEventHandler.
    /// </summary>
    [Fact]
    public async Task CancellingPaymentEntry_UnreconcilesLinkedBankTransaction()
    {
        Guid txId = Guid.Empty, peId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var paymentEntryRepository = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var bankReconciliationAppService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Cancel Unwind Co"), autoSave: true);
            var bankGl = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130", "Cancel Unwind Bank GL", AccountType.Asset), autoSave: true);
            var receivable = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1210", "Receivable 2", AccountType.Asset), autoSave: true);

            var pe = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                DateTime.Today, 750m, receivable.Id, bankGl.Id);
            pe.Submit();
            pe.Post();
            await paymentEntryRepository.InsertAsync(pe, autoSave: true);
            peId = pe.Id;

            var tx = await transactionRepository.InsertAsync(new BankTransaction(
                Guid.NewGuid(), company.Id, bankGl.Id, DateTime.Today, "Customer receipt to be unwound", 750m), autoSave: true);
            txId = tx.Id;

            await bankReconciliationAppService.ReconcileAsync(new ReconcileBankTransactionDto
            {
                TransactionId = tx.Id,
                PaymentEntryId = pe.Id,
            });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var reconciled = await transactionRepository.GetAsync(txId);
            reconciled.IsReconciled.ShouldBeTrue();
            reconciled.PaymentEntryId.ShouldBe(peId);

            var paymentEntryRepository = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var pe = await paymentEntryRepository.GetAsync(peId);
            pe.Cancel();
            await paymentEntryRepository.UpdateAsync(pe, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var transactionRepository = GetRequiredService<IRepository<BankTransaction, Guid>>();
            var tx = await transactionRepository.GetAsync(txId);
            tx.IsReconciled.ShouldBeFalse();
            tx.PaymentEntryId.ShouldBeNull();
        });
    }
}
