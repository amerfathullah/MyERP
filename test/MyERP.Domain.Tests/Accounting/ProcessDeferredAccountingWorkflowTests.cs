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

        var emptyJes = new List<JournalEntry>();
        _jeRepo.GetQueryableAsync().Returns(Task.FromResult(emptyJes.AsQueryable()));

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
        Assert.NotEmpty(preview.Items);
        Assert.True(preview.TotalAmountToRecognize > 0);
        Assert.All(preview.Items, i => Assert.Equal(_deferredAccountId, i.DeferredAccountId));
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
