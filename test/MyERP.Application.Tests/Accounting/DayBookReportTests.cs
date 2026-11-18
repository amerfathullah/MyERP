using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for Day Book report per ERPNext PR #62896 / commit be89c1c68d.
/// </summary>
public class DayBookReportTests
{
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<FinanceBook, Guid> _financeBookRepo = Substitute.For<IRepository<FinanceBook, Guid>>();
    private readonly IRepository<CostCenter, Guid> _costCenterRepo = Substitute.For<IRepository<CostCenter, Guid>>();
    private readonly IRepository<Project, Guid> _projectRepo = Substitute.For<IRepository<Project, Guid>>();

    private readonly ReportingAppService _appService;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _debtorAccId = Guid.NewGuid();
    private readonly Guid _salesAccId = Guid.NewGuid();
    private readonly Guid _bankAccId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public DayBookReportTests()
    {
        var debtor = new Account(_debtorAccId, _companyId, "1200", "Debtors", AccountType.Asset);
        var sales = new Account(_salesAccId, _companyId, "4100", "Sales", AccountType.Revenue);
        var bank = new Account(_bankAccId, _companyId, "1110", "Bank", AccountType.Asset);

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(Task.FromResult(new List<Account> { debtor, sales, bank }));

        var customer = new Customer(_customerId, _companyId, "Acme Trading");
        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { customer }));
        _supplierRepo.GetListAsync(Arg.Any<Expression<Func<Supplier, bool>>>())
            .Returns(Task.FromResult(new List<Supplier>()));
        _financeBookRepo.GetListAsync(Arg.Any<Expression<Func<FinanceBook, bool>>>())
            .Returns(Task.FromResult(new List<FinanceBook>()));

        var balanceService = new AccountBalanceService(_journalEntryRepo, _journalLineRepo, _closingBalanceRepo, _accountRepo);
        _appService = new ReportingAppService(
            _accountRepo,
            _journalLineRepo,
            _journalEntryRepo,
            balanceService,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            _fiscalYearRepo,
            financeBookRepository: _financeBookRepo,
            costCenterRepository: _costCenterRepo,
            projectRepository: _projectRepo);
    }

    [Fact]
    public async Task GetDayBookAsync_SummaryAndDetailed_FormatsVouchersAndChildLegs()
    {
        var fyId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
        var siId = Guid.NewGuid();
        var peId = Guid.NewGuid();

        var siJe = new JournalEntry(Guid.NewGuid(), _companyId, fyId, postingDate)
        {
            EntryNumber = "JE-SI-1",
            ReferenceType = "SalesInvoice",
            ReferenceId = siId,
            ReferenceNumber = "SI-2026-0001",
            Narration = "Sales invoice posting"
        };
        siJe.AddLine(_debtorAccId, 150m, true, partyType: "Customer", partyId: _customerId);
        siJe.AddLine(_salesAccId, 150m, false);
        siJe.Post();

        var peJe = new JournalEntry(Guid.NewGuid(), _companyId, fyId, postingDate.AddHours(1))
        {
            EntryNumber = "JE-PE-1",
            ReferenceType = "PaymentEntry",
            ReferenceId = peId,
            ReferenceNumber = "PE-2026-0001",
            Narration = "Customer payment"
        };
        peJe.AddLine(_bankAccId, 100m, true);
        peJe.AddLine(_debtorAccId, 100m, false, partyType: "Customer", partyId: _customerId);
        peJe.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(ci =>
            {
                var pred = ci.Arg<Expression<Func<JournalEntry, bool>>>().Compile();
                return Task.FromResult(new List<JournalEntry> { siJe, peJe }.FindAll(je => pred(je)));
            });

        var report = await _appService.GetDayBookAsync(new DayBookRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc),
            Detailed = true,
            ShowRemarks = true,
        });

        report.VoucherCount.ShouldBe(2);
        report.TotalDebit.ShouldBe(150m);
        report.TotalCredit.ShouldBe(100m);

        var summaryRows = report.Rows.FindAll(r => r.Indent == 0);
        summaryRows.Count.ShouldBe(2);
        summaryRows[0].VoucherType.ShouldBe("SalesInvoice");
        summaryRows[0].VoucherNo.ShouldBe("SI-2026-0001");
        summaryRows[0].Particulars.ShouldBe("Acme Trading");
        summaryRows[0].Debit.ShouldBe(150m);
        summaryRows[0].Credit.ShouldBeNull();
        summaryRows[0].Remarks.ShouldBe("Sales invoice posting");

        summaryRows[1].VoucherType.ShouldBe("PaymentEntry");
        summaryRows[1].VoucherNo.ShouldBe("PE-2026-0001");
        summaryRows[1].Particulars.ShouldBe("Acme Trading");
        summaryRows[1].Debit.ShouldBeNull();
        summaryRows[1].Credit.ShouldBe(100m);

        var siLegs = report.Rows.FindAll(r => r.ParentVoucherKey == summaryRows[0].VoucherKey);
        siLegs.Count.ShouldBe(2);
        siLegs[0].Indent.ShouldBe(1);
        var legDebits = 0m;
        var legCredits = 0m;
        foreach (var leg in siLegs)
        {
            legDebits += leg.Debit ?? 0m;
            legCredits += leg.Credit ?? 0m;
        }
        legDebits.ShouldBe(legCredits);
    }

    [Fact]
    public async Task GetDayBookAsync_FiltersByVoucherType_AndValidatesDateRange()
    {
        var fyId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        var peId = Guid.NewGuid();

        var peJe = new JournalEntry(Guid.NewGuid(), _companyId, fyId, postingDate)
        {
            EntryNumber = "JE-PE-2",
            ReferenceType = "PaymentEntry",
            ReferenceId = peId,
            ReferenceNumber = "PE-2026-0002"
        };
        peJe.AddLine(_bankAccId, 75m, true);
        peJe.AddLine(_debtorAccId, 75m, false, partyType: "Customer", partyId: _customerId);
        peJe.Post();

        var standaloneJe = new JournalEntry(Guid.NewGuid(), _companyId, fyId, postingDate)
        {
            EntryNumber = "JE-0009"
        };
        standaloneJe.AddLine(_bankAccId, 25m, true);
        standaloneJe.AddLine(_salesAccId, 25m, false);
        standaloneJe.Post();

        _journalEntryRepo.GetListAsync(Arg.Any<Expression<Func<JournalEntry, bool>>>())
            .Returns(Task.FromResult(new List<JournalEntry> { peJe, standaloneJe }));

        var filtered = await _appService.GetDayBookAsync(new DayBookRequestDto
        {
            CompanyId = _companyId,
            FromDate = postingDate,
            ToDate = postingDate,
            VoucherType = "PaymentEntry",
        });

        filtered.VoucherCount.ShouldBe(1);
        filtered.Rows.Count.ShouldBe(1);
        filtered.Rows[0].VoucherType.ShouldBe("PaymentEntry");
        filtered.Rows[0].Remarks.ShouldBeNull();

        await Should.ThrowAsync<BusinessException>(() => _appService.GetDayBookAsync(new DayBookRequestDto
        {
            CompanyId = _companyId,
            FromDate = postingDate.AddDays(1),
            ToDate = postingDate,
        }));
    }
}
