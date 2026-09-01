using System;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Accounting;

public class PeriodClosingVoucherTests
{
    private static PeriodClosingVoucher CreatePCV() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, new DateTime(2026, 6, 30), Guid.NewGuid());

    [Fact]
    public void Create_SetsDefaults()
    {
        var pcv = CreatePCV();
        pcv.Status.ShouldBe(Core.DocumentStatus.Draft);
        pcv.TotalClosingAmount.ShouldBe(0);
        pcv.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void AddEntry_UpdatesTotal()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 5000m, true);
        pcv.AddEntry(Guid.NewGuid(), Guid.NewGuid(), 3000m, false);
        pcv.Entries.Count.ShouldBe(2);
        pcv.TotalClosingAmount.ShouldBe(8000m);
    }

    [Fact]
    public void Submit_WithEntries_Succeeds()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 10000m, true);
        pcv.Submit();
        pcv.Status.ShouldBe(Core.DocumentStatus.Submitted);
    }

    [Fact]
    public void Submit_Empty_Throws()
    {
        var pcv = CreatePCV();
        Should.Throw<BusinessException>(() => pcv.Submit());
    }

    [Fact]
    public void Cancel_Submitted_Succeeds()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();
        pcv.Cancel();
        pcv.Status.ShouldBe(Core.DocumentStatus.Cancelled);
    }

    [Fact]
    public void AddEntry_AfterSubmit_Throws()
    {
        var pcv = CreatePCV();
        pcv.AddEntry(Guid.NewGuid(), null, 5000m, true);
        pcv.Submit();
        Should.Throw<BusinessException>(() => pcv.AddEntry(Guid.NewGuid(), null, 1000m, false));
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_ThrowsWhenPostingDateIsOnOrBeforeAccountsFrozenTillDate()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var frozenDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            AccountsFrozenTillDate = frozenDate,
            CurrencyCode = "MYR"
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity)
        {
            Currency = "MYR"
        };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);

        var service = new MyERP.Accounting.DomainServices.PeriodClosingPostingService(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>(),
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>(),
            accountRepo,
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<FiscalYear, Guid>>(),
            companyRepo,
            new MyERP.Accounting.DomainServices.AccountClosingBalanceService(
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<AccountClosingBalance, Guid>>(),
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>(),
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>()
            )
        );

        var ex = await Should.ThrowAsync<BusinessException>(async () => await service.ValidateForSubmitAsync(pcv));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.AccountingPeriodClosed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_SucceedsWhenPostingDateIsAfterAccountsFrozenTillDate()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var frozenDate = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            AccountsFrozenTillDate = frozenDate,
            CurrencyCode = "MYR"
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity)
        {
            Currency = "MYR"
        };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);

        var service = new MyERP.Accounting.DomainServices.PeriodClosingPostingService(
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>(),
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>(),
            accountRepo,
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<FiscalYear, Guid>>(),
            companyRepo,
            new MyERP.Accounting.DomainServices.AccountClosingBalanceService(
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<AccountClosingBalance, Guid>>(),
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>(),
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>()
            )
        );

        await service.ValidateForSubmitAsync(pcv); // Should not throw
    }
}
