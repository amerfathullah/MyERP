using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Unit tests for BankClearanceAppService.
/// Covers ERPNext PR #60044 / commits 39c57a0402, b5ecdf00f1 parity:
/// - Payment Entry bank-side amount after taxes (ReceivedAmountAfterTax / PaidAmountAfterTax)
/// - Multi-currency / foreign bank accounts resolved via GetAmountInBankCurrency
/// - Journal Entries touching bank accounts aggregated per entry
/// - Clearance date bulk update and posting date validation
/// - Uncleared vs cleared entry filtering
/// </summary>
public class BankClearanceAppServiceTests
{
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _otherAccountId = Guid.NewGuid();

    private BankClearanceAppService CreateAppService()
    {
        return new BankClearanceAppService(
            _paymentRepo,
            _journalRepo,
            _journalLineRepo,
            _accountRepo,
            _companyRepo);
    }

    private void MockPaymentQuery(List<PaymentEntry> list)
    {
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(list.AsQueryable()));
        _paymentRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentEntry, object>>[]>())
            .Returns(Task.FromResult(list.AsQueryable()));
    }

    [Fact]
    public async Task GetEntriesAsync_PaymentEntryWithTaxes_ReflectsBankSideAmountAfterTax()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var bankAccount = new Account(_bankAccountId, _companyId, "1120", "Bank Account", AccountType.Asset) { Currency = "MYR" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(bankAccount));

        var postingDate = new DateTime(2026, 6, 15);

        // Receive payment with 10% deducted tax (e.g. withholding / fee):
        // 1000 nominal - 100 tax = 900 effective deposit
        var receipt = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, postingDate,
            1000m, _otherAccountId, _bankAccountId)
        {
            ReferenceNumber = "REC-1001"
        };
        receipt.AddTax(new PaymentEntryTax(Guid.NewGuid(), receipt.Id, _otherAccountId)
        {
            Description = "Withholding Tax",
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 10m,
            AddDeductTax = TaxAddDeduct.Deduct,
            TaxAmount = 100m
        });
        receipt.SetAmounts();
        receipt.Submit();
        receipt.Post();

        // Pay payment with 18% included tax:
        // 1180 nominal payment out
        var payment = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Pay, postingDate,
            1180m, _bankAccountId, _otherAccountId)
        {
            ReferenceNumber = "PAY-2002"
        };
        payment.AddTax(new PaymentEntryTax(Guid.NewGuid(), payment.Id, _otherAccountId)
        {
            Description = "Service Tax",
            ChargeType = PaymentTaxChargeType.OnPaidAmount,
            Rate = 18m,
            AddDeductTax = TaxAddDeduct.Add,
            TaxAmount = 180m,
            IncludedInPaidAmount = true
        });
        payment.SetAmounts();
        payment.Submit();
        payment.Post();

        var peList = new List<PaymentEntry> { receipt, payment };
        MockPaymentQuery(peList);
        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));

        var appService = CreateAppService();
        var result = await appService.GetEntriesAsync(new GetBankClearanceEntriesInput
        {
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            IncludeCleared = false
        });

        result.Count.ShouldBe(2);

        var receiptEntry = result.Single(r => r.DocumentId == receipt.Id);
        receiptEntry.Debit.ShouldBe(900m);
        receiptEntry.Credit.ShouldBe(0m);
        receiptEntry.ReferenceNumber.ShouldBe("REC-1001");

        var paymentEntry = result.Single(r => r.DocumentId == payment.Id);
        paymentEntry.Debit.ShouldBe(0m);
        paymentEntry.Credit.ShouldBe(1180m);
        paymentEntry.ReferenceNumber.ShouldBe("PAY-2002");
    }

    [Fact]
    public async Task GetEntriesAsync_ForeignCurrencyBankAccount_ResolvesBankCurrencyAmount()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var foreignBankAccount = new Account(_bankAccountId, _companyId, "1121", "USD Bank", AccountType.Asset) { Currency = "USD" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(foreignBankAccount));

        var postingDate = new DateTime(2026, 6, 15);
        var payment = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Pay, postingDate,
            100m, _bankAccountId, _otherAccountId)
        {
            CurrencyCode = "USD",
            ExchangeRate = 4.5m,
            ReferenceNumber = "USD-WIRE-01"
        };
        payment.SetAmounts();
        payment.Submit();
        payment.Post();

        MockPaymentQuery(new List<PaymentEntry> { payment });
        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));

        var appService = CreateAppService();
        var result = await appService.GetEntriesAsync(new GetBankClearanceEntriesInput
        {
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            IncludeCleared = false
        });

        result.Count.ShouldBe(1);
        var entry = result[0];
        // Bank is in USD, company in MYR -> GetAmountInBankCurrency picks paidAmount in bank currency (100 USD)
        entry.Credit.ShouldBe(100m);
        entry.Debit.ShouldBe(0m);
        entry.ReferenceNumber.ShouldBe("USD-WIRE-01");
    }

    [Fact]
    public async Task GetEntriesAsync_JournalEntryLines_AggregatedAndConverted()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var bankAccount = new Account(_bankAccountId, _companyId, "1120", "Bank Account", AccountType.Asset) { Currency = "MYR" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(bankAccount));

        var postingDate = new DateTime(2026, 6, 10);
        var fiscalYearId = Guid.NewGuid();
        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceNumber = "JE-REF-77"
        };
        je.AddLineWithDimensions(_bankAccountId, 500m, true, description: "Bank deposit line 1");
        je.AddLineWithDimensions(_bankAccountId, 250m, true, description: "Bank deposit line 2");
        je.AddLineWithDimensions(_otherAccountId, 750m, false, description: "Revenue line");
        je.Post();

        MockPaymentQuery(new List<PaymentEntry>());
        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je }.AsQueryable()));
        _journalLineRepo.GetQueryableAsync().Returns(Task.FromResult(je.Lines.AsQueryable()));

        var appService = CreateAppService();
        var result = await appService.GetEntriesAsync(new GetBankClearanceEntriesInput
        {
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            IncludeCleared = false
        });

        result.Count.ShouldBe(1);
        var entry = result[0];
        entry.DocumentType.ShouldBe("JournalEntry");
        entry.DocumentId.ShouldBe(je.Id);
        entry.Debit.ShouldBe(750m);
        entry.Credit.ShouldBe(0m);
        entry.ReferenceNumber.ShouldBe("JE-REF-77");
    }

    [Fact]
    public async Task GetEntriesAsync_IncludeCleared_FilterBehavior()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var bankAccount = new Account(_bankAccountId, _companyId, "1120", "Bank Account", AccountType.Asset) { Currency = "MYR" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(bankAccount));

        var postingDate = new DateTime(2026, 6, 15);
        var clearedPe = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, postingDate,
            500m, _otherAccountId, _bankAccountId);
        clearedPe.SetAmounts();
        clearedPe.Submit();
        clearedPe.Post();
        clearedPe.SetClearanceDate(new DateTime(2026, 6, 16));

        var unclearedPe = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, postingDate,
            300m, _otherAccountId, _bankAccountId);
        unclearedPe.SetAmounts();
        unclearedPe.Submit();
        unclearedPe.Post();

        MockPaymentQuery(new List<PaymentEntry> { clearedPe, unclearedPe });
        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));

        var appService = CreateAppService();

        // Default: uncleared only
        var unclearedOnly = await appService.GetEntriesAsync(new GetBankClearanceEntriesInput
        {
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            IncludeCleared = false
        });
        unclearedOnly.Count.ShouldBe(1);
        unclearedOnly[0].DocumentId.ShouldBe(unclearedPe.Id);

        // Include cleared
        var allEntries = await appService.GetEntriesAsync(new GetBankClearanceEntriesInput
        {
            CompanyId = _companyId,
            BankAccountId = _bankAccountId,
            FromDate = new DateTime(2026, 6, 1),
            ToDate = new DateTime(2026, 6, 30),
            IncludeCleared = true
        });
        allEntries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SetClearanceDateAsync_UpdatesClearanceDate_AndClears()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var pe = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, postingDate,
            500m, _otherAccountId, _bankAccountId);
        pe.SetAmounts();
        pe.Submit();
        pe.Post();

        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), postingDate);
        je.AddLineWithDimensions(_bankAccountId, 200m, true);
        je.AddLineWithDimensions(_otherAccountId, 200m, false);
        je.Post();

        _paymentRepo.GetAsync(pe.Id).Returns(Task.FromResult(pe));
        _journalRepo.GetAsync(je.Id).Returns(Task.FromResult(je));

        var appService = CreateAppService();

        // Clear entries
        var clearanceDate = new DateTime(2026, 6, 18);
        var result = await appService.SetClearanceDateAsync(new SetClearanceDateDto
        {
            ClearanceDate = clearanceDate,
            Entries = new List<BankClearanceDocRefDto>
            {
                new() { DocumentType = "PaymentEntry", DocumentId = pe.Id },
                new() { DocumentType = "JournalEntry", DocumentId = je.Id }
            }
        });

        result.UpdatedCount.ShouldBe(2);
        pe.ClearanceDate.ShouldBe(clearanceDate);
        je.ClearanceDate.ShouldBe(clearanceDate);

        // Un-clear entries
        var unClearResult = await appService.SetClearanceDateAsync(new SetClearanceDateDto
        {
            ClearanceDate = null,
            Entries = new List<BankClearanceDocRefDto>
            {
                new() { DocumentType = "PaymentEntry", DocumentId = pe.Id },
                new() { DocumentType = "JournalEntry", DocumentId = je.Id }
            }
        });

        unClearResult.UpdatedCount.ShouldBe(2);
        pe.ClearanceDate.ShouldBeNull();
        je.ClearanceDate.ShouldBeNull();
    }

    [Fact]
    public async Task SetClearanceDateAsync_ClearanceDateBeforePostingDate_Throws()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var pe = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, postingDate,
            500m, _otherAccountId, _bankAccountId);
        pe.SetAmounts();
        pe.Submit();
        pe.Post();

        _paymentRepo.GetAsync(pe.Id).Returns(Task.FromResult(pe));

        var appService = CreateAppService();

        var invalidClearanceDate = new DateTime(2026, 6, 10); // Before June 15
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.SetClearanceDateAsync(new SetClearanceDateDto
            {
                ClearanceDate = invalidClearanceDate,
                Entries = new List<BankClearanceDocRefDto>
                {
                    new() { DocumentType = "PaymentEntry", DocumentId = pe.Id }
                }
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ClearanceDateBeforePostingDate);
    }
}
