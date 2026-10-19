using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace MyERP.Accounting;

public class BankAutoMatchCandidatePermissionTests
{
    private readonly IRepository<BankTransaction, Guid> _txRepo = Substitute.For<IRepository<BankTransaction, Guid>>();
    private readonly IRepository<PaymentEntry, Guid> _peRepo = Substitute.For<IRepository<PaymentEntry, Guid>>();
    private readonly IRepository<JournalEntry, Guid> _jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
    private readonly IRepository<JournalEntryLine, Guid> _jeLineRepo = Substitute.For<IRepository<JournalEntryLine, Guid>>();
    private readonly IRepository<BankAccount, Guid> _bankAccountRepo = Substitute.For<IRepository<BankAccount, Guid>>();
    private readonly IAbpAuthorizationService _authorizationService = Substitute.For<IAbpAuthorizationService>();
    private readonly ICurrentPrincipalAccessor _principalAccessor = Substitute.For<ICurrentPrincipalAccessor>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _bankAccountId = Guid.NewGuid();
    private readonly Guid _glAccountId = Guid.NewGuid();
    private readonly Guid _otherAccountId = Guid.NewGuid();

    private BankAutoMatchService CreateService(bool canReadPe, bool canReadJe)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "testuser") }, "test"));
        _principalAccessor.Principal.Returns(principal);

        _authorizationService.AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<object?>(),
            MyERPPermissions.PaymentEntries.Default)
            .Returns(Task.FromResult(canReadPe ? AuthorizationResult.Success() : AuthorizationResult.Failed()));

        _authorizationService.AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<object?>(),
            MyERPPermissions.JournalEntries.Default)
            .Returns(Task.FromResult(canReadJe ? AuthorizationResult.Success() : AuthorizationResult.Failed()));

        var lazyProvider = Substitute.For<IAbpLazyServiceProvider>();
        lazyProvider.LazyGetRequiredService<IAuthorizationService>().Returns(_authorizationService);
        lazyProvider.LazyGetService<IAuthorizationService>().Returns(_authorizationService);
        lazyProvider.LazyGetRequiredService<ICurrentPrincipalAccessor>().Returns(_principalAccessor);
        lazyProvider.LazyGetService<ICurrentPrincipalAccessor>().Returns(_principalAccessor);

        var service = new BankAutoMatchService(
            _txRepo,
            _peRepo,
            _jeRepo,
            _jeLineRepo,
            _bankAccountRepo)
        {
            LazyServiceProvider = lazyProvider
        };

        return service;
    }

    private (BankTransaction, PaymentEntry, JournalEntry) CreateTestData()
    {
        var tx = new BankTransaction(Guid.NewGuid(), _companyId, _bankAccountId, DateTime.Today, "Ref123", 1000m);

        var pe = new PaymentEntry(
            Guid.NewGuid(), _companyId, PaymentType.Receive, DateTime.Today,
            1000m, _otherAccountId, _bankAccountId)
        {
            ReferenceNumber = "Ref123"
        };
        pe.SetAmounts();
        pe.Submit();
        pe.Post();

        var je = new JournalEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today)
        {
            VoucherType = JournalEntryVoucherType.BankEntry,
            ReferenceNumber = "Ref123"
        };
        je.AddLineWithDimensions(_glAccountId, 1000m, true);
        je.AddLineWithDimensions(_otherAccountId, 1000m, false);
        je.Post();

        _txRepo.GetAsync(tx.Id).Returns(Task.FromResult(tx));
        _txRepo.GetQueryableAsync().Returns(Task.FromResult(new List<BankTransaction> { tx }.AsQueryable()));

        _peRepo.GetQueryableAsync().Returns(Task.FromResult(new List<PaymentEntry> { pe }.AsQueryable()));

        var bankAccount = new BankAccount(_bankAccountId, _companyId, "Main Bank", _glAccountId, "Maybank");
        _bankAccountRepo.FindAsync(_bankAccountId).Returns(Task.FromResult<BankAccount?>(bankAccount));

        _jeRepo.GetQueryableAsync().Returns(Task.FromResult(new List<JournalEntry> { je }.AsQueryable()));
        _jeLineRepo.GetQueryableAsync().Returns(Task.FromResult(je.Lines.AsQueryable()));

        return (tx, pe, je);
    }

    [Fact]
    public async Task GetMatchCandidatesAsync_WhenUserHasBothPermissions_ReturnsBothCandidates()
    {
        var (tx, pe, je) = CreateTestData();
        var service = CreateService(canReadPe: true, canReadJe: true);

        var candidates = await service.GetMatchCandidatesAsync(tx.Id, _companyId);

        candidates.Count.ShouldBe(2);
        candidates.ShouldContain(c => c.VoucherType == "PaymentEntry" && c.PaymentEntryId == pe.Id);
        candidates.ShouldContain(c => c.VoucherType == "JournalEntry" && c.JournalEntryId == je.Id);
    }

    [Fact]
    public async Task GetMatchCandidatesAsync_WhenUserLacksPePermission_HidesPaymentEntries()
    {
        var (tx, pe, je) = CreateTestData();
        var service = CreateService(canReadPe: false, canReadJe: true);

        var candidates = await service.GetMatchCandidatesAsync(tx.Id, _companyId);

        candidates.Count.ShouldBe(1);
        candidates.ShouldNotContain(c => c.VoucherType == "PaymentEntry");
        candidates.ShouldContain(c => c.VoucherType == "JournalEntry" && c.JournalEntryId == je.Id);
    }

    [Fact]
    public async Task GetMatchCandidatesAsync_WhenUserLacksJePermission_HidesJournalEntries()
    {
        var (tx, pe, je) = CreateTestData();
        var service = CreateService(canReadPe: true, canReadJe: false);

        var candidates = await service.GetMatchCandidatesAsync(tx.Id, _companyId);

        candidates.Count.ShouldBe(1);
        candidates.ShouldContain(c => c.VoucherType == "PaymentEntry" && c.PaymentEntryId == pe.Id);
        candidates.ShouldNotContain(c => c.VoucherType == "JournalEntry");
    }

    [Fact]
    public async Task GetMatchCandidatesAsync_WhenUserLacksBothPermissions_ReturnsEmpty()
    {
        var (tx, pe, je) = CreateTestData();
        var service = CreateService(canReadPe: false, canReadJe: false);

        var candidates = await service.GetMatchCandidatesAsync(tx.Id, _companyId);

        candidates.ShouldBeEmpty();
    }
}
