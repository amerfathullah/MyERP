using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Accounting;

/// <summary>
/// Tests for PaymentReconciliationAppService GetLinkedAllocationsAsync and UnreconcileAllocationsAsync.
/// Per ERPNext PR #59677 (commit dbcb87fae8) / PR #59517:
/// unreconcile dialog fetches linked allocations and unreconciles selected entries.
/// </summary>
public class PaymentUnreconcileAppServiceTests
{
    private readonly IRepository<FiscalYear, Guid> _fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();

    private readonly PaymentLedgerService _pleService;
    private readonly PaymentReconciliationEngine _engine;

    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();

    private readonly PaymentReconciliationAppService _appService;

    public PaymentUnreconcileAppServiceTests()
    {
        _pleService = new PaymentLedgerService(_pleRepo);
        _engine = new PaymentReconciliationEngine(
            _pleService,
            _pleRepo,
            _siRepo,
            _piRepo,
            _peRepo,
            _jeRepo,
            _fyRepo,
            _companyRepo);

        _appService = new PaymentReconciliationAppService(
            _engine,
            _pleService,
            _pleRepo,
            _siRepo,
            _piRepo,
            _peRepo,
            _jeRepo,
            _companyRepo);
    }

    [Fact]
    public async Task GetLinkedAllocationsAsync_ForPaymentEntry_ReturnsLinkedSalesInvoices()
    {
        var companyId = Guid.NewGuid();
        var peId = Guid.NewGuid();
        var siId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow.Date, 500m, accountId, accountId)
        {
            PaymentNumber = "PE-001"
        };
        _peRepo.FindAsync(peId).Returns(Task.FromResult<PaymentEntry?>(pe));

        var si = new SalesInvoice(siId, companyId, partyId, "SINV-001", DateTime.UtcNow.Date);
        _siRepo.FindAsync(siId).Returns(Task.FromResult<SalesInvoice?>(si));

        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), companyId, DateTime.UtcNow.Date,
            accountId, "Customer", partyId,
            "PaymentEntry", peId,
            "SalesInvoice", siId,
            -200m, -200m, "MYR");

        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry> { ple }.AsQueryable()));

        var allocations = await _appService.GetLinkedAllocationsAsync("PaymentEntry", peId);

        allocations.Count.ShouldBe(1);
        var alloc = allocations[0];
        alloc.PaymentVoucherType.ShouldBe("PaymentEntry");
        alloc.PaymentVoucherId.ShouldBe(peId);
        alloc.PaymentVoucherNumber.ShouldBe("PE-001");
        alloc.InvoiceVoucherType.ShouldBe("SalesInvoice");
        alloc.InvoiceVoucherId.ShouldBe(siId);
        alloc.InvoiceVoucherNumber.ShouldBe("SINV-001");
        alloc.AllocatedAmount.ShouldBe(200m);
    }

    [Fact]
    public async Task GetLinkedAllocationsAsync_ForSalesInvoice_ReturnsLinkedPaymentEntries()
    {
        var companyId = Guid.NewGuid();
        var peId = Guid.NewGuid();
        var siId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow.Date, 500m, accountId, accountId)
        {
            PaymentNumber = "PE-002"
        };
        _peRepo.FindAsync(peId).Returns(Task.FromResult<PaymentEntry?>(pe));

        var si = new SalesInvoice(siId, companyId, partyId, "SINV-002", DateTime.UtcNow.Date);
        _siRepo.FindAsync(siId).Returns(Task.FromResult<SalesInvoice?>(si));

        var ple = new PaymentLedgerEntry(
            Guid.NewGuid(), companyId, DateTime.UtcNow.Date,
            accountId, "Customer", partyId,
            "PaymentEntry", peId,
            "SalesInvoice", siId,
            -350m, -350m, "MYR");

        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry> { ple }.AsQueryable()));

        var allocations = await _appService.GetLinkedAllocationsAsync("SalesInvoice", siId);

        allocations.Count.ShouldBe(1);
        var alloc = allocations[0];
        alloc.PaymentVoucherType.ShouldBe("PaymentEntry");
        alloc.PaymentVoucherId.ShouldBe(peId);
        alloc.PaymentVoucherNumber.ShouldBe("PE-002");
        alloc.InvoiceVoucherType.ShouldBe("SalesInvoice");
        alloc.InvoiceVoucherId.ShouldBe(siId);
        alloc.InvoiceVoucherNumber.ShouldBe("SINV-002");
        alloc.AllocatedAmount.ShouldBe(350m);
    }

    [Fact]
    public async Task GetLinkedAllocationsAsync_NoAllocations_ReturnsEmpty()
    {
        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry>().AsQueryable()));

        var allocations = await _appService.GetLinkedAllocationsAsync("PaymentEntry", Guid.NewGuid());

        allocations.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnreconcileAllocationsAsync_Batch_UnreconcilesAllSelected()
    {
        var peId = Guid.NewGuid();
        var siId1 = Guid.NewGuid();
        var siId2 = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var si1 = new SalesInvoice(siId1, companyId, Guid.NewGuid(), "SINV-001", DateTime.UtcNow.Date) { AmountPaid = 200m };
        var si2 = new SalesInvoice(siId2, companyId, Guid.NewGuid(), "SINV-002", DateTime.UtcNow.Date) { AmountPaid = 300m };
        _siRepo.GetAsync(siId1).Returns(Task.FromResult(si1));
        _siRepo.GetAsync(siId2).Returns(Task.FromResult(si2));

        var ple1 = new PaymentLedgerEntry(Guid.NewGuid(), companyId, DateTime.UtcNow.Date, accountId, "Customer", Guid.NewGuid(), "PaymentEntry", peId, "SalesInvoice", siId1, -200m, -200m, "MYR");
        var ple2 = new PaymentLedgerEntry(Guid.NewGuid(), companyId, DateTime.UtcNow.Date, accountId, "Customer", Guid.NewGuid(), "PaymentEntry", peId, "SalesInvoice", siId2, -300m, -300m, "MYR");

        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry> { ple1, ple2 }.AsQueryable()));
        _jeRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));

        var inputs = new List<UnreconcileDto>
        {
            new() { PaymentVoucherType = "PaymentEntry", PaymentVoucherId = peId, InvoiceVoucherType = "SalesInvoice", InvoiceVoucherId = siId1 },
            new() { PaymentVoucherType = "PaymentEntry", PaymentVoucherId = peId, InvoiceVoucherType = "SalesInvoice", InvoiceVoucherId = siId2 },
        };

        await _appService.UnreconcileAllocationsAsync(inputs);

        ple1.Delinked.ShouldBeTrue();
        ple2.Delinked.ShouldBeTrue();
        si1.AmountPaid.ShouldBe(0m);
        si2.AmountPaid.ShouldBe(0m);
    }
}
