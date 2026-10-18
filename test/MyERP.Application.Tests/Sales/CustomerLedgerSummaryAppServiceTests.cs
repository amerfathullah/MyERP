using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class CustomerLedgerSummaryAppServiceTests
{
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId1 = Guid.NewGuid();
    private readonly Guid _customerId2 = Guid.NewGuid();

    private readonly Customer _customer1;
    private readonly Customer _customer2;
    private readonly Company _company;

    public CustomerLedgerSummaryAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Trading") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _customer1 = new Customer(_customerId1, _companyId, "Star Logistics") { CustomerCode = "CUST-SL" };
        _customer2 = new Customer(_customerId2, _companyId, "Prime Retail") { CustomerCode = "CUST-PR" };

        _customerRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Customer> { _customer1, _customer2 }.AsQueryable()));
        _journalRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, object>>[]>())
            .Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_OpeningAndInPeriodTransactions_CalculatesSummaryCorrectly()
    {
        // Customer 1:
        // Prior invoice: 10 Dec 2025, 1000 MYR
        var invPrior = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-001", new DateTime(2025, 12, 10));
        invPrior.AddItem(Guid.NewGuid(), "Consulting Services", 1m, 1000m, 0m);
        invPrior.Submit();

        // In-period invoice: 15 Jan 2026, 2500 MYR
        var invPeriod = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-002", new DateTime(2026, 1, 15));
        invPeriod.AddItem(Guid.NewGuid(), "Maintenance Services", 1m, 2500m, 0m);
        invPeriod.Submit();

        // In-period payment received: 20 Jan 2026, 1500 MYR
        var payPeriod = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Receive,
            new DateTime(2026, 1, 20),
            1500m,
            Guid.NewGuid(),
            Guid.NewGuid())
        {
            PartyType = "Customer",
            PartyId = _customerId1,
            ExchangeRate = 1m
        };
        payPeriod.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { invPrior, invPeriod }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry> { payPeriod }.AsQueryable()));

        var appService = new CustomerLedgerSummaryAppService(
            _customerRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new CustomerLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.CustomerCode.ShouldBe("CUST-SL");
        row.OpeningBalance.ShouldBe(1000m);
        row.InvoicedAmount.ShouldBe(2500m);
        row.PaidAmount.ShouldBe(1500m);
        row.ReturnAmount.ShouldBe(0m);
        // Closing = 1000 + 2500 - 1500 = 2000
        row.ClosingBalance.ShouldBe(2000m);
        row.DrOrCr.ShouldBe("Dr");

        result.TotalRow.ClosingBalance.ShouldBe(2000m);
        result.TotalRow.DrOrCr.ShouldBe("Dr");
    }

    [Fact]
    public async Task GetReportAsync_WithReturnsAndCreditBalance_CalculatesCrCorrectly()
    {
        // Customer 2:
        // In-period invoice: 10 Jan 2026, 500 MYR
        var inv = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId2, "SINV-003", new DateTime(2026, 1, 10));
        inv.AddItem(Guid.NewGuid(), "Hardware", 1m, 500m, 0m);
        inv.Submit();

        // In-period return (credit note): 12 Jan 2026, 200 MYR
        var ret = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId2, "SRET-001", new DateTime(2026, 1, 12))
        {
            IsReturn = true
        };
        ret.AddItem(Guid.NewGuid(), "Hardware Return", -1m, 200m, 0m);
        ret.Submit();

        // Customer overpaid: 800 MYR payment received
        var pay = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Receive,
            new DateTime(2026, 1, 15),
            800m,
            Guid.NewGuid(),
            Guid.NewGuid())
        {
            PartyType = "Customer",
            PartyId = _customerId2,
            ExchangeRate = 1m
        };
        pay.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv, ret }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry> { pay }.AsQueryable()));

        var appService = new CustomerLedgerSummaryAppService(
            _customerRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new CustomerLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.CustomerCode.ShouldBe("CUST-PR");
        row.InvoicedAmount.ShouldBe(500m);
        row.ReturnAmount.ShouldBe(200m);
        row.PaidAmount.ShouldBe(800m);
        // Closing = 0 + 500 - 800 - 200 = -500 (Advance / Overpayment)
        row.ClosingBalance.ShouldBe(-500m);
        row.DrOrCr.ShouldBe("Cr");
    }

    [Fact]
    public async Task GetReportAsync_CustomerIdFilter_RestrictsResults()
    {
        var inv1 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId1, "SINV-004", new DateTime(2026, 1, 10));
        inv1.AddItem(Guid.NewGuid(), "Service A", 1m, 100m, 0m);
        inv1.Submit();

        var inv2 = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId2, "SINV-005", new DateTime(2026, 1, 10));
        inv2.AddItem(Guid.NewGuid(), "Service B", 1m, 200m, 0m);
        inv2.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { inv1, inv2 }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));

        var appService = new CustomerLedgerSummaryAppService(
            _customerRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act: filter for Customer 1 only
        var result = await appService.GetReportAsync(new CustomerLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            CustomerId = _customerId1,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].CustomerId.ShouldBe(_customerId1);
    }
}
