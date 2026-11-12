using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Journal Entry rows on a foreign currency account. Per ERPNext journal_entry.py
/// (validate_multi_currency, set_exchange_rate, set_amounts_in_company_currency): the amount is
/// entered in the account's currency with a rate, the company amount is derived from the two, and
/// such rows need the entry's Multi Currency flag.
///
/// Before this the create DTO had only a company currency amount, so a row on a USD account stored
/// that amount as its account-currency amount too — the General Ledger then showed MYR figures
/// labelled USD.
/// </summary>
public abstract class JournalEntryMultiCurrencyTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // The test exchange provider answers every foreign pair with this rate.
    private const decimal ProviderRate = 4.72m;

    private sealed record Fixture(Company Company, Account Usd, Account Cash, Account Receivable, FiscalYear FiscalYear, CostCenter CostCenter);

    private async Task<Fixture> SeedAsync(string tag)
    {
        var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(
            new Company(Guid.NewGuid(), $"JE MC Co {tag}") { CurrencyCode = "MYR" }, autoSave: true);
        await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
            new DocumentSeries(Guid.NewGuid(), company.Id, $"JE Series {tag}", "JournalEntry", $"JEMC{tag}-"), autoSave: true);

        var accounts = GetRequiredService<IRepository<Account, Guid>>();
        var usd = await accounts.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1110-{tag}", "USD Bank", AccountType.Asset) { Currency = "USD" }, autoSave: true);
        var cash = await accounts.InsertAsync(new Account(Guid.NewGuid(), company.Id, $"1100-{tag}", "Cash", AccountType.Asset) { Currency = "MYR" }, autoSave: true);
        var receivable = await accounts.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"1200-{tag}", "Debtors", AccountType.Asset) { AccountSubType = AccountSubType.AccountsReceivable }, autoSave: true);
        var fiscalYear = await GetRequiredService<IRepository<FiscalYear, Guid>>().InsertAsync(
            new FiscalYear(Guid.NewGuid(), company.Id, $"FY {tag}", DateTime.UtcNow.Date.AddYears(-1), DateTime.UtcNow.Date.AddYears(1)), autoSave: true);
        var costCenter = await GetRequiredService<IRepository<CostCenter, Guid>>().InsertAsync(
            new CostCenter(Guid.NewGuid(), company.Id, $"JE MC CC {tag}"), autoSave: true);

        return new Fixture(company, usd, cash, receivable, fiscalYear, costCenter);
    }

    private static CreateJournalEntryDto Entry(Fixture f, bool multiCurrency, params CreateJournalEntryLineDto[] lines) => new()
    {
        CompanyId = f.Company.Id,
        FiscalYearId = f.FiscalYear.Id,
        PostingDate = DateTime.UtcNow.Date,
        IsMultiCurrency = multiCurrency,
        Lines = lines.ToList(),
    };

    [Fact]
    public async Task ForeignCurrencyRow_StoresAccountAmountRateAndDerivedCompanyAmount()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("A");
            var app = GetRequiredService<IJournalEntryAppService>();

            var created = await app.CreateAsync(Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto { AccountId = f.Usd.Id, AmountInAccountCurrency = 100m, ExchangeRate = 4.5m, IsDebit = true },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 450m, IsDebit = false }));

            var entry = await app.GetAsync(created.Id);
            entry.IsMultiCurrency.ShouldBeTrue();
            entry.TotalDebit.ShouldBe(450m);
            entry.TotalCredit.ShouldBe(450m);

            var usdLine = entry.Lines.Single(l => l.AccountId == f.Usd.Id);
            usdLine.AccountCurrency.ShouldBe("USD");
            usdLine.AmountInAccountCurrency.ShouldBe(100m);
            usdLine.ExchangeRate.ShouldBe(4.5m);
            usdLine.Amount.ShouldBe(450m);

            var cashLine = entry.Lines.Single(l => l.AccountId == f.Cash.Id);
            cashLine.AccountCurrency.ShouldBe("MYR");
            cashLine.AmountInAccountCurrency.ShouldBe(450m);
            cashLine.ExchangeRate.ShouldBe(1m);

            // The ledger of the USD account now reads in real USD.
            await app.PostAsync(created.Id);
            var report = await GetRequiredService<IGeneralLedgerAppService>().GetReportAsync(new GeneralLedgerFilterDto
            {
                CompanyId = f.Company.Id,
                AccountId = f.Usd.Id,
                FromDate = DateTime.UtcNow.Date.AddDays(-1),
                ToDate = DateTime.UtcNow.Date,
            });
            report.PresentationCurrency.ShouldBe("USD");
            report.TotalDebit.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task ForeignCurrencyRow_WithoutMultiCurrencyFlag_IsRejected()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("B");

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<IJournalEntryAppService>().CreateAsync(Entry(f, multiCurrency: false,
                    new CreateJournalEntryLineDto { AccountId = f.Usd.Id, AmountInAccountCurrency = 100m, ExchangeRate = 4.5m, IsDebit = true },
                    new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 450m, IsDebit = false })));

            exception.Code.ShouldBe(MyERPDomainErrorCodes.JournalMultiCurrencyNotChecked);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1.0)]
    public async Task ForeignCurrencyRow_WithoutRate_TakesTheRateForThePostingDate(double? rate)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync(rate.HasValue ? "C1" : "C0");
            var app = GetRequiredService<IJournalEntryAppService>();

            var created = await app.CreateAsync(Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto { AccountId = f.Usd.Id, AmountInAccountCurrency = 100m, ExchangeRate = (decimal?)rate, IsDebit = true },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 472m, IsDebit = false }));

            var usdLine = (await app.GetAsync(created.Id)).Lines.Single(l => l.AccountId == f.Usd.Id);
            usdLine.ExchangeRate.ShouldBe(ProviderRate);
            usdLine.Amount.ShouldBe(472m);
        });
    }

    [Fact]
    public async Task ForeignCurrencyRow_WithOnlyCompanyAmount_GetsItsAccountAmountFromTheRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("D");
            var app = GetRequiredService<IJournalEntryAppService>();

            // What a client that predates the account-currency fields sends.
            var created = await app.CreateAsync(Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto { AccountId = f.Usd.Id, Amount = 472m, IsDebit = true },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 472m, IsDebit = false }));

            var usdLine = (await app.GetAsync(created.Id)).Lines.Single(l => l.AccountId == f.Usd.Id);
            usdLine.Amount.ShouldBe(472m);
            usdLine.AmountInAccountCurrency.ShouldBe(100m);
            usdLine.ExchangeRate.ShouldBe(ProviderRate);
        });
    }

    [Fact]
    public async Task RowWithoutAnyAmount_IsRejected()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("E");

            var exception = await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<IJournalEntryAppService>().CreateAsync(Entry(f, multiCurrency: false,
                    new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 100m, IsDebit = true },
                    new CreateJournalEntryLineDto { AccountId = f.Receivable.Id, IsDebit = false })));

            exception.Code.ShouldBe(MyERPDomainErrorCodes.JournalLineAmountRequired);
            exception.Data["row"].ShouldBe(2);
        });
    }

    [Fact]
    public async Task Update_RecomputesCurrencyAmounts()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("F");
            var app = GetRequiredService<IJournalEntryAppService>();
            var created = await app.CreateAsync(Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto { AccountId = f.Usd.Id, AmountInAccountCurrency = 100m, ExchangeRate = 4.5m, IsDebit = true },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 450m, IsDebit = false }));

            await app.UpdateAsync(created.Id, Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto { AccountId = f.Usd.Id, AmountInAccountCurrency = 200m, ExchangeRate = 4.4m, IsDebit = true },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 880m, IsDebit = false }));

            var usdLine = (await app.GetAsync(created.Id)).Lines.Single(l => l.AccountId == f.Usd.Id);
            usdLine.AmountInAccountCurrency.ShouldBe(200m);
            usdLine.ExchangeRate.ShouldBe(4.4m);
            usdLine.Amount.ShouldBe(880m);
        });
    }

    [Fact]
    public async Task ReverseEntry_KeepsCurrencyAmountsPartyAndDimensions()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("G");
            var app = GetRequiredService<IJournalEntryAppService>();
            var created = await app.CreateAsync(Entry(f, multiCurrency: true,
                new CreateJournalEntryLineDto
                {
                    AccountId = f.Usd.Id, AmountInAccountCurrency = 100m, ExchangeRate = 4.5m, IsDebit = true, CostCenterId = f.CostCenter.Id,
                },
                new CreateJournalEntryLineDto { AccountId = f.Cash.Id, Amount = 450m, IsDebit = false }));
            await app.PostAsync(created.Id);

            var reversed = await app.CreateReverseJournalEntryAsync(created.Id);

            var entry = await app.GetAsync(reversed.Id);
            entry.IsMultiCurrency.ShouldBeTrue();
            var usdLine = entry.Lines.Single(l => l.AccountId == f.Usd.Id);
            usdLine.IsDebit.ShouldBeFalse();
            usdLine.Amount.ShouldBe(450m);
            usdLine.AmountInAccountCurrency.ShouldBe(100m);
            usdLine.ExchangeRate.ShouldBe(4.5m);
            usdLine.AccountCurrency.ShouldBe("USD");
            usdLine.CostCenterId.ShouldBe(f.CostCenter.Id);
        });
    }

    [Fact]
    public async Task GetAccountDetails_ReturnsCurrencyRateAndPartyType()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var f = await SeedAsync("H");
            var app = GetRequiredService<IJournalEntryAppService>();

            var usd = await app.GetAccountDetailsAsync(f.Company.Id, f.Usd.Id, DateTime.UtcNow.Date);
            usd.AccountCurrency.ShouldBe("USD");
            usd.CompanyCurrency.ShouldBe("MYR");
            usd.ExchangeRate.ShouldBe(ProviderRate);
            usd.PartyType.ShouldBeNull();

            var receivable = await app.GetAccountDetailsAsync(f.Company.Id, f.Receivable.Id, DateTime.UtcNow.Date);
            receivable.AccountCurrency.ShouldBe("MYR");
            receivable.ExchangeRate.ShouldBe(1m);
            receivable.PartyType.ShouldBe("Customer");
        });
    }
}
