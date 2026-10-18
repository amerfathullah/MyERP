using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Purchasing;

public class SupplierLedgerSummaryAppServiceTests
{
    private readonly IRepository<Supplier, Guid> _supplierRepo = Substitute.For<IRepository<Supplier, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _paymentRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _journalRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId1 = Guid.NewGuid();
    private readonly Guid _supplierId2 = Guid.NewGuid();

    private readonly Supplier _supplier1;
    private readonly Supplier _supplier2;
    private readonly Company _company;

    public SupplierLedgerSummaryAppServiceTests()
    {
        _company = new Company(_companyId, "Acme Trading") { CurrencyCode = "MYR" };
        _companyRepo.FindAsync(_companyId).Returns(Task.FromResult<Company?>(_company));

        _supplier1 = new Supplier(_supplierId1, _companyId, "Apex Materials") { SupplierCode = "SUPP-APEX" };
        _supplier2 = new Supplier(_supplierId2, _companyId, "Global Supplies") { SupplierCode = "SUPP-GLOB" };

        _supplierRepo.GetQueryableAsync().Returns(Task.FromResult(new List<Supplier> { _supplier1, _supplier2 }.AsQueryable()));
        _journalRepo.WithDetailsAsync(Arg.Any<System.Linq.Expressions.Expression<Func<JournalEntry, object>>[]>())
            .Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));
    }

    [Fact]
    public async Task GetReportAsync_OpeningAndInPeriodTransactions_CalculatesSummaryCorrectly()
    {
        // Supplier 1:
        // Prior invoice: 10 Dec 2025, 1200 MYR
        var invPrior = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-001", new DateTime(2025, 12, 10));
        invPrior.AddItem(Guid.NewGuid(), "Raw Steel", 1m, 1200m, 0m);
        invPrior.Submit();

        // In-period invoice: 15 Jan 2026, 3000 MYR
        var invPeriod = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-002", new DateTime(2026, 1, 15));
        invPeriod.AddItem(Guid.NewGuid(), "Sheet Metal", 1m, 3000m, 0m);
        invPeriod.Submit();

        // In-period payment paid: 20 Jan 2026, 2000 MYR
        var payPeriod = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Pay,
            new DateTime(2026, 1, 20),
            2000m,
            Guid.NewGuid(),
            Guid.NewGuid())
        {
            PartyType = "Supplier",
            PartyId = _supplierId1,
            ExchangeRate = 1m
        };
        payPeriod.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { invPrior, invPeriod }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry> { payPeriod }.AsQueryable()));

        var appService = new SupplierLedgerSummaryAppService(
            _supplierRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SupplierLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.ShouldNotBeNull();
        result.Rows.Count.ShouldBe(1);

        var row = result.Rows[0];
        row.SupplierCode.ShouldBe("SUPP-APEX");
        row.OpeningBalance.ShouldBe(1200m);
        row.InvoicedAmount.ShouldBe(3000m);
        row.PaidAmount.ShouldBe(2000m);
        row.ReturnAmount.ShouldBe(0m);
        // Closing = 1200 + 3000 - 2000 = 2200
        row.ClosingBalance.ShouldBe(2200m);
        row.DrOrCr.ShouldBe("Cr");

        result.TotalRow.ClosingBalance.ShouldBe(2200m);
        result.TotalRow.DrOrCr.ShouldBe("Cr");
    }

    [Fact]
    public async Task GetReportAsync_WithDebitNotesAndPrepayment_CalculatesDrCorrectly()
    {
        // Supplier 2:
        // In-period invoice: 10 Jan 2026, 600 MYR
        var inv = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId2, "PINV-003", new DateTime(2026, 1, 10));
        inv.AddItem(Guid.NewGuid(), "Electronics", 1m, 600m, 0m);
        inv.Submit();

        // In-period return (debit note): 12 Jan 2026, 200 MYR
        var ret = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId2, "PRET-001", new DateTime(2026, 1, 12))
        {
            IsReturn = true
        };
        ret.AddItem(Guid.NewGuid(), "Defective Parts", -1m, 200m, 0m);
        ret.Submit();

        // Company overpaid / advance: 800 MYR paid
        var pay = new PaymentEntry(
            Guid.NewGuid(),
            _companyId,
            PaymentType.Pay,
            new DateTime(2026, 1, 15),
            800m,
            Guid.NewGuid(),
            Guid.NewGuid())
        {
            PartyType = "Supplier",
            PartyId = _supplierId2,
            ExchangeRate = 1m
        };
        pay.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { inv, ret }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry> { pay }.AsQueryable()));

        var appService = new SupplierLedgerSummaryAppService(
            _supplierRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act
        var result = await appService.GetReportAsync(new SupplierLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        var row = result.Rows[0];
        row.SupplierCode.ShouldBe("SUPP-GLOB");
        row.InvoicedAmount.ShouldBe(600m);
        row.ReturnAmount.ShouldBe(200m);
        row.PaidAmount.ShouldBe(800m);
        // Closing = 0 + 600 - 800 - 200 = -400 (Advance to supplier / debit balance)
        row.ClosingBalance.ShouldBe(-400m);
        row.DrOrCr.ShouldBe("Dr");
    }

    [Fact]
    public async Task GetReportAsync_SupplierIdFilter_RestrictsResults()
    {
        var inv1 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId1, "PINV-004", new DateTime(2026, 1, 10));
        inv1.AddItem(Guid.NewGuid(), "Items 1", 1m, 150m, 0m);
        inv1.Submit();

        var inv2 = new PurchaseInvoice(Guid.NewGuid(), _companyId, _supplierId2, "PINV-005", new DateTime(2026, 1, 10));
        inv2.AddItem(Guid.NewGuid(), "Items 2", 1m, 350m, 0m);
        inv2.Submit();

        _invoiceRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PurchaseInvoice> { inv1, inv2 }.AsQueryable()));
        _paymentRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry>().AsQueryable()));

        var appService = new SupplierLedgerSummaryAppService(
            _supplierRepo, _invoiceRepo, _paymentRepo, _journalRepo, _companyRepo);

        // Act: filter for Supplier 1 only
        var result = await appService.GetReportAsync(new SupplierLedgerSummaryRequestDto
        {
            CompanyId = _companyId,
            SupplierId = _supplierId1,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        // Assert
        result.Rows.Count.ShouldBe(1);
        result.Rows[0].SupplierId.ShouldBe(_supplierId1);
    }
}
