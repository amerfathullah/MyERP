using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage for round-95's fix: ReimburseAsync inserted a Draft PaymentEntry and never
/// called Submit()/Post() — the GL never posted (customer/employee liability never cleared, no
/// expense recognized) and no PaymentEntryReference row existed, so ExpenseClaimPaymentStatusJob's
/// nightly resync (which recomputes TotalAmountReimbursed from ReferenceType == "ExpenseClaim" rows)
/// would silently reset the claim's reimbursed amount back to 0 on its next run.
/// </summary>
public abstract class ExpenseClaimReimbursementGlPostingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ReimburseAsync_PostsBalancedJournalEntry_AndCreatesExpenseClaimReference()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var claimRepository = GetRequiredService<IRepository<ExpenseClaim, Guid>>();
            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var peRepository = GetRequiredService<IRepository<PaymentEntry, Guid>>();
            var referenceRepository = GetRequiredService<IRepository<PaymentEntryReference, Guid>>();
            var journalEntryRepository = GetRequiredService<IRepository<JournalEntry, Guid>>();
            var expenseClaimAppService = GetRequiredService<IExpenseClaimAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Expense GL Test Co"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "PE Series GL", "PaymentEntry", "PEGL-"), autoSave: true);

            var travelExpenseAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9GL01", "Travel Expense", AccountType.Expense), autoSave: true);
            var mealsExpenseAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9GL02", "Meals Expense", AccountType.Expense), autoSave: true);
            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9GL03", "Test Bank", AccountType.Asset), autoSave: true);

            var claim = new ExpenseClaim(Guid.NewGuid(), company.Id, Guid.NewGuid(), DateTime.Today);
            claim.AddExpense(DateTime.Today, "Flight to KL", 700m, travelExpenseAccount.Id);
            claim.AddExpense(DateTime.Today, "Client dinner", 300m, mealsExpenseAccount.Id);
            claim.Approve();
            claim.Submit();
            await claimRepository.InsertAsync(claim, autoSave: true);

            var paymentEntryId = await expenseClaimAppService.ReimburseAsync(claim.Id, bankAccount.Id);

            var pe = await peRepository.GetAsync(paymentEntryId);
            pe.Status.ShouldBe(DocumentStatus.Posted);

            var references = await referenceRepository.GetListAsync(r => r.PaymentEntryId == paymentEntryId);
            references.ShouldContain(r => r.ReferenceType == "ExpenseClaim" && r.ReferenceId == claim.Id && r.AllocatedAmount == 1000m);

            var journalEntries = await journalEntryRepository.GetListAsync(je => je.ReferenceType == "PaymentEntry" && je.ReferenceId == paymentEntryId);
            journalEntries.ShouldHaveSingleItem();
            var je = journalEntries.Single();
            je.TotalDebit.ShouldBe(je.TotalCredit);
            je.TotalDebit.ShouldBe(1000m);
            je.Lines.ShouldContain(l => l.AccountId == travelExpenseAccount.Id && l.IsDebit && l.Amount == 700m);
            je.Lines.ShouldContain(l => l.AccountId == mealsExpenseAccount.Id && l.IsDebit && l.Amount == 300m);
            je.Lines.ShouldContain(l => l.AccountId == bankAccount.Id && !l.IsDebit && l.Amount == 1000m);

            var updatedClaim = await claimRepository.GetAsync(claim.Id);
            updatedClaim.TotalAmountReimbursed.ShouldBe(1000m);
        });
    }
}
