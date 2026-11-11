using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Unit tests for Process Deferred Accounting preview, calculation, and summary metrics.
/// Verifies rules from erpnext/accounts/doctype/process_deferred_accounting (#5995).
/// </summary>
public class ProcessDeferredAccountingWorkflowTests
{
    private readonly IRepository<ProcessDeferredAccounting, Guid> _pdaRepo = Substitute.For<IRepository<ProcessDeferredAccounting, Guid>>();
    private readonly IRepository<Company, Guid> _companyRepo = Substitute.For<IRepository<Company, Guid>>();
    private readonly IRepository<Account, Guid> _accountRepo = Substitute.For<IRepository<Account, Guid>>();
    private readonly IRepository<SalesInvoice, Guid> _siRepo = Substitute.For<IRepository<SalesInvoice, Guid>>();
    private readonly IRepository<PurchaseInvoice, Guid> _piRepo = Substitute.For<IRepository<PurchaseInvoice, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<FiscalYear, Guid> _fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();

    private readonly DeferredAccountingService _deferredService;
    private readonly ProcessDeferredAccountingAppService _appService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _deferredAccountId = Guid.NewGuid();

    public ProcessDeferredAccountingWorkflowTests()
    {
        _deferredService = new DeferredAccountingService(
            _jeRepo, _siRepo, _piRepo, _fyRepo, _companyRepo);

        _appService = new ProcessDeferredAccountingAppService(
            _pdaRepo, _companyRepo, _accountRepo, _siRepo, _piRepo, _jeRepo, _deferredService);
    }

    [Fact]
    public async Task PreviewDeferredAccountingAsync_Income_ReturnsPreviewItemsAndTotals()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-0001", new DateTime(2026, 1, 1));
        si.AddItem(Guid.NewGuid(), "Annual Software Subscription", 1m, 1200m, 0m);
        var item = si.Items.First();
        item.EnableDeferredRevenue = true;
        item.DeferredRevenueAccountId = _deferredAccountId;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        si.Submit();
        si.Post();

        var salesInvoices = new List<SalesInvoice> { si };
        _siRepo.GetQueryableAsync().Returns(Task.FromResult(salesInvoices.AsQueryable()));

        // The invoice's own GL: the deferred row sits in the deferred revenue account. Only a row
        // that actually reached that account has anything to recognize.
        var invoiceJournal = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), si.IssueDate)
        {
            ReferenceType = "SalesInvoice",
            ReferenceId = si.Id,
        };
        invoiceJournal.AddLine(Guid.NewGuid(), 1200m, true);
        invoiceJournal.AddLine(new JournalEntryLine(Guid.NewGuid(), invoiceJournal.Id, _deferredAccountId, 1200m, false)
        {
            ReferenceDetailId = item.Id,
        });
        invoiceJournal.Post();
        _jeRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { invoiceJournal }.AsQueryable()));
        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(new Company(_companyId, "Deferred Preview Co")));

        var input = new PreviewDeferredAccountingInput
        {
            CompanyId = _companyId,
            Type = DeferredAccountingType.Income,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 3, 31)
        };

        var preview = await _appService.PreviewDeferredAccountingAsync(input);

        Assert.NotNull(preview);
        Assert.Equal(_companyId, preview.CompanyId);
        Assert.Equal(DeferredAccountingType.Income, preview.Type);
        Assert.Equal(1, preview.TotalInvoicesCount);
        // One entry per elapsed month: January, February, March.
        Assert.Equal(3, preview.Items.Count);
        Assert.Equal(new DateTime(2026, 1, 31), preview.Items[0].PostingDate);
        // 31 + 28 + 31 = 90 of 365 days, each month rounded on its own.
        Assert.Equal(101.92m + 92.05m + 101.92m, preview.TotalAmountToRecognize);
        Assert.All(preview.Items, i => Assert.Equal(_deferredAccountId, i.DeferredAccountId));
    }

    [Fact]
    public async Task PreviewDeferredAccountingAsync_RowThatNeverReachedTheDeferredAccount_IsLeftOut()
    {
        var si = new SalesInvoice(Guid.NewGuid(), _companyId, _customerId, "INV-2026-0002", new DateTime(2026, 1, 1));
        si.AddItem(Guid.NewGuid(), "Annual Software Subscription", 1m, 1200m, 0m);
        var item = si.Items.First();
        item.EnableDeferredRevenue = true;
        item.DeferredRevenueAccountId = _deferredAccountId;
        item.ServiceStartDate = new DateTime(2026, 1, 1);
        item.ServiceEndDate = new DateTime(2026, 12, 31);
        si.Submit();
        si.Post();

        _siRepo.GetQueryableAsync().Returns(Task.FromResult(new List<SalesInvoice> { si }.AsQueryable()));
        // No GL line of this invoice on the deferred account: its income was posted in full
        // already, so recognizing it would count that income twice.
        _jeRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry>().AsQueryable()));
        _companyRepo.GetAsync(_companyId).Returns(Task.FromResult(new Company(_companyId, "Deferred Preview Co")));

        var preview = await _appService.PreviewDeferredAccountingAsync(new PreviewDeferredAccountingInput
        {
            CompanyId = _companyId,
            Type = DeferredAccountingType.Income,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 3, 31)
        });

        Assert.Empty(preview.Items);
        Assert.Equal(0m, preview.TotalAmountToRecognize);
    }

    [Fact]
    public async Task PreviewDeferredAccountingAsync_InvalidDateRange_ThrowsValidationException()
    {
        var input = new PreviewDeferredAccountingInput
        {
            CompanyId = _companyId,
            Type = DeferredAccountingType.Income,
            StartDate = new DateTime(2026, 6, 30),
            EndDate = new DateTime(2026, 6, 1) // start > end
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => _appService.PreviewDeferredAccountingAsync(input));
        Assert.Equal(MyERPDomainErrorCodes.InvalidDateRange, ex.Code);
    }
}
