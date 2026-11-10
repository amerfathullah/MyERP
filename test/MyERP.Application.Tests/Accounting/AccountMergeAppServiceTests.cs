using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class AccountMergeAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAccountAppService _accountAppService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepository;

    protected AccountMergeAppServiceTests()
    {
        _accountAppService = GetRequiredService<IAccountAppService>();
        _companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        _fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
        _accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        _journalRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
        _journalLineRepository = GetRequiredService<IRepository<JournalEntryLine, Guid>>();
    }

    [Fact]
    public async Task MergeAccountAsync_SelfMerge_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Merge 1"), autoSave: true);
            var account = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100", "Rent Expense", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = account.Id,
                    NewAccountId = account.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_CompanyMismatch_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company1 = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co A Merge"), autoSave: true);
            var company2 = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co B Merge"), autoSave: true);

            var acc1 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company1.Id, "5101", "Rent Expense A", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var acc2 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company2.Id, "5102", "Rent Expense B", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = acc1.Id,
                    NewAccountId = acc2.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_IsGroupMismatch_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Group Mismatch"), autoSave: true);

            var groupAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5000", "Expenses Group", AccountType.Expense)
                {
                    IsGroup = true
                }, autoSave: true);

            var ledgerAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100", "Office Expense", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = groupAcc.Id,
                    NewAccountId = ledgerAcc.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_AccountTypeMismatch_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Type Mismatch"), autoSave: true);

            var assetAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1500", "Other Asset", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.CurrentAsset,
                    IsGroup = false
                }, autoSave: true);

            var expenseAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100", "Office Expense", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = assetAcc.Id,
                    NewAccountId = expenseAcc.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_CurrencyMismatch_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Curr Mismatch"), autoSave: true);

            var accUsd = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1201", "USD Bank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    Currency = "USD",
                    IsGroup = false
                }, autoSave: true);

            var accMyr = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1202", "MYR Bank", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    Currency = "MYR",
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = accUsd.Id,
                    NewAccountId = accMyr.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_AccountSubTypeMismatch_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co SubType Mismatch"), autoSave: true);

            var bankAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200", "Bank Ledger", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false
                }, autoSave: true);

            var stockAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1300", "Stock Ledger", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.Stock,
                    IsGroup = false
                }, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = bankAcc.Id,
                    NewAccountId = stockAcc.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_BalanceMustBeViolation_ThrowsInvalidAccountMerge()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Balance Dir"), autoSave: true);

            var fy = await _fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2025, 1, 1), new DateTime(2027, 12, 31)), autoSave: true);

            var oldAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2101", "Old Accounts Payable", AccountType.Liability)
                {
                    AccountSubType = AccountSubType.AccountsPayable,
                    IsGroup = false
                }, autoSave: true);

            var newAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2102", "New Accounts Payable", AccountType.Liability)
                {
                    AccountSubType = AccountSubType.AccountsPayable,
                    BalanceMustBe = BalanceDirection.Credit,
                    IsGroup = false
                }, autoSave: true);

            var bankAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1201", "Bank Offset", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    IsGroup = false
                }, autoSave: true);

            // Post a Debit entry (positive balance) to oldAcc so merged balance is positive (Debit), violating Credit requirement
            var je = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, DateTime.UtcNow);
            je.AddLine(oldAcc.Id, 500m, isDebit: true);
            je.AddLine(bankAcc.Id, 500m, isDebit: false);
            je.Post();

            await _journalRepository.InsertAsync(je, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(async () =>
            {
                await _accountAppService.MergeAccountAsync(new MergeAccountDto
                {
                    OldAccountId = oldAcc.Id,
                    NewAccountId = newAcc.Id
                });
            });

            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidAccountMerge);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_GroupAccounts_ReparentsChildrenAndMergesSuccessfully()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Group Merge"), autoSave: true);

            var rootGroup = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5000", "All Expenses", AccountType.Expense)
                {
                    IsGroup = true
                }, autoSave: true);

            var oldGroup = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100", "Indirect Old Expenses", AccountType.Expense)
                {
                    IsGroup = true,
                    ParentAccountId = rootGroup.Id
                }, autoSave: true);

            var newGroup = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5200", "Indirect New Expenses", AccountType.Expense)
                {
                    IsGroup = true,
                    ParentAccountId = rootGroup.Id
                }, autoSave: true);

            var child1 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5110", "Office Expense Child", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false,
                    ParentAccountId = oldGroup.Id
                }, autoSave: true);

            var child2 = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5120", "Travel Expense Child", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false,
                    ParentAccountId = oldGroup.Id
                }, autoSave: true);

            var result = await _accountAppService.MergeAccountAsync(new MergeAccountDto
            {
                OldAccountId = oldGroup.Id,
                NewAccountId = newGroup.Id
            });

            result.Id.ShouldBe(newGroup.Id);

            // Old group should be deleted
            var deletedOld = await _accountRepository.FindAsync(oldGroup.Id);
            deletedOld.ShouldBeNull();

            // Children should be reparented to newGroup
            var updatedChild1 = await _accountRepository.GetAsync(child1.Id);
            updatedChild1.ParentAccountId.ShouldBe(newGroup.Id);

            var updatedChild2 = await _accountRepository.GetAsync(child2.Id);
            updatedChild2.ParentAccountId.ShouldBe(newGroup.Id);
        });
    }

    [Fact]
    public async Task MergeAccountAsync_GroupMergedIntoOwnChild_ReparentsNewGroupToGrandparent()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Cycle Prevention"), autoSave: true);

            var rootGroup = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "6000", "Top Level Expenses", AccountType.Expense)
                {
                    IsGroup = true
                }, autoSave: true);

            var oldGroup = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "6100", "Parent Group", AccountType.Expense)
                {
                    IsGroup = true,
                    ParentAccountId = rootGroup.Id
                }, autoSave: true);

            var newGroupChild = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "6110", "Child Group To Merge Into", AccountType.Expense)
                {
                    IsGroup = true,
                    ParentAccountId = oldGroup.Id
                }, autoSave: true);

            var result = await _accountAppService.MergeAccountAsync(new MergeAccountDto
            {
                OldAccountId = oldGroup.Id,
                NewAccountId = newGroupChild.Id
            });

            result.Id.ShouldBe(newGroupChild.Id);

            // New group should now have ParentAccountId = rootGroup.Id (reparented)
            var updatedNewGroup = await _accountRepository.GetAsync(newGroupChild.Id);
            updatedNewGroup.ParentAccountId.ShouldBe(rootGroup.Id);

            var deletedOld = await _accountRepository.FindAsync(oldGroup.Id);
            deletedOld.ShouldBeNull();
        });
    }

    [Fact]
    public async Task MergeAccountAsync_LedgerAccount_ReassignsJournalLinesAndCompanyDefaults()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await _companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Co Line Merge"), autoSave: true);

            var oldAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "7100", "Old Expense Ledger", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            var newAcc = await _accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "7200", "New Expense Ledger", AccountType.Expense)
                {
                    AccountSubType = AccountSubType.OperatingExpense,
                    IsGroup = false
                }, autoSave: true);

            // Set Company default expense account to oldAcc
            company.DefaultExpenseAccountId = oldAcc.Id;
            await _companyRepository.UpdateAsync(company, autoSave: true);

            // Insert JournalEntryLine for oldAcc
            var je = await _journalRepository.InsertAsync(
                new JournalEntry(Guid.NewGuid(), company.Id, Guid.NewGuid(), DateTime.UtcNow), autoSave: true);

            var line = await _journalLineRepository.InsertAsync(
                new JournalEntryLine(Guid.NewGuid(), je.Id, oldAcc.Id, 250m, true), autoSave: true);

            var result = await _accountAppService.MergeAccountAsync(new MergeAccountDto
            {
                OldAccountId = oldAcc.Id,
                NewAccountId = newAcc.Id
            });

            result.Id.ShouldBe(newAcc.Id);

            // Verify line was reassigned
            var updatedLine = await _journalLineRepository.GetAsync(line.Id);
            updatedLine.AccountId.ShouldBe(newAcc.Id);

            // Verify company default was updated
            var updatedCompany = await _companyRepository.GetAsync(company.Id);
            updatedCompany.DefaultExpenseAccountId.ShouldBe(newAcc.Id);

            // Verify old account deleted
            var deletedOld = await _accountRepository.FindAsync(oldAcc.Id);
            deletedOld.ShouldBeNull();
        });
    }
}
