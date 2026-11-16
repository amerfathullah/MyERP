using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Application.Tests.Accounting;

/// <summary>
/// Verifies core financial statement services include intra-day posted journals up to 23:59:59.9999999
/// on ToDate/AsOfDate and exclude next-day entries.
/// </summary>
public class FinancialStatementsToDateCutoffTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _fyId = Guid.NewGuid();
    private readonly DateTime _fromDate = new(2026, 6, 1);
    private readonly DateTime _toDate = new(2026, 6, 30);

    private static JournalEntry CreatePostedJe(Guid companyId, Guid fyId, DateTime postingDate, Guid debitAcc, Guid creditAcc, decimal amount, Guid? partyId = null, string? partyType = null)
    {
        var je = new JournalEntry(Guid.NewGuid(), companyId, fyId, postingDate);
        je.AddLine(debitAcc, amount, isDebit: true, partyId: partyId, partyType: partyType);
        je.AddLine(creditAcc, amount, isDebit: false);
        je.Post();
        return je;
    }

    [Fact]
    public async Task ReportingAppService_ProfitLossBalanceSheetAndPartyTrialBalance_IncludeIntraDayOnToDate()
    {
        var accRepo = Substitute.For<IRepository<Account, Guid>>();
        var jlRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var supRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
        var closingRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();

        var bankAccId = Guid.NewGuid();
        var revAccId = Guid.NewGuid();
        var custId = Guid.NewGuid();

        var allAccounts = new List<Account>
        {
            new(bankAccId, _companyId, "1110", "Bank", AccountType.Asset),
            new(revAccId, _companyId, "4100", "Sales", AccountType.Revenue)
        };
        accRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(ci => allAccounts.Where(ci.Arg<Expression<Func<Account, bool>>>().Compile()).ToList());

        compRepo.FindAsync(_companyId).Returns(new Company(_companyId, "Acme Co") { CurrencyCode = "MYR" });
        fyRepo.GetListAsync(Arg.Any<Expression<Func<FiscalYear, bool>>>())
            .Returns(new List<FiscalYear> { new(_fyId, _companyId, "FY2026", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)) });
        custRepo.GetQueryableAsync().Returns(new List<Customer> { new(custId, _companyId, "Buyer Co") }.AsQueryable());
        supRepo.GetQueryableAsync().Returns(new List<Supplier>().AsQueryable());

        var jeSameDayLate = CreatePostedJe(_companyId, _fyId, _toDate.AddHours(23).AddMinutes(45), bankAccId, revAccId, 400m, custId, "Customer");
        var jeNextDay = CreatePostedJe(_companyId, _fyId, _toDate.AddDays(1), bankAccId, revAccId, 900m, custId, "Customer");
        var allJes = new List<JournalEntry> { jeSameDayLate, jeNextDay };

        jeRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(ci => allJes.Where(ci.Arg<Expression<Func<JournalEntry, bool>>>().Compile()).ToList());

        var balanceService = new AccountBalanceService(jeRepo, jlRepo, closingRepo, accRepo);
        var svc = new ReportingAppService(accRepo, jlRepo, jeRepo, balanceService, custRepo, supRepo, compRepo, fyRepo);

        var pl = await svc.GetProfitLossAsync(new ProfitLossRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        });
        pl.TotalRevenue.ShouldBe(400m);

        var bs = await svc.GetBalanceSheetAsync(new BalanceSheetRequestDto
        {
            CompanyId = _companyId,
            AsOfDate = _toDate
        });
        bs.TotalAssets.ShouldBe(400m);

        var ptb = await svc.GetTrialBalanceForPartyAsync(new PartyTrialBalanceRequestDto
        {
            CompanyId = _companyId,
            PartyType = "Customer",
            FromDate = _fromDate,
            ToDate = _toDate
        });
        ptb.TotalDebit.ShouldBe(400m);
        ptb.TotalClosingDebit.ShouldBe(400m);
    }

    [Fact]
    public async Task ProfitLossByCostCenter_IncludesIntraDayJournalsOnToDate()
    {
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var ccRepo = Substitute.For<IRepository<CostCenter, Guid>>();
        var accRepo = Substitute.For<IRepository<Account, Guid>>();

        var ccId = Guid.NewGuid();
        var cashAccId = Guid.NewGuid();
        var revAccId = Guid.NewGuid();

        ccRepo.GetQueryableAsync().Returns(new List<CostCenter> { new(ccId, _companyId, "Operations") }.AsQueryable());
        accRepo.GetQueryableAsync().Returns(new List<Account>
        {
            new(cashAccId, _companyId, "1000", "Cash", AccountType.Asset),
            new(revAccId, _companyId, "4000", "Sales", AccountType.Revenue)
        }.AsQueryable());

        var jeSameDay = new JournalEntry(Guid.NewGuid(), _companyId, _fyId, _toDate.AddHours(23).AddMinutes(50));
        jeSameDay.AddLine(cashAccId, 300m, isDebit: true);
        jeSameDay.AddLine(revAccId, 300m, isDebit: false, costCenterId: ccId);
        jeSameDay.Post();

        var jeNextDay = new JournalEntry(Guid.NewGuid(), _companyId, _fyId, _toDate.AddDays(1));
        jeNextDay.AddLine(cashAccId, 700m, isDebit: true);
        jeNextDay.AddLine(revAccId, 700m, isDebit: false, costCenterId: ccId);
        jeNextDay.Post();

        jeRepo.GetQueryableAsync().Returns(new List<JournalEntry> { jeSameDay, jeNextDay }.AsQueryable());

        var svc = new ProfitLossByCostCenterAppService(jeRepo, ccRepo, accRepo);
        var result = await svc.GetReportAsync(_companyId, _fromDate, _toDate);

        result.TotalRevenue.ShouldBe(300m);
        result.CostCenters.ShouldHaveSingleItem();
    }
}
