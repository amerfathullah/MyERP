using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for General and Payment Ledger Comparison report per ERPNext PR #60013 / commits faaae8f27fe, 2727084167.
/// - Compares GL entries against Payment Ledger entries (PLE) on receivable and payable accounts.
/// - Groups by against-voucher as well as voucher.
/// - Folds reconciled PLE allocations under unallocated GL row when GL lacks an invoice counterpart, preventing false mismatches.
/// </summary>
public class GeneralAndPaymentLedgerComparisonTests
{
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepo = Substitute.For<IRepository<AccountClosingBalance, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepo = Substitute.For<IRepository<FiscalYear, Guid>>();
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();

    private readonly AccountBalanceService _balanceService;
    private readonly ReportingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _debtorsAccountId = Guid.NewGuid();
    private readonly Guid _creditorsAccountId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Account _debtorsAccount;
    private readonly Account _creditorsAccount;
    private readonly Customer _customer;

    public GeneralAndPaymentLedgerComparisonTests()
    {
        _debtorsAccount = new Account(
            _debtorsAccountId, _companyId, "1310", "Debtors", AccountType.Asset)
        {
            AccountSubType = AccountSubType.AccountsReceivable
        };

        _creditorsAccount = new Account(
            _creditorsAccountId, _companyId, "2110", "Creditors", AccountType.Liability)
        {
            AccountSubType = AccountSubType.AccountsPayable
        };

        _customer = new Customer(_customerId, _companyId, "Test Customer");

        _accountRepo.GetListAsync(Arg.Any<Expression<Func<Account, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Account, bool>>>().Compile();
                return Task.FromResult(new List<Account> { _debtorsAccount, _creditorsAccount }.Where(predicate).ToList());
            });

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<Customer, bool>>>().Compile();
                return Task.FromResult(new List<Customer> { _customer }.Where(predicate).ToList());
            });

        _supplierRepo.GetListAsync(Arg.Any<Expression<Func<Supplier, bool>>>())
            .Returns(Task.FromResult(new List<Supplier>()));

        _balanceService = new AccountBalanceService(_journalEntryRepo, _journalLineRepo, _closingBalanceRepo, _accountRepo);
        _appService = new ReportingAppService(
            _accountRepo,
            _journalLineRepo,
            _journalEntryRepo,
            _balanceService,
            _customerRepo,
            _supplierRepo,
            _companyRepo,
            _fiscalYearRepo,
            pleRepository: _pleRepo);
    }

    [Fact]
    public async Task BasicComparison_WhenBalancesMatch_ReturnsZeroVariations()
    {
        var invoiceId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 1);

        var je = CreateJournalEntry(invoiceId, "SalesInvoice", "INV-001", postingDate);
        var jeLine = new JournalEntryLine(Guid.NewGuid(), je.Id, _debtorsAccountId, 1000m, true)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "SalesInvoice",
            AgainstVoucherId = invoiceId
        };
        je.AddLine(jeLine);
        je.AddLine(_bankAccountId, 1000m, false);
        je.Post();

        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "SalesInvoice", invoiceId, "SalesInvoice", invoiceId, 1000m, 1000m, "MYR");

        SetupQueryables(new List<JournalEntry> { je }, new List<PaymentLedgerEntry> { ple });

        var report = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId });

        report.Rows.ShouldBeEmpty();
        report.TotalDifference.ShouldBe(0m);
    }

    [Fact]
    public async Task BasicComparison_WhenPLEMismatches_ReportsVariationRow()
    {
        var invoiceId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 1);

        var je = CreateJournalEntry(invoiceId, "SalesInvoice", "INV-001", postingDate);
        var jeLine = new JournalEntryLine(Guid.NewGuid(), je.Id, _debtorsAccountId, 1000m, true)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "SalesInvoice",
            AgainstVoucherId = invoiceId
        };
        je.AddLine(jeLine);
        je.AddLine(_bankAccountId, 1000m, false);
        je.Post();

        // PLE tampered / altered to 999m (diff of 1m)
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "SalesInvoice", invoiceId, "SalesInvoice", invoiceId, 999m, 999m, "MYR");

        SetupQueryables(new List<JournalEntry> { je }, new List<PaymentLedgerEntry> { ple });

        var report = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId });

        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.VoucherNo.ShouldBe("INV-001");
        row.AgainstVoucherNo.ShouldBe("INV-001");
        row.GLBalance.ShouldBe(1000m);
        row.PLBalance.ShouldBe(999m);
        row.Difference.ShouldBe(1m);
        report.TotalDifference.ShouldBe(1m);
    }

    [Fact]
    public async Task Comparison_FiltersByAccountVoucherNoAndDateRange()
    {
        var invoiceId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 15);

        var je = CreateJournalEntry(invoiceId, "SalesInvoice", "INV-001", postingDate);
        var jeLine = new JournalEntryLine(Guid.NewGuid(), je.Id, _debtorsAccountId, 1000m, true)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "SalesInvoice",
            AgainstVoucherId = invoiceId
        };
        je.AddLine(jeLine);
        je.AddLine(_bankAccountId, 1000m, false);
        je.Post();

        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "SalesInvoice", invoiceId, "SalesInvoice", invoiceId, 900m, 900m, "MYR");

        SetupQueryables(new List<JournalEntry> { je }, new List<PaymentLedgerEntry> { ple });

        // Filter by matching account -> shows mismatch
        var reportAcc = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId, AccountId = _debtorsAccountId });
        reportAcc.Rows.Count.ShouldBe(1);

        // Filter by creditors account -> empty
        var reportCred = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId, AccountId = _creditorsAccountId });
        reportCred.Rows.ShouldBeEmpty();

        // Filter by matching voucher number
        var reportVoucher = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId, VoucherNo = "INV-001" });
        reportVoucher.Rows.Count.ShouldBe(1);

        // Filter by different voucher number
        var reportOtherVoucher = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId, VoucherNo = "INV-999" });
        reportOtherVoucher.Rows.ShouldBeEmpty();

        // Filter by out-of-range dates
        var reportOutRange = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto
            {
                CompanyId = _companyId,
                PeriodStartDate = new DateTime(2026, 7, 1),
                PeriodEndDate = new DateTime(2026, 7, 31)
            });
        reportOutRange.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task FoldReconciledAllocations_WhenPaymentReconciledToInvoice_DoesNotReportMismatch()
    {
        // Reconciling an advance payment points PLE to the invoice, but leaves GL entry unallocated (against payment).
        // Per ERPNext PR #60013 / commit 2727084167: fold PLE row whose against voucher has no GL counterpart under unallocated.
        var invoiceId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 1);

        // GL side: payment entry is unallocated (CR 60 -> -60 net balance, against itself: paymentId)
        var jePayment = CreateJournalEntry(paymentId, "PaymentEntry", "PAY-001", postingDate);
        var jePaymentLine = new JournalEntryLine(Guid.NewGuid(), jePayment.Id, _debtorsAccountId, 60m, false)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "PaymentEntry",
            AgainstVoucherId = paymentId
        };
        jePayment.AddLine(_bankAccountId, 60m, true);
        jePayment.AddLine(jePaymentLine);
        jePayment.Post();

        // PLE side: reconciled against invoice! (amount -60, againstVoucherId: invoiceId)
        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "PaymentEntry", paymentId, "SalesInvoice", invoiceId, -60m, -60m, "MYR");

        SetupQueryables(new List<JournalEntry> { jePayment }, new List<PaymentLedgerEntry> { ple });

        var report = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId });

        // Since the PLE row against invoiceId has no GL counterpart, but unallocated against paymentId does,
        // it folds under unallocated and matches GL (-60 == -60) -> NO MISMATCH!
        report.Rows.ShouldBeEmpty();
        report.TotalDifference.ShouldBe(0m);
    }

    [Fact]
    public async Task FoldReconciledAllocations_WhenPaymentAppliedToAnotherInvoiceInPLE_ReportsVariationsPerAgainstVoucher()
    {
        // Per ERPNext PR #60013 / commit faaae8f27fe:
        // Payment of 60: in GL, 40 against invoice 1, 20 unallocated against self.
        // In PLE, row of 40 is against invoice 2 instead of invoice 1.
        // The PLE row against invoice 2 is folded into unallocated, reporting:
        // (payment, invoice 1): GL -40, PL 0 (diff -40)
        // (payment, payment): GL -20, PL -60 (diff 40)
        var invoice1Id = Guid.NewGuid();
        var invoice2Id = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var postingDate = new DateTime(2026, 6, 1);

        var jePayment = CreateJournalEntry(paymentId, "PaymentEntry", "PAY-001", postingDate);
        // GL leg 1: -40 against invoice 1
        var line1 = new JournalEntryLine(Guid.NewGuid(), jePayment.Id, _debtorsAccountId, 40m, false)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "SalesInvoice",
            AgainstVoucherId = invoice1Id
        };
        // GL leg 2: -20 unallocated against self
        var line2 = new JournalEntryLine(Guid.NewGuid(), jePayment.Id, _debtorsAccountId, 20m, false)
        {
            PartyType = "Customer",
            PartyId = _customerId,
            AgainstVoucherType = "PaymentEntry",
            AgainstVoucherId = paymentId
        };
        jePayment.AddLine(_bankAccountId, 60m, true);
        jePayment.AddLine(line1);
        jePayment.AddLine(line2);
        jePayment.Post();

        // In PLE: -40 against invoice 2, and -20 unallocated against self
        var ple1 = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "PaymentEntry", paymentId, "SalesInvoice", invoice2Id, -40m, -40m, "MYR");
        var ple2 = new PaymentLedgerEntry(
            Guid.NewGuid(), _companyId, postingDate, _debtorsAccountId, "Customer", _customerId,
            "PaymentEntry", paymentId, "PaymentEntry", paymentId, -20m, -20m, "MYR");

        SetupQueryables(new List<JournalEntry> { jePayment }, new List<PaymentLedgerEntry> { ple1, ple2 });

        var report = await _appService.GetGeneralAndPaymentLedgerComparisonAsync(
            new GeneralPaymentLedgerComparisonRequestDto { CompanyId = _companyId });

        report.Rows.Count.ShouldBe(2);

        var rowInv1 = report.Rows.Single(r => r.AgainstVoucherId == invoice1Id);
        rowInv1.GLBalance.ShouldBe(-40m);
        rowInv1.PLBalance.ShouldBe(0m);
        rowInv1.Difference.ShouldBe(-40m);

        var rowUnallocated = report.Rows.Single(r => r.AgainstVoucherId == paymentId);
        rowUnallocated.GLBalance.ShouldBe(-20m);
        rowUnallocated.PLBalance.ShouldBe(-60m);
        rowUnallocated.Difference.ShouldBe(40m);
    }

    private JournalEntry CreateJournalEntry(Guid referenceId, string referenceType, string referenceNumber, DateTime postingDate)
    {
        return new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), postingDate)
        {
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            ReferenceNumber = referenceNumber
        };
    }

    private void SetupQueryables(List<JournalEntry> journalEntries, List<PaymentLedgerEntry> pleEntries)
    {
        _journalEntryRepo.GetQueryableAsync()
            .Returns(Task.FromResult(journalEntries.AsQueryable()));

        _pleRepo.GetListAsync(Arg.Any<Expression<Func<PaymentLedgerEntry, bool>>>())
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<PaymentLedgerEntry, bool>>>().Compile();
                return Task.FromResult(pleEntries.Where(predicate).ToList());
            });
    }
}
