using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using MyERP.Shared;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for payment deductions in register ledger balance &amp; statement opening balance calculations.
/// Verifies ERPNext PR #58437 (commit dbe153a15e).
/// </summary>
public class StatementOfAccountsPaymentDeductionTests
{
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();

    private readonly StatementOfAccountsAppService _soaAppService;
    private readonly SalesRegisterAppService _salesRegisterAppService;
    private readonly PurchaseRegisterAppService _purchaseRegisterAppService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _partyAccountId = Guid.NewGuid();

    public StatementOfAccountsPaymentDeductionTests()
    {
        _soaAppService = new StatementOfAccountsAppService(
            _siRepo, _piRepo, _peRepo, _customerRepo, _supplierRepo);

        _salesRegisterAppService = new SalesRegisterAppService(
            _siRepo, _peRepo);

        _purchaseRegisterAppService = new PurchaseRegisterAppService(
            _piRepo, _peRepo);
    }

    private void SetupPaymentRepo(List<PaymentEntry> payments)
    {
        _peRepo.GetQueryableAsync().Returns(Task.FromResult(payments.AsQueryable()));
        _peRepo.WithDetailsAsync(Arg.Any<Expression<Func<PaymentEntry, object>>[]>())
            .Returns(Task.FromResult(payments.AsQueryable()));
    }

    [Fact]
    public async Task CustomerStatement_WithPaymentDeductions_SettlesInvoiceAndShowsAccurateBalance()
    {
        var fromDate = new DateTime(2026, 7, 1);
        var toDate = new DateTime(2026, 7, 31);

        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-001", new DateTime(2026, 7, 5));
        si.AddItem(Guid.NewGuid(), "Product A", 1m, 1000m, 0m);
        si.Submit();
        si.Post();
        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        // Paid 950 cash + 50 withholding tax deduction = 1000 settled
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, new DateTime(2026, 7, 15), 950m, _partyAccountId, _bankAccountId)
        {
            PartyType = "Customer",
            PartyId = _customerId
        };
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 50m,
            BaseTaxAmount = 50m,
            AddDeductTax = TaxAddDeduct.Deduct,
            IncludedInPaidAmount = true
        };
        pe.AddTax(tax);
        pe.Submit();
        pe.Post();

        SetupPaymentRepo(new List<PaymentEntry> { pe });

        var stmt = await _soaAppService.GetCustomerStatementAsync(_customerId, _companyId, fromDate, toDate);

        Assert.NotNull(stmt);
        Assert.Equal(0m, stmt.OpeningBalance);
        Assert.Equal(1000m, stmt.TotalDebit);
        Assert.Equal(1000m, stmt.TotalCredit); // 950 + 50 deduction settled
        Assert.Equal(0m, stmt.ClosingBalance);
        Assert.Equal(2, stmt.Entries.Count);

        var invEntry = stmt.Entries[0];
        Assert.Equal(1000m, invEntry.DebitAmount);
        Assert.Equal(0m, invEntry.CreditAmount);
        Assert.Equal(1000m, invEntry.RunningBalance);

        var payEntry = stmt.Entries[1];
        Assert.Equal(0m, payEntry.DebitAmount);
        Assert.Equal(1000m, payEntry.CreditAmount); // settled amount includes deduction
        Assert.Equal(0m, payEntry.RunningBalance);
    }

    [Fact]
    public async Task CustomerStatement_OpeningBalance_ExcludesPeriodPaymentsFromPriorInvoices()
    {
        var fromDate = new DateTime(2026, 6, 1);
        var toDate = new DateTime(2026, 6, 30);

        // Prior invoice in May, paid during statement period (June)
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-PRIOR", new DateTime(2026, 5, 15));
        si.AddItem(Guid.NewGuid(), "Prior Service", 1m, 1000m, 0m);
        si.Submit();
        si.Post();
        // In live DB, payment allocation mutates AmountPaid on invoice
        si.AmountPaid = 1000m;
        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));

        // Payment made on June 10
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, new DateTime(2026, 6, 10), 1000m, _partyAccountId, _bankAccountId)
        {
            PartyType = "Customer",
            PartyId = _customerId
        };
        pe.Submit();
        pe.Post();

        SetupPaymentRepo(new List<PaymentEntry> { pe });

        var stmt = await _soaAppService.GetCustomerStatementAsync(_customerId, _companyId, fromDate, toDate);

        Assert.NotNull(stmt);
        // Opening balance must be 1000m (prior invoice not paid before June 1)
        Assert.Equal(1000m, stmt.OpeningBalance);
        Assert.Equal(1000m, stmt.TotalCredit);
        Assert.Equal(0m, stmt.ClosingBalance);
        Assert.Single(stmt.Entries);
        Assert.Equal(1000m, stmt.Entries[0].CreditAmount);
        Assert.Equal(0m, stmt.Entries[0].RunningBalance);
    }

    [Fact]
    public async Task SupplierStatement_WithPaymentDeductions_PopulatesTotalDebitCreditAndSettles()
    {
        var fromDate = new DateTime(2026, 7, 1);
        var toDate = new DateTime(2026, 7, 31);

        var pi = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "BILL-2026-001", new DateTime(2026, 7, 5));
        pi.AddItem(Guid.NewGuid(), "Raw Materials", 1m, 2000m, 0m);
        pi.Submit();
        pi.Post();
        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { pi }.AsQueryable()));

        // Paid 1900 + 100 deduction = 2000 settled
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Pay, new DateTime(2026, 7, 12), 1900m, _bankAccountId, _partyAccountId)
        {
            PartyType = "Supplier",
            PartyId = _supplierId
        };
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 100m,
            BaseTaxAmount = 100m,
            AddDeductTax = TaxAddDeduct.Deduct,
            IncludedInPaidAmount = true
        };
        pe.AddTax(tax);
        pe.Submit();
        pe.Post();

        SetupPaymentRepo(new List<PaymentEntry> { pe });

        var stmt = await _soaAppService.GetSupplierStatementAsync(_supplierId, _companyId, fromDate, toDate);

        Assert.NotNull(stmt);
        Assert.Equal(0m, stmt.OpeningBalance);
        Assert.Equal(2000m, stmt.TotalInvoiced);
        Assert.Equal(2000m, stmt.TotalPaid);
        Assert.Equal(2000m, stmt.TotalDebit);
        Assert.Equal(2000m, stmt.TotalCredit);
        Assert.Equal(0m, stmt.ClosingBalance);
        Assert.Equal(2, stmt.Entries.Count);

        var billEntry = stmt.Entries[0];
        Assert.Equal(0m, billEntry.DebitAmount);
        Assert.Equal(2000m, billEntry.CreditAmount);
        Assert.Equal(2000m, billEntry.RunningBalance);

        var payEntry = stmt.Entries[1];
        Assert.Equal(2000m, payEntry.DebitAmount); // settled amount includes deduction
        Assert.Equal(0m, payEntry.CreditAmount);
        Assert.Equal(0m, payEntry.RunningBalance);
    }

    [Fact]
    public async Task SalesRegister_WithIncludePayments_IncludesDeductionsAndRunningBalance()
    {
        var fromDate = new DateTime(2026, 7, 1);
        var toDate = new DateTime(2026, 7, 31);

        // Prior SI in June: 500m
        var siPrior = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-JUNE", new DateTime(2026, 6, 20));
        siPrior.AddItem(Guid.NewGuid(), "Item Prior", 1m, 500m, 0m);
        siPrior.Submit();
        siPrior.Post();

        // Period SI in July: 1000m
        var siPeriod = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-JULY", new DateTime(2026, 7, 5));
        siPeriod.AddItem(Guid.NewGuid(), "Item Period", 1m, 1000m, 0m);
        siPeriod.Submit();
        siPeriod.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { siPrior, siPeriod }.AsQueryable()));

        // Period PE: 950m paid + 50m deduction = 1000m settled
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Receive, new DateTime(2026, 7, 15), 950m, _partyAccountId, _bankAccountId)
        {
            PartyType = "Customer",
            PartyId = _customerId
        };
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 50m,
            BaseTaxAmount = 50m,
            AddDeductTax = TaxAddDeduct.Deduct,
            IncludedInPaidAmount = true
        };
        pe.AddTax(tax);
        pe.Submit();
        pe.Post();

        SetupPaymentRepo(new List<PaymentEntry> { pe });

        var filter = new RegisterFilterDto
        {
            CompanyId = _companyId,
            CustomerId = _customerId,
            FromDate = fromDate,
            ToDate = toDate,
            IncludePayments = true
        };

        var result = await _salesRegisterAppService.GetReportAsync(filter);

        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);

        var openingLine = result.Items[0];
        Assert.Equal("Opening", openingLine.VoucherType);
        Assert.Equal(500m, openingLine.Debit);
        Assert.Equal(0m, openingLine.Credit);
        Assert.Equal(500m, openingLine.Balance);

        var line1 = result.Items[1];
        Assert.Equal("Sales Invoice", line1.VoucherType);
        Assert.Equal(1000m, line1.Debit);
        Assert.Equal(0m, line1.Credit);
        Assert.Equal(1500m, line1.Balance); // 500 opening + 1000 invoice

        var line2 = result.Items[2];
        Assert.Equal("Payment Entry", line2.VoucherType);
        Assert.Equal(0m, line2.Debit);
        Assert.Equal(1000m, line2.Credit); // settled amount with deduction
        Assert.Equal(500m, line2.Balance); // 1500 - 1000
    }

    [Fact]
    public async Task PurchaseRegister_WithIncludePayments_IncludesDeductionsAndRunningBalance()
    {
        var fromDate = new DateTime(2026, 7, 1);
        var toDate = new DateTime(2026, 7, 31);

        // Prior PI in June: 800m
        var piPrior = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "BILL-2026-JUNE", new DateTime(2026, 6, 15));
        piPrior.AddItem(Guid.NewGuid(), "Item Prior", 1m, 800m, 0m);
        piPrior.Submit();
        piPrior.Post();

        // Period PI in July: 1200m
        var piPeriod = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId, "BILL-2026-JULY", new DateTime(2026, 7, 5));
        piPeriod.AddItem(Guid.NewGuid(), "Item Period", 1m, 1200m, 0m);
        piPeriod.Submit();
        piPeriod.Post();

        _piRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { piPrior, piPeriod }.AsQueryable()));

        // Period PE: 1150m paid + 50m deduction = 1200m settled
        var pe = new PaymentEntry(Guid.NewGuid(), _companyId, PaymentType.Pay, new DateTime(2026, 7, 20), 1150m, _bankAccountId, _partyAccountId)
        {
            PartyType = "Supplier",
            PartyId = _supplierId
        };
        var tax = new PaymentEntryTax(Guid.NewGuid(), pe.Id, Guid.NewGuid())
        {
            ChargeType = PaymentTaxChargeType.Actual,
            TaxAmount = 50m,
            BaseTaxAmount = 50m,
            AddDeductTax = TaxAddDeduct.Deduct,
            IncludedInPaidAmount = true
        };
        pe.AddTax(tax);
        pe.Submit();
        pe.Post();

        SetupPaymentRepo(new List<PaymentEntry> { pe });

        var filter = new RegisterFilterDto
        {
            CompanyId = _companyId,
            SupplierId = _supplierId,
            FromDate = fromDate,
            ToDate = toDate,
            IncludePayments = true
        };

        var result = await _purchaseRegisterAppService.GetReportAsync(filter);

        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);

        var openingLine = result.Items[0];
        Assert.Equal("Opening", openingLine.VoucherType);
        Assert.Equal(0m, openingLine.Debit);
        Assert.Equal(800m, openingLine.Credit);
        Assert.Equal(800m, openingLine.Balance);

        var line1 = result.Items[1];
        Assert.Equal("Purchase Invoice", line1.VoucherType);
        Assert.Equal(0m, line1.Debit);
        Assert.Equal(1200m, line1.Credit);
        Assert.Equal(2000m, line1.Balance); // 800 opening + 1200 bill

        var line2 = result.Items[2];
        Assert.Equal("Payment Entry", line2.VoucherType);
        Assert.Equal(1200m, line2.Debit); // settled amount with deduction
        Assert.Equal(0m, line2.Credit);
        Assert.Equal(800m, line2.Balance); // 2000 - 1200
    }
}
