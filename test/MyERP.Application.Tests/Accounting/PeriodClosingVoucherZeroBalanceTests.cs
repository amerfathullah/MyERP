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
/// Regression coverage for a real gap found while testing the previous-year-closed guard
/// ([[project_myerp_migration_2026_09_10t]]): PeriodClosingVoucher.Submit() unconditionally
/// rejected a voucher with zero closing entries, which made
/// PeriodClosingVoucherAppService.SubmitAsync's own "no P&L balances" branch (for a period with no
/// revenue/expense activity - e.g. a new company's first year) unreachable without throwing. Per
/// ERPNext, a period with zero P&L activity is still a valid one to close.
/// </summary>
public abstract class PeriodClosingVoucherZeroBalanceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_NoProfitAndLossActivity_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var pcvAppService = GetRequiredService<IPeriodClosingVoucherAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PCV Zero Balance Co"), autoSave: true);

            var fy1 = await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026-Zero", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var closingAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "3000-PCVZ", "Test Retained Earnings", AccountType.Equity), autoSave: true);

            var pcv = await pcvAppService.CreateAsync(new CreatePeriodClosingVoucherDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy1.Id,
                PostingDate = new DateTime(2026, 12, 31),
                TransactionDate = new DateTime(2026, 12, 31),
                ClosingAccountId = closingAccount.Id,
            });

            // No P&L activity anywhere for this company — the "no balances" branch must
            // successfully mark the voucher Submitted rather than throwing.
            var result = await pcvAppService.SubmitAsync(pcv.Id);
            result.Id.ShouldBe(pcv.Id);
        });
    }
}
