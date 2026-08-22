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
}
