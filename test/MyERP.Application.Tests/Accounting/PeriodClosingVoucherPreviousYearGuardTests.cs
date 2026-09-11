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
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's Period
/// Closing Voucher blocks closing a fiscal year while the immediately prior year still has
/// un-closed GL activity (validate_previous_year_closed). MyERP's PeriodClosingVoucherAppService
/// had no such check — a company could close year 2 before year 1, leaving year 1's P&L never
/// rolled into retained earnings.
/// </summary>
public abstract class PeriodClosingVoucherPreviousYearGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_PreviousYearHasUnclosedGlActivity_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var pcvAppService = GetRequiredService<IPeriodClosingVoucherAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PCV PrevYear Guard Co"), autoSave: true);

            var fy1 = await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2025", new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)), autoSave: true);
            var fy2 = await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var debitAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1100-PCVPY", "Test Asset", AccountType.Asset), autoSave: true);
            var creditAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "4000-PCVPY", "Test Revenue", AccountType.Revenue), autoSave: true);
            var closingAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "3000-PCVPY", "Test Retained Earnings", AccountType.Equity), autoSave: true);

            // Posted GL activity in FY1, with no PeriodClosingVoucher ever closing FY1.
            var je = new JournalEntry(Guid.NewGuid(), company.Id, fy1.Id, new DateTime(2025, 6, 1));
            je.AddLine(debitAccount.Id, 100m, isDebit: true, description: "PY activity");
            je.AddLine(creditAccount.Id, 100m, isDebit: false, description: "PY activity");
            je.Post();
            await journalEntryRepository.InsertAsync(je, autoSave: true);

            var pcv = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy2.Id,
                PostingDate = new DateTime(2026, 12, 31),
                TransactionDate = new DateTime(2026, 12, 31),
                ClosingAccountId = closingAccount.Id,
            });

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => pcvAppService.SubmitAsync(pcv.Id));
        });
    }
}
