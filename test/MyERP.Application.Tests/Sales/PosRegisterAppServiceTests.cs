using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Unit tests for PosRegisterAppService per ERPNext PR #59990 / commits 06c74bc91d, 2929ee8904, 045e383982, 1ce22d620d:
/// - Includes POS Invoices and direct POS Sales Invoices, excluding consolidated invoices.
/// - Shows Paid Amount in flat/ungrouped list.
/// - When grouped by Payment Method, counts Grand Total once per invoice across payment rows.
/// - Validates date ranges and guards against filtering by the grouped dimension.
/// </summary>
public class PosRegisterAppServiceTests
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<Customer, Guid> _customerRepo = Substitute.For<IRepository<Customer, Guid>>();
    private readonly IRepository<PosProfile, Guid> _posProfileRepo = Substitute.For<IRepository<PosProfile, Guid>>();

    private readonly PosRegisterAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _posProfileId = Guid.NewGuid();
    private readonly Customer _customer;
    private readonly PosProfile _posProfile;

    private readonly DateTime _fromDate = new DateTime(2026, 1, 1);
    private readonly DateTime _toDate = new DateTime(2026, 12, 31);

    public PosRegisterAppServiceTests()
    {
        _customer = new Customer(_customerId, _companyId, "Walk-in Customer");
        _posProfile = new PosProfile(_posProfileId, _companyId, "Retail Counter", Guid.NewGuid());

        _customerRepo.GetListAsync(Arg.Any<Expression<Func<Customer, bool>>>())
            .Returns(Task.FromResult(new List<Customer> { _customer }));

        _posProfileRepo.GetListAsync(Arg.Any<Expression<Func<PosProfile, bool>>>())
            .Returns(Task.FromResult(new List<PosProfile> { _posProfile }));

        _appService = new PosRegisterAppService(
            _invoiceRepo,
            _customerRepo,
            _posProfileRepo);
    }

    [Fact]
    public async Task GetReportAsync_FlatList_IncludesPosInvoices_AndExcludesConsolidatedInvoices()
    {
        // 1. Direct POS Invoice: GrandTotal = 500, AmountPaid = 500
        var directPos = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "POS-001", new DateTime(2026, 5, 10))
        {
            IsPos = true,
            IsConsolidated = false,
            PosProfileId = _posProfileId,
            AmountPaid = 500m
        };
        directPos.AddItem(Guid.NewGuid(), "Item 1", 1, 500m, 0m);
        directPos.Submit();
        directPos.Post();

        // 2. Consolidated Sales Invoice from POS Closing: GrandTotal = 1500 (must be excluded)
        var consolidated = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "SINV-CONSOL-001", new DateTime(2026, 5, 10))
        {
            IsPos = true,
            IsConsolidated = true,
            PosProfileId = _posProfileId,
            AmountPaid = 1500m
        };
        consolidated.AddItem(Guid.NewGuid(), "Item 2", 1, 1500m, 0m);
        consolidated.Submit();
        consolidated.Post();

        var invoices = new List<SalesInvoice> { directPos, consolidated };
        _invoiceRepo.GetQueryableAsync()
            .Returns(Task.FromResult(invoices.AsQueryable()));

        var request = new PosRegisterRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate
        };

        var report = await _appService.GetReportAsync(request);

        // Only direct POS invoice is listed (PR #59990)
        report.Rows.Count.ShouldBe(1);
        var row = report.Rows[0];
        row.InvoiceNumber.ShouldBe("POS-001");
        row.GrandTotal.ShouldBe(500m);
        row.PaidAmount.ShouldBe(500m); // Paid amount shown when not grouped (commit 2929ee8904)
        report.TotalGrandTotal.ShouldBe(500m);
        report.TotalPaidAmount.ShouldBe(500m);
    }

    [Fact]
    public async Task GetReportAsync_PaymentMethodGrouping_CountsGrandTotalOncePerInvoice()
    {
        var invId = Guid.NewGuid();
        var invoice = new SalesInvoice(invId, _companyId, _customerId, "POS-SPLIT-001", new DateTime(2026, 6, 1))
        {
            IsPos = true,
            IsConsolidated = false,
            PosProfileId = _posProfileId,
            AmountPaid = 1000m,
            Notes = "Cash"
        };
        invoice.AddItem(Guid.NewGuid(), "Item 1", 1, 1000m, 0m);
        invoice.Submit();
        invoice.Post();

        var invoices = new List<SalesInvoice> { invoice };
        _invoiceRepo.GetQueryableAsync()
            .Returns(Task.FromResult(invoices.AsQueryable()));

        var request = new PosRegisterRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate,
            GroupBy = "Payment Method"
        };

        var report = await _appService.GetReportAsync(request);

        // Group rows + subtotal row
        report.Rows.Count.ShouldBe(2);
        var detailRow = report.Rows[0];
        detailRow.GrandTotal.ShouldBe(1000m);
        detailRow.PaidAmount.ShouldBe(1000m);
        detailRow.IsSubtotal.ShouldBeFalse();

        var subtotalRow = report.Rows[1];
        subtotalRow.IsSubtotal.ShouldBeTrue();
        subtotalRow.GrandTotal.ShouldBe(1000m);
        subtotalRow.PaidAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task GetReportAsync_CashierFilter_FiltersByCashierId()
    {
        var cashierA = Guid.NewGuid();
        var cashierB = Guid.NewGuid();

        var invA = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "POS-CASHIER-A", new DateTime(2026, 7, 1))
        {
            IsPos = true,
            IsConsolidated = false,
            AmountPaid = 300m,
        };
        invA.AddItem(Guid.NewGuid(), "Item A", 1, 300m, 0m);
        invA.CreatorId = cashierA;
        invA.Submit();
        invA.Post();

        var invB = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "POS-CASHIER-B", new DateTime(2026, 7, 1))
        {
            IsPos = true,
            IsConsolidated = false,
            AmountPaid = 700m,
        };
        invB.AddItem(Guid.NewGuid(), "Item B", 1, 700m, 0m);
        invB.CreatorId = cashierB;
        invB.Submit();
        invB.Post();

        var invoices = new List<SalesInvoice> { invA, invB };
        _invoiceRepo.GetQueryableAsync()
            .Returns(Task.FromResult(invoices.AsQueryable()));

        var request = new PosRegisterRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate,
            CashierId = cashierA
        };

        var report = await _appService.GetReportAsync(request);

        report.Rows.Count.ShouldBe(1);
        report.Rows[0].InvoiceNumber.ShouldBe("POS-CASHIER-A");
        report.TotalGrandTotal.ShouldBe(300m);
    }

    [Fact]
    public async Task GetReportAsync_InvalidFilters_ThrowsBusinessException()
    {
        // 1. FromDate > ToDate throws DateRangeInvalid
        var invalidDates = new PosRegisterRequestDto
        {
            CompanyId = _companyId,
            FromDate = new DateTime(2026, 12, 31),
            ToDate = new DateTime(2026, 1, 1)
        };
        await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(invalidDates));

        // 2. Cannot filter based on POS Profile if grouped by POS Profile
        var conflictGroup = new PosRegisterRequestDto
        {
            CompanyId = _companyId,
            FromDate = _fromDate,
            ToDate = _toDate,
            PosProfileId = _posProfileId,
            GroupBy = "POS Profile"
        };
        await Should.ThrowAsync<BusinessException>(() => _appService.GetReportAsync(conflictGroup));
    }
}
