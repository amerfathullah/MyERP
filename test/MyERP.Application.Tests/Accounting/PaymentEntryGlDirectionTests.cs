using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Regression coverage for DocumentPostingOrchestrator.PostPaymentEntryAsync's main JE: must
/// always debit PaidToAccountId / credit PaidFromAccountId, for both Receive and Pay. Guards
/// against a real bug found and fixed in the 75th migration session — the previous
/// AccountingRuleEngine-routed implementation posted every Payment Entry (Receive or Pay) as
/// DR a single hardcoded bank account / CR the company's default Receivable, since the seeded
/// "PaymentEntry" AccountingRule rows have no PaymentType discriminator.
/// </summary>
public abstract class PaymentEntryGlDirectionTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task Pay_Type_PaymentEntry_Debits_Payable_Credits_Bank()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var postingOrchestrator = GetRequiredService<DocumentPostingOrchestrator>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PE GL Direction Co (Pay)"), autoSave: true);

            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1121", "Test Bank", AccountType.Asset), autoSave: true);
            var payableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110", "Test Payable", AccountType.Liability), autoSave: true);
            var receivableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130", "Test Receivable", AccountType.Asset), autoSave: true);

            company.DefaultReceivableAccountId = receivableAccount.Id;
            company.DefaultPayableAccountId = payableAccount.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
                autoSave: true);

            // A "Pay" direction Payment Entry: cash leaves Bank, settles a Payable. PaidFrom=Bank,
            // PaidTo=Payable per the Angular form's own resolveAccounts() convention.
            var pe = new PaymentEntry(
                Guid.NewGuid(), company.Id, PaymentType.Pay, new DateTime(2026, 6, 1),
                paidAmount: 500m, paidFromAccountId: bankAccount.Id, paidToAccountId: payableAccount.Id);

            var journal = await postingOrchestrator.PostPaymentEntryAsync(
                pe,
                partyAccountId: payableAccount.Id,
                partyType: "Supplier",
                partyId: Guid.NewGuid(),
                accountCurrency: "MYR",
                exchangeRate: 1m,
                allocations: Array.Empty<PaymentAllocation>(),
                paidFromAccountId: pe.PaidFromAccountId,
                paidToAccountId: pe.PaidToAccountId);

            var lineSummary = string.Join(" | ", journal.Lines.Select(l =>
                $"{(l.IsDebit ? "DR" : "CR")} {(l.AccountId == bankAccount.Id ? "Bank" : l.AccountId == payableAccount.Id ? "Payable" : l.AccountId == receivableAccount.Id ? "Receivable" : l.AccountId.ToString())} {l.Amount}"));

            journal.Lines.Any(l => l.AccountId == receivableAccount.Id)
                .ShouldBeFalse($"Pay-type PE must never touch Receivable. Actual lines: {lineSummary}");
            journal.Lines.ShouldContain(l => l.AccountId == payableAccount.Id && l.IsDebit && l.Amount == 500m);
            journal.Lines.ShouldContain(l => l.AccountId == bankAccount.Id && !l.IsDebit && l.Amount == 500m);
        });
    }

    [Fact]
    public async Task Receive_Type_PaymentEntry_Debits_Bank_Credits_Receivable()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fiscalYearRepository = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var postingOrchestrator = GetRequiredService<DocumentPostingOrchestrator>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PE GL Direction Co (Receive)"), autoSave: true);

            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1121", "Test Bank", AccountType.Asset), autoSave: true);
            var payableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "2110", "Test Payable", AccountType.Liability), autoSave: true);
            var receivableAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1130", "Test Receivable", AccountType.Asset), autoSave: true);

            company.DefaultReceivableAccountId = receivableAccount.Id;
            company.DefaultPayableAccountId = payableAccount.Id;
            await companyRepository.UpdateAsync(company, autoSave: true);

            await fiscalYearRepository.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY2027", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)),
                autoSave: true);

            // A "Receive" direction Payment Entry: customer pays into Bank, clears Receivable.
            // PaidFrom=Receivable, PaidTo=Bank.
            var pe = new PaymentEntry(
                Guid.NewGuid(), company.Id, PaymentType.Receive, new DateTime(2026, 6, 1),
                paidAmount: 300m, paidFromAccountId: receivableAccount.Id, paidToAccountId: bankAccount.Id);

            var journal = await postingOrchestrator.PostPaymentEntryAsync(
                pe,
                partyAccountId: receivableAccount.Id,
                partyType: "Customer",
                partyId: Guid.NewGuid(),
                accountCurrency: "MYR",
                exchangeRate: 1m,
                allocations: Array.Empty<PaymentAllocation>(),
                paidFromAccountId: pe.PaidFromAccountId,
                paidToAccountId: pe.PaidToAccountId);

            var lineSummary = string.Join(" | ", journal.Lines.Select(l =>
                $"{(l.IsDebit ? "DR" : "CR")} {(l.AccountId == bankAccount.Id ? "Bank" : l.AccountId == payableAccount.Id ? "Payable" : l.AccountId == receivableAccount.Id ? "Receivable" : l.AccountId.ToString())} {l.Amount}"));

            journal.Lines.Any(l => l.AccountId == payableAccount.Id)
                .ShouldBeFalse($"Receive-type PE must never touch Payable. Actual lines: {lineSummary}");
            journal.Lines.ShouldContain(l => l.AccountId == bankAccount.Id && l.IsDebit && l.Amount == 300m);
            journal.Lines.ShouldContain(l => l.AccountId == receivableAccount.Id && !l.IsDebit && l.Amount == 300m);
        });
    }
}
