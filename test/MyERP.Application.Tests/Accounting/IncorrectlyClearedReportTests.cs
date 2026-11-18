using System;
using System.Reflection;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Integration tests for Cheques and Deposits Incorrectly Cleared report
/// per ERPNext accounts/report/cheques_and_deposits_incorrectly_cleared.
/// </summary>
public abstract class IncorrectlyClearedReportTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task GetIncorrectlyClearedEntriesAsync_ReturnsPostDatedButEarlierClearedVouchers_AndMatchesStatementTotal()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var accountRepo = GetRequiredService<IRepository<Account, Guid>>();
            var peRepo = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var jeRepo = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var appService = GetRequiredService<IBankReconciliationAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Incorrectly Cleared Test Co"), autoSave: true);
            var fy = await fyRepo.InsertAsync(
                new FiscalYear(Guid.NewGuid(), company.Id, "FY-2026",
                    new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);

            var bankGl = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1110-IC", "Maybank Operating", AccountType.Asset)
                {
                    AccountSubType = AccountSubType.BankAccount,
                    Currency = "MYR"
                }, autoSave: true);
            var receivable = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "1200-IC", "Debtors", AccountType.Asset), autoSave: true);
            var expense = await accountRepo.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "5100-IC", "Bank Charges", AccountType.Expense), autoSave: true);

            var reportDate = new DateTime(2026, 11, 18);

            // 1. Payment Entry posted AFTER reportDate (2026-11-25) but cleared ON OR BEFORE reportDate (2026-11-15) -> deposit +500
            var peBadDeposit = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                new DateTime(2026, 11, 25), 500m, receivable.Id, bankGl.Id)
            {
                PaymentNumber = "PE-IC-001",
                ReferenceNumber = "CHQ-IC-1"
            };
            peBadDeposit.Submit();
            peBadDeposit.Post();
            SetClearanceDateDirect(peBadDeposit, new DateTime(2026, 11, 15));
            await peRepo.InsertAsync(peBadDeposit, autoSave: true);

            // 2. Payment Entry posted ON reportDate and cleared ON reportDate -> valid, must NOT appear
            var peNormal = new PaymentEntry(Guid.NewGuid(), company.Id, PaymentType.Receive,
                reportDate, 300m, receivable.Id, bankGl.Id)
            {
                PaymentNumber = "PE-OK-001"
            };
            peNormal.Submit();
            peNormal.Post();
            peNormal.SetClearanceDate(reportDate);
            await peRepo.InsertAsync(peNormal, autoSave: true);

            // 3. Non-opening Journal Entry posted AFTER reportDate (2026-11-22) and cleared BEFORE reportDate (2026-11-10) -> withdrawal -120
            var jeBadWithdrawal = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 11, 22))
            {
                EntryNumber = "JE-IC-001",
                ReferenceNumber = "ADJ-IC-1"
            };
            jeBadWithdrawal.AddLineWithDimensions(expense.Id, 120m, true, description: "Charges");
            jeBadWithdrawal.AddLineWithDimensions(bankGl.Id, 120m, false, description: "Bank credit");
            jeBadWithdrawal.Post();
            SetClearanceDateDirect(jeBadWithdrawal, new DateTime(2026, 11, 10));
            await jeRepo.InsertAsync(jeBadWithdrawal, autoSave: true);

            // 4. Opening Journal Entry with same dates -> excluded
            var jeOpening = new JournalEntry(Guid.NewGuid(), company.Id, fy.Id, new DateTime(2026, 11, 23))
            {
                EntryNumber = "JE-OPEN-001",
                IsOpening = true
            };
            jeOpening.AddLineWithDimensions(bankGl.Id, 999m, true, description: "Opening");
            jeOpening.AddLineWithDimensions(receivable.Id, 999m, false, description: "Offset");
            jeOpening.Post();
            SetClearanceDateDirect(jeOpening, new DateTime(2026, 11, 5));
            await jeRepo.InsertAsync(jeOpening, autoSave: true);

            var input = new GetBankReconciliationStatementInput
            {
                CompanyId = company.Id,
                BankAccountId = bankGl.Id,
                ReportDate = reportDate
            };

            var entries = await appService.GetIncorrectlyClearedEntriesAsync(input);

            entries.Count.ShouldBe(2);
            // Sorted ascending by PostingDate: JE (Nov 22) then PE (Nov 25)
            entries[0].DocumentType.ShouldBe("Journal Entry");
            entries[0].DocumentNumber.ShouldBe("JE-IC-001");
            entries[0].Debit.ShouldBe(0m);
            entries[0].Credit.ShouldBe(120m);
            entries[0].ClearanceDate.ShouldBe(new DateTime(2026, 11, 10));

            entries[1].DocumentType.ShouldBe("Payment Entry");
            entries[1].DocumentNumber.ShouldBe("PE-IC-001");
            entries[1].Debit.ShouldBe(500m);
            entries[1].Credit.ShouldBe(0m);
            entries[1].ClearanceDate.ShouldBe(new DateTime(2026, 11, 15));

            var statement = await appService.GetReconciliationStatementAsync(input);
            statement.AmountsNotReflectedInSystem.ShouldBe(380m);
            statement.IncorrectlyClearedEntries.Count.ShouldBe(2);
        });
    }

    private static void SetClearanceDateDirect<TEntity>(TEntity entity, DateTime clearanceDate)
    {
        var prop = typeof(TEntity).GetProperty("ClearanceDate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop.ShouldNotBeNull();
        prop!.SetValue(entity, clearanceDate);
    }
}
