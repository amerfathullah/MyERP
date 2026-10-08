using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using MyERP.Inventory.Entities;
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
    public void Submit_WithNoEntries_Succeeds()
    {
        // A period with zero P&L activity (e.g. a new company's first year) is still a valid
        // period to close — per ERPNext, before_submit only skips stock-balance validation when
        // there's nothing to check, it never blocks the submit itself.
        var pcv = CreatePCV();
        pcv.Submit();
        pcv.Status.ShouldBe(Core.DocumentStatus.Submitted);
        pcv.TotalClosingAmount.ShouldBe(0);
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

    [Fact]
    public void SetStockValueDifference_SetsValue_WhenDraft()
    {
        var pcv = CreatePCV();
        pcv.SetStockValueDifference(-5.50m);
        pcv.StockValueDifference.ShouldBe(-5.50m);

        pcv.SetStockValueDifference(null);
        pcv.StockValueDifference.ShouldBeNull();
    }

    [Fact]
    public void SetStockValueDifference_Throws_WhenSubmitted()
    {
        var pcv = CreatePCV();
        pcv.Submit();
        Should.Throw<BusinessException>(() => pcv.SetStockValueDifference(10m));
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_ThrowsWhenStockAccountsBalanceDiffersBeyondTolerance()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var stockAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            CurrencyCode = "MYR",
            EnablePerpetualInventory = true
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity) { Currency = "MYR" };
        var stockAccount = new Account(stockAccountId, companyId, "Stock in Hand", "1500", AccountType.Asset)
        {
            AccountSubType = AccountSubType.Stock,
            IsGroup = false,
            IsActive = true
        };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);
        accountRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { stockAccount });

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);
        companyRepo.FindAsync(companyId).Returns(company);

        var jeId = Guid.NewGuid();
        var je = new JournalEntry(jeId, companyId, Guid.NewGuid(), postingDate);
        je.AddLine(stockAccountId, 1000m, true);
        je.AddLine(Guid.NewGuid(), 1000m, false);
        je.Post();
        var journalRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>();
        journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { je });

        var line = new JournalEntryLine(Guid.NewGuid(), jeId, stockAccountId, 1000m, true);
        var lineRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>();
        lineRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntryLine, bool>>>())
            .Returns(new List<JournalEntryLine> { line });

        // SLE: stock value delta is 800m -> difference is 200m (tolerance 1% of 800 = 8m)
        var sle = new StockLedgerEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            postingDate, 10m, 80m, 10m, 800m)
        {
            StockValueDifference = 800m
        };
        var sleRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        var stockClosing = new StockClosingEntry(Guid.NewGuid(), companyId, postingDate);
        stockClosing.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 10m, 800m, 80m, null);
        stockClosing.Submit();
        var stockClosingRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockClosingEntry, Guid>>();
        stockClosingRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockClosingEntry> { stockClosing }.AsQueryable()));

        var service = new MyERP.Accounting.DomainServices.PeriodClosingPostingService(
            journalRepo,
            lineRepo,
            accountRepo,
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<FiscalYear, Guid>>(),
            companyRepo,
            new MyERP.Accounting.DomainServices.AccountClosingBalanceService(
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<AccountClosingBalance, Guid>>(),
                journalRepo,
                lineRepo
            ),
            sleRepo,
            stockClosingRepo
        );

        // Even with acceptedDifference = 200m, it exceeds 1% tolerance so it must throw
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateForSubmitAsync(pcv, acceptedDifference: 200m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_ThrowsWhenStockAccountsBalanceDiffersWithinTolerance_WithoutAcceptedDifference()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var stockAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            CurrencyCode = "MYR",
            EnablePerpetualInventory = true
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity) { Currency = "MYR" };
        var stockAccount = new Account(stockAccountId, companyId, "Stock in Hand", "1500", AccountType.Asset)
        {
            AccountSubType = AccountSubType.Stock,
            IsGroup = false,
            IsActive = true
        };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);
        accountRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { stockAccount });

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);
        companyRepo.FindAsync(companyId).Returns(company);

        var jeId = Guid.NewGuid();
        var je = new JournalEntry(jeId, companyId, Guid.NewGuid(), postingDate);
        je.AddLine(stockAccountId, 1005m, true);
        je.AddLine(Guid.NewGuid(), 1005m, false);
        je.Post();
        var journalRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>();
        journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { je });

        var line = new JournalEntryLine(Guid.NewGuid(), jeId, stockAccountId, 1005m, true);
        var lineRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>();
        lineRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntryLine, bool>>>())
            .Returns(new List<JournalEntryLine> { line });

        // SLE: stock value delta is 1000m -> difference is 5m (within 1% tolerance of 1000m = 10m)
        var sle = new StockLedgerEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            postingDate, 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m
        };
        var sleRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        var stockClosing = new StockClosingEntry(Guid.NewGuid(), companyId, postingDate);
        stockClosing.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 10m, 1000m, 100m, null);
        stockClosing.Submit();
        var stockClosingRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockClosingEntry, Guid>>();
        stockClosingRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockClosingEntry> { stockClosing }.AsQueryable()));

        var service = new MyERP.Accounting.DomainServices.PeriodClosingPostingService(
            journalRepo,
            lineRepo,
            accountRepo,
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<FiscalYear, Guid>>(),
            companyRepo,
            new MyERP.Accounting.DomainServices.AccountClosingBalanceService(
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<AccountClosingBalance, Guid>>(),
                journalRepo,
                lineRepo
            ),
            sleRepo,
            stockClosingRepo
        );

        // Without acceptedDifference, it must throw despite being within tolerance
        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateForSubmitAsync(pcv));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_SucceedsWhenStockAccountsBalanceDiffersWithinTolerance_WithAcceptedDifference()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var stockAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            CurrencyCode = "MYR",
            EnablePerpetualInventory = true
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity) { Currency = "MYR" };
        var stockAccount = new Account(stockAccountId, companyId, "Stock in Hand", "1500", AccountType.Asset)
        {
            AccountSubType = AccountSubType.Stock,
            IsGroup = false,
            IsActive = true
        };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);
        accountRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<Account, bool>>>())
            .Returns(new List<Account> { stockAccount });

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);
        companyRepo.FindAsync(companyId).Returns(company);

        var jeId = Guid.NewGuid();
        var je = new JournalEntry(jeId, companyId, Guid.NewGuid(), postingDate);
        je.AddLine(stockAccountId, 1005m, true);
        je.AddLine(Guid.NewGuid(), 1005m, false);
        je.Post();
        var journalRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntry, Guid>>();
        journalRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, bool>>>())
            .Returns(new List<JournalEntry> { je });

        var line = new JournalEntryLine(Guid.NewGuid(), jeId, stockAccountId, 1005m, true);
        var lineRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<JournalEntryLine, Guid>>();
        lineRepo.GetListAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntryLine, bool>>>())
            .Returns(new List<JournalEntryLine> { line });

        // SLE: stock value delta is 1000m -> difference is 5m (within 1% tolerance of 1000m = 10m)
        var sle = new StockLedgerEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            postingDate, 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m
        };
        var sleRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        var stockClosing = new StockClosingEntry(Guid.NewGuid(), companyId, postingDate);
        stockClosing.AddBalance(Guid.NewGuid(), Guid.NewGuid(), 10m, 1000m, 100m, null);
        stockClosing.Submit();
        var stockClosingRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockClosingEntry, Guid>>();
        stockClosingRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockClosingEntry> { stockClosing }.AsQueryable()));

        var service = new MyERP.Accounting.DomainServices.PeriodClosingPostingService(
            journalRepo,
            lineRepo,
            accountRepo,
            Substitute.For<Volo.Abp.Domain.Repositories.IRepository<FiscalYear, Guid>>(),
            companyRepo,
            new MyERP.Accounting.DomainServices.AccountClosingBalanceService(
                Substitute.For<Volo.Abp.Domain.Repositories.IRepository<AccountClosingBalance, Guid>>(),
                journalRepo,
                lineRepo
            ),
            sleRepo,
            stockClosingRepo
        );

        // When accepted difference matches 5m, validation succeeds
        await service.ValidateForSubmitAsync(pcv, acceptedDifference: 5m);
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateForSubmitAsync_ThrowsWhenStockClosingEntryMissing()
    {
        var companyId = Guid.NewGuid();
        var closingAccountId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        var pcv = new PeriodClosingVoucher(Guid.NewGuid(), companyId, Guid.NewGuid(),
            postingDate, postingDate, closingAccountId);

        var company = new MyERP.Core.Entities.Company(companyId, "Test Company")
        {
            CurrencyCode = "MYR",
            EnablePerpetualInventory = true
        };
        var closingAccount = new Account(closingAccountId, companyId, "Retained Earnings", "3100", AccountType.Equity) { Currency = "MYR" };

        var accountRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<Account, Guid>>();
        accountRepo.GetAsync(closingAccountId).Returns(closingAccount);

        var companyRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Core.Entities.Company, Guid>>();
        companyRepo.GetAsync(companyId).Returns(company);
        companyRepo.FindAsync(companyId).Returns(company);

        var sle = new StockLedgerEntry(Guid.NewGuid(), companyId, Guid.NewGuid(), Guid.NewGuid(),
            postingDate, 10m, 100m, 10m, 1000m)
        {
            StockValueDifference = 1000m
        };
        var sleRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockLedgerEntry, Guid>>();
        sleRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockLedgerEntry> { sle }.AsQueryable()));

        // No submitted stock closing entry exists
        var stockClosingRepo = Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.StockClosingEntry, Guid>>();
        stockClosingRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(new List<StockClosingEntry>().AsQueryable()));

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
            ),
            sleRepo,
            stockClosingRepo
        );

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await service.ValidateForSubmitAsync(pcv));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }
}
