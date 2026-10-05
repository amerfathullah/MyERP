using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

/// <summary>
/// Verifies ERPNext PR #59358 (commit 73a7fd8c41):
/// Authorise whitelisted / bank recon / unreconcile methods against the target voucher records.
/// </summary>
public class BankReconAndUnreconcileAuthorizationTests
{
    private readonly IRepository<BankTransaction, Guid> _txRepo = Substitute.For<IRepository<BankTransaction, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<BankAccount, Guid> _baRepo = Substitute.For<IRepository<BankAccount, Guid>>();
    private readonly IRepository<UnreconcilePayment, Guid> _unreconRepo = Substitute.For<IRepository<UnreconcilePayment, Guid>>();
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepo = Substitute.For<IRepository<PaymentLedgerEntry, Guid>>();
    private readonly IAbpLazyServiceProvider _lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
    private readonly IAuthorizationService _authService = Substitute.For<IAuthorizationService, IAbpAuthorizationService>();

    public BankReconAndUnreconcileAuthorizationTests()
    {
        _authService.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<string>())
            .Returns(Task.FromResult(AuthorizationResult.Success()));
        _lazyProvider.LazyGetRequiredService<IAuthorizationService>().Returns(_authService);
    }

    [Fact]
    public async Task BankReconciliation_ReconcileAsync_AuthorizesPaymentEntryEdit_WhenPaymentEntryLinked()
    {
        var appService = new BankReconciliationAppService(
            _txRepo, _peRepo, _jeRepo, _baRepo, null!, null!)
        {
            LazyServiceProvider = _lazyProvider
        };

        var txId = Guid.NewGuid();
        var peId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var tx = new BankTransaction(txId, companyId, bankAccountId, DateTime.UtcNow, "Payment from client", 500m);
        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow, 500m, Guid.NewGuid(), Guid.NewGuid());

        _txRepo.GetAsync(txId).Returns(Task.FromResult(tx));
        _peRepo.GetAsync(peId).Returns(Task.FromResult(pe));
        _txRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BankTransaction> { tx }.AsQueryable()));

        await appService.ReconcileAsync(new ReconcileBankTransactionDto
        {
            TransactionId = txId,
            PaymentEntryId = peId,
            MatchedDocumentRef = "PE-001"
        });

        await _authService.Received(1).AuthorizeAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<object>(),
            MyERPPermissions.PaymentEntries.Edit);
    }

    [Fact]
    public async Task BankReconciliation_ReconcileAsync_AuthorizesJournalEntryPost_WhenJournalEntryLinked()
    {
        var appService = new BankReconciliationAppService(
            _txRepo, _peRepo, _jeRepo, _baRepo, null!, null!)
        {
            LazyServiceProvider = _lazyProvider
        };

        var txId = Guid.NewGuid();
        var jeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fiscalYearId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var tx = new BankTransaction(txId, companyId, bankAccountId, DateTime.UtcNow, "Interest income", 120m);
        var je = new JournalEntry(jeId, companyId, fiscalYearId, DateTime.UtcNow);

        _txRepo.GetAsync(txId).Returns(Task.FromResult(tx));
        _jeRepo.GetAsync(jeId).Returns(Task.FromResult(je));
        _txRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BankTransaction> { tx }.AsQueryable()));

        await appService.ReconcileAsync(new ReconcileBankTransactionDto
        {
            TransactionId = txId,
            JournalEntryId = jeId,
            MatchedDocumentRef = "JE-001"
        });

        await _authService.Received(1).AuthorizeAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<object>(),
            MyERPPermissions.JournalEntries.Post);
    }

    [Fact]
    public async Task BankReconciliation_UnreconcileAsync_AuthorizesPaymentEntryEdit()
    {
        var appService = new BankReconciliationAppService(
            _txRepo, _peRepo, _jeRepo, _baRepo, null!, null!)
        {
            LazyServiceProvider = _lazyProvider
        };

        var txId = Guid.NewGuid();
        var peId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var tx = new BankTransaction(txId, companyId, bankAccountId, DateTime.UtcNow, "Payment", 500m);
        tx.Reconcile(peId, "PE-001");
        var pe = new PaymentEntry(peId, companyId, PaymentType.Receive, DateTime.UtcNow, 500m, Guid.NewGuid(), Guid.NewGuid());
        pe.SetClearanceDate(tx.TransactionDate);

        _txRepo.GetAsync(txId).Returns(Task.FromResult(tx));
        _peRepo.FindAsync(peId).Returns(Task.FromResult<PaymentEntry?>(pe));

        await appService.UnreconcileAsync(txId);

        await _authService.Received(1).AuthorizeAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<object>(),
            MyERPPermissions.PaymentEntries.Edit);
    }

    [Fact]
    public async Task UnreconcilePayment_CreateAsync_AuthorizesPaymentEntryEdit_ForPaymentEntry()
    {
        var appService = new UnreconcilePaymentAppService(
            _unreconRepo, _pleRepo, null!)
        {
            LazyServiceProvider = _lazyProvider
        };

        var peId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var ple = new PaymentLedgerEntry(Guid.NewGuid(), companyId, DateTime.UtcNow,
            Guid.NewGuid(), "Customer", Guid.NewGuid(),
            "PaymentEntry", peId,
            "SalesInvoice", Guid.NewGuid(),
            -500m, -500m, "MYR");

        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry> { ple }.AsQueryable()));

        var activityLogRepo = Substitute.For<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        _lazyProvider.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(activityLogRepo);

        await appService.CreateAsync(new CreateUnreconcilePaymentDto
        {
            CompanyId = companyId,
            VoucherType = UnreconcileVoucherType.PaymentEntry,
            VoucherId = peId
        });

        await _authService.Received(1).AuthorizeAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<object>(),
            MyERPPermissions.PaymentEntries.Edit);
    }

    [Fact]
    public async Task UnreconcilePayment_CreateAsync_AuthorizesJournalEntryPost_ForJournalEntry()
    {
        var appService = new UnreconcilePaymentAppService(
            _unreconRepo, _pleRepo, null!)
        {
            LazyServiceProvider = _lazyProvider
        };

        var jeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var ple = new PaymentLedgerEntry(Guid.NewGuid(), companyId, DateTime.UtcNow,
            Guid.NewGuid(), "Supplier", Guid.NewGuid(),
            "JournalEntry", jeId,
            "PurchaseInvoice", Guid.NewGuid(),
            300m, 300m, "MYR");

        _pleRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentLedgerEntry> { ple }.AsQueryable()));

        var activityLogRepo = Substitute.For<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>();
        _lazyProvider.LazyGetRequiredService<IRepository<global::MyERP.Core.Entities.DocumentActivityLog, Guid>>().Returns(activityLogRepo);

        await appService.CreateAsync(new CreateUnreconcilePaymentDto
        {
            CompanyId = companyId,
            VoucherType = UnreconcileVoucherType.JournalEntry,
            VoucherId = jeId
        });

        await _authService.Received(1).AuthorizeAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<object>(),
            MyERPPermissions.JournalEntries.Post);
    }
}
