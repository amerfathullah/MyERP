using System;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.HumanResources;
using MyERP.HumanResources.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.HumanResources;

/// <summary>
/// Regression coverage for a gap found while surveying HumanResources DomainServices for unwired
/// methods: ExpenseClaimManager.ValidateAdvanceLinkage had zero callers anywhere, even though
/// ExpenseClaimAppService.ReimburseAsync's own doc comment claimed it ran ("Per DO-NOT: Allow
/// expense claim GL posting without verifying advance linkage (double-payment risk)"). Wired it in.
/// </summary>
public abstract class ExpenseClaimAdvanceLinkageTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ReimburseAsync_NoAdvanceLinked_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, expenseClaimAppService, claim) = await SeedAsync("NoAdv", advanceAmount: 0, linkedAdvancePaidAmount: null);

            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9EC00", "Test Bank", AccountType.Asset), autoSave: true);

            var paymentEntryId = await expenseClaimAppService.ReimburseAsync(claim.Id, bankAccount.Id);
            paymentEntryId.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task ReimburseAsync_AdvanceAmountExceedsLinkedPayment_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, expenseClaimAppService, claim) = await SeedAsync(
                "Exceeds", advanceAmount: 500m, linkedAdvancePaidAmount: 300m);

            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9EC01", "Test Bank", AccountType.Asset), autoSave: true);

            await Should.ThrowAsync<BusinessException>(
                () => expenseClaimAppService.ReimburseAsync(claim.Id, bankAccount.Id));
        });
    }

    [Fact]
    public async Task ReimburseAsync_AdvanceAmountWithinLinkedPayment_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (company, expenseClaimAppService, claim) = await SeedAsync(
                "Within", advanceAmount: 200m, linkedAdvancePaidAmount: 300m);

            var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
            var bankAccount = await accountRepository.InsertAsync(
                new Account(Guid.NewGuid(), company.Id, "9EC02", "Test Bank", AccountType.Asset), autoSave: true);

            var paymentEntryId = await expenseClaimAppService.ReimburseAsync(claim.Id, bankAccount.Id);
            paymentEntryId.ShouldNotBe(Guid.Empty);
        });
    }

    private async Task<(Company Company, IExpenseClaimAppService AppService, ExpenseClaim Claim)> SeedAsync(
        string suffix, decimal advanceAmount, decimal? linkedAdvancePaidAmount)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var claimRepository = GetRequiredService<IRepository<ExpenseClaim, Guid>>();
        var peRepository = GetRequiredService<IRepository<PaymentEntry, Guid>>();
        var accountRepository = GetRequiredService<IRepository<Account, Guid>>();
        var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
        var expenseClaimAppService = GetRequiredService<IExpenseClaimAppService>();

        var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), $"Expense Claim Test Co {suffix}"), autoSave: true);
        await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, $"PE Series {suffix}", "PaymentEntry", $"PE{suffix}-"), autoSave: true);

        var payableAccount = await accountRepository.InsertAsync(
            new Account(Guid.NewGuid(), company.Id, $"9{suffix}03", "Test Payable", AccountType.Liability), autoSave: true);

        var claim = new ExpenseClaim(Guid.NewGuid(), company.Id, Guid.NewGuid(), DateTime.Today)
        {
            PayableAccountId = payableAccount.Id,
            AdvanceAmount = advanceAmount,
        };
        claim.AddExpense(DateTime.Today, "Travel", 1000m);
        claim.Approve();
        claim.Submit();

        if (linkedAdvancePaidAmount.HasValue)
        {
            var advancePe = new PaymentEntry(
                Guid.NewGuid(), company.Id, PaymentType.Pay, DateTime.Today.AddDays(-5),
                linkedAdvancePaidAmount.Value, payableAccount.Id, payableAccount.Id);
            await peRepository.InsertAsync(advancePe, autoSave: true);
            claim.AdvancePaymentEntryId = advancePe.Id;
        }

        await claimRepository.InsertAsync(claim, autoSave: true);

        return (company, expenseClaimAppService, claim);
    }
}
