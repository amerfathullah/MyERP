using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

public abstract class JournalEntryReversalEndpointTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company Company, FiscalYear Fy, Account Cash, Account Expense)> SeedAsync(string tag)
    {
        var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(
            new Company(Guid.NewGuid(), $"JE Rev Co {tag}") { CurrencyCode = "MYR" }, autoSave: true);
        await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"JE Series {tag}", "JournalEntry", $"JER{tag}-"), autoSave: true);

        var accounts = GetRequiredService<IRepository<Account, Guid>>();
        var cash = await accounts.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1100-{tag}", "Cash", AccountType.Asset), autoSave: true);
        var expense = await accounts.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"5100-{tag}", "Office Expense", AccountType.Expense), autoSave: true);

        var fy = await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);

        return (company, fy, cash, expense);
    }

    [Fact]
    public async Task CreateReversalAsync_UsesJournalEntrySeriesAndPopulatesReversalMetadata()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, fy, cash, expense) = await SeedAsync("A");
            var app = GetRequiredService<IJournalEntryAppService>();

            var created = await app.CreateAsync(new CreateJournalEntryDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy.Id,
                PostingDate = DateTime.UtcNow.Date,
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new() { AccountId = expense.Id, Amount = 250m, IsDebit = true },
                    new() { AccountId = cash.Id, Amount = 250m, IsDebit = false },
                }
            });
            await app.PostAsync(created.Id);

            var reversal = await app.CreateReversalAsync(created.Id);
            reversal.Status.ShouldBe("Posted");
            reversal.ReversalOfId.ShouldBe(created.Id);
            reversal.EntryNumber.ShouldStartWith("JERA-");

            var revDto = await app.GetAsync(reversal.Id);
            revDto.ReversalOfId.ShouldBe(created.Id);
            revDto.ReversalOfEntryNumber.ShouldBe(created.EntryNumber);

            var sourceDto = await app.GetAsync(created.Id);
            sourceDto.HasActiveReversal.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task CreateReversalAsync_WhenAccountsFrozen_ThrowsAccountingPeriodClosed()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, fy, cash, expense) = await SeedAsync("B");
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var app = GetRequiredService<IJournalEntryAppService>();

            var created = await app.CreateAsync(new CreateJournalEntryDto
            {
                CompanyId = company.Id,
                FiscalYearId = fy.Id,
                PostingDate = DateTime.UtcNow.Date.AddDays(-2),
                Lines = new List<CreateJournalEntryLineDto>
                {
                    new() { AccountId = expense.Id, Amount = 100m, IsDebit = true },
                    new() { AccountId = cash.Id, Amount = 100m, IsDebit = false },
                }
            });
            await app.PostAsync(created.Id);

            company.AccountsFrozenTillDate = DateTime.UtcNow.Date.AddDays(1);
            await companyRepo.UpdateAsync(company, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => app.CreateReversalAsync(created.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.AccountingPeriodClosed);
        });
    }
}
