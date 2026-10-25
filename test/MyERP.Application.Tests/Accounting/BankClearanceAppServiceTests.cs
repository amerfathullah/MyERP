using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
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
/// - POS Sales Invoice & Paid Purchase Invoice clearance date and reference number resolution
/// </summary>
public class BankClearanceAppServiceTests
{
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<BankTransaction, Guid> _bankTransactionRepo = Substitute.For<IRepository<BankTransaction, Guid>>();

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
            _companyRepo,
            _salesInvoiceRepo,
            _purchaseInvoiceRepo,
            _bankTransactionRepo);
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

    [Fact]
    public async Task GetEntriesAsync_SalesInvoiceJournalEntry_ResolvesDocTypeAndReference()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var bankAccount = new Account(_bankAccountId, _companyId, "1120", "Bank Account", AccountType.Asset) { Currency = "MYR" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(bankAccount));

        var postingDate = new DateTime(2026, 6, 15);
        var fiscalYearId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var si = new SalesInvoice(Guid.NewGuid(), _companyId, customerId, "POS-INV-2026-001", postingDate)
        {
            Notes = "UTR-POS-999"
        };
        _salesInvoiceRepo.FindAsync(si.Id).Returns(Task.FromResult<SalesInvoice?>(si));

        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = si.Id
        };
        je.AddLineWithDimensions(_bankAccountId, 300m, true, description: "POS sale deposit");
        je.AddLineWithDimensions(_otherAccountId, 300m, false, description: "Receivable credit");
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
        entry.DocumentType.ShouldBe("SalesInvoice");
        entry.DocumentId.ShouldBe(je.Id);
        entry.DocumentNumber.ShouldBe("POS-INV-2026-001");
        entry.ReferenceNumber.ShouldBe("UTR-POS-999");
        entry.Debit.ShouldBe(300m);
        entry.Credit.ShouldBe(0m);
    }

    [Fact]
    public async Task GetEntriesAsync_PurchaseInvoiceJournalEntry_ResolvesSupplierInvoiceNumberAsReference()
    {
        var company = new Company(_companyId, "Test Co") { CurrencyCode = "MYR" };
        var bankAccount = new Account(_bankAccountId, _companyId, "1120", "Bank Account", AccountType.Asset) { Currency = "MYR" };

        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(company));
        _accountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<Account?>(bankAccount));

        var postingDate = new DateTime(2026, 6, 15);
        var fiscalYearId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, supplierId, "PINV-2026-005", postingDate)
        {
            SupplierInvoiceNumber = "BILL-SUPP-1234"
        };
        _purchaseInvoiceRepo.FindAsync(pi.Id).Returns(Task.FromResult<PurchaseInvoice?>(pi));

        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceType = "PurchaseInvoice",
            ReferenceId = pi.Id
        };
        je.AddLineWithDimensions(_otherAccountId, 450m, true, description: "Payable debit");
        je.AddLineWithDimensions(_bankAccountId, 450m, false, description: "Bank payment");
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
        entry.DocumentType.ShouldBe("PurchaseInvoice");
        entry.DocumentId.ShouldBe(je.Id);
        entry.DocumentNumber.ShouldBe("PINV-2026-005");
        entry.ReferenceNumber.ShouldBe("BILL-SUPP-1234");
        entry.Debit.ShouldBe(0m);
        entry.Credit.ShouldBe(450m);
    }

    [Fact]
    public async Task SetClearanceDateAsync_SalesInvoiceDocType_UpdatesLinkedJournalEntry()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var fiscalYearId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = invoiceId
        };
        je.AddLineWithDimensions(_bankAccountId, 200m, true, description: "Bank deposit");
        je.AddLineWithDimensions(_otherAccountId, 200m, false, description: "AR credit");
        je.Post();

        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je }.AsQueryable()));
        _journalRepo.UpdateAsync(je).Returns(Task.FromResult(je));

        var appService = CreateAppService();
        var clearanceDate = new DateTime(2026, 6, 18);

        var result = await appService.SetClearanceDateAsync(new SetClearanceDateDto
        {
            ClearanceDate = clearanceDate,
            Entries = new List<BankClearanceDocRefDto>
            {
                new() { DocumentType = "SalesInvoice", DocumentId = invoiceId }
            }
        });

        result.UpdatedCount.ShouldBe(1);
        je.ClearanceDate.ShouldBe(clearanceDate);
    }

    [Fact]
    public async Task SetClearanceDateAsync_PurchaseInvoiceDocType_UpdatesLinkedJournalEntry()
    {
        var postingDate = new DateTime(2026, 6, 15);
        var fiscalYearId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceType = "PurchaseInvoice",
            ReferenceId = invoiceId
        };
        je.AddLineWithDimensions(_otherAccountId, 350m, true, description: "AP debit");
        je.AddLineWithDimensions(_bankAccountId, 350m, false, description: "Bank payment");
        je.Post();

        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je }.AsQueryable()));
        _journalRepo.UpdateAsync(je).Returns(Task.FromResult(je));

        var appService = CreateAppService();
        var clearanceDate = new DateTime(2026, 6, 20);

        var result = await appService.SetClearanceDateAsync(new SetClearanceDateDto
        {
            ClearanceDate = clearanceDate,
            Entries = new List<BankClearanceDocRefDto>
            {
                new() { DocumentType = "PurchaseInvoice", DocumentId = invoiceId }
            }
        });

        result.UpdatedCount.ShouldBe(1);
        je.ClearanceDate.ShouldBe(clearanceDate);
    }

    [Fact]
    public async Task SetClearanceDateAsync_SalesInvoiceLinkedJeReconciled_ThrowsValidation()
    {
        // Parity with ERPNext commit 4890dc1c45 / PR #61468: guard bank clearance on reconciled vouchers
        var postingDate = new DateTime(2026, 6, 15);
        var fiscalYearId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();

        var je = new JournalEntry(Guid.NewGuid(), _companyId, fiscalYearId, postingDate)
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = invoiceId,
            EntryNumber = "POS-JE-001"
        };
        je.AddLineWithDimensions(_bankAccountId, 200m, true, description: "Bank deposit");
        je.AddLineWithDimensions(_otherAccountId, 200m, false, description: "Sales receivable");
        je.SetClearanceDate(new DateTime(2026, 6, 16));
        je.Post();

        _journalRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je }.AsQueryable()));

        var bt = new BankTransaction(
            Guid.NewGuid(), _companyId, _bankAccountId, new DateTime(2026, 6, 16),
            "POS deposit", 200m);
        bt.ReconcileWithJournalEntry(je.Id, "POS-JE-001");

        _bankTransactionRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BankTransaction> { bt }.AsQueryable()));

        var appService = CreateAppService();

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            appService.SetClearanceDateAsync(new SetClearanceDateDto
            {
                ClearanceDate = null, // attempting to clear clearance date while reconciled
                Entries = new List<BankClearanceDocRefDto>
                {
                    new() { DocumentType = "SalesInvoice", DocumentId = invoiceId }
                }
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain("Please unreconcile the voucher before changing the clearance date.");
    }
}