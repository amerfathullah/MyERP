using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;
using Xunit;

namespace MyERP.Domain.Tests.Accounting;

public class JournalEntryCompanyValidationTests
{
    private readonly Guid _companyAId = Guid.NewGuid();
    private readonly Guid _companyBId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();
    private readonly Guid _accountA1Id = Guid.NewGuid();
    private readonly Guid _accountA2Id = Guid.NewGuid();
    private readonly Guid _accountB1Id = Guid.NewGuid();

    private static void ConfigureLazyServiceProvider(
        Volo.Abp.Application.Services.ApplicationService service,
        CompanyRestrictionValidationService companyRestriction)
    {
        var lazyProvider = Substitute.For<Volo.Abp.DependencyInjection.IAbpLazyServiceProvider>();
        var guidGen = Substitute.For<Volo.Abp.Guids.IGuidGenerator>();
        guidGen.Create().Returns(_ => Guid.NewGuid());
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>(Arg.Any<Func<IServiceProvider, object>>()).Returns(guidGen);
        lazyProvider.LazyGetService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);
        lazyProvider.LazyGetRequiredService<Volo.Abp.Guids.IGuidGenerator>().Returns(guidGen);

        var mapper = Substitute.For<IObjectMapper>();
        mapper.Map<JournalEntry, JournalEntryDto>(Arg.Any<JournalEntry>()).Returns(callInfo =>
        {
            var je = callInfo.Arg<JournalEntry>();
            return new JournalEntryDto
            {
                Id = je.Id,
                CompanyId = je.CompanyId,
                FiscalYearId = je.FiscalYearId,
                PostingDate = je.PostingDate,
                Status = je.Status.ToString(),
                TotalDebit = je.TotalDebit,
                TotalCredit = je.TotalCredit
            };
        });
        lazyProvider.LazyGetService<IObjectMapper>(Arg.Any<Func<IServiceProvider, object>>()).Returns(mapper);
        lazyProvider.LazyGetService<IObjectMapper>().Returns(mapper);
        lazyProvider.LazyGetRequiredService<IObjectMapper>().Returns(mapper);

        lazyProvider.LazyGetRequiredService<CompanyRestrictionValidationService>().Returns(companyRestriction);
        lazyProvider.LazyGetService<CompanyRestrictionValidationService>(Arg.Any<Func<IServiceProvider, object>>()).Returns(companyRestriction);
        lazyProvider.LazyGetService<CompanyRestrictionValidationService>().Returns(companyRestriction);

        service.LazyServiceProvider = lazyProvider;
    }

    [Fact]
    public async Task UpdateAsync_AccountCompanyMismatch_ThrowsCompanyMismatch()
    {
        // Per ERPNext PR #59655 / commit a7ee553218:
        // Switching Company or adding accounts from another Company must be rejected on save, not just submit
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        var periodRepo = Substitute.For<IRepository<AccountingPeriod, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();

        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        // Account A1 belongs to Company A, Account B1 belongs to Company B
        var acctA1 = new Account(_accountA1Id, _companyAId, "1001", "Cash A", AccountType.Asset);
        var acctB1 = new Account(_accountB1Id, _companyBId, "2001", "Payable B", AccountType.Liability);
        var accounts = new List<Account> { acctA1, acctB1 }.AsQueryable();
        acctRepo.GetQueryableAsync().Returns(Task.FromResult(accounts));

        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo);

        var existingJe = new JournalEntry(Guid.NewGuid(), _companyAId, _fiscalYearId, DateTime.UtcNow);
        existingJe.AddLine(_accountA1Id, 100m, isDebit: true);
        existingJe.AddLine(_accountA1Id, 100m, isDebit: false);
        jeRepo.GetAsync(existingJe.Id, includeDetails: true).Returns(Task.FromResult(existingJe));

        var appService = new JournalEntryAppService(jeRepo, numGen, compRepo, periodRepo, fyRepo);
        ConfigureLazyServiceProvider(appService, companyRestriction);

        // Update JE with lines including Account B1 (which belongs to Company B) for Company A JE
        var updateDto = new CreateJournalEntryDto
        {
            CompanyId = _companyAId,
            FiscalYearId = _fiscalYearId,
            PostingDate = DateTime.UtcNow,
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = _accountA1Id, Amount = 100m, IsDebit = true },
                new() { AccountId = _accountB1Id, Amount = 100m, IsDebit = false },
            }
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await appService.UpdateAsync(existingJe.Id, updateDto);
        });

        ex.Code.ShouldBe(MyERPDomainErrorCodes.CompanyRestrictionBlocked);
    }

    [Fact]
    public async Task UpdateAsync_MatchingCompanyAccounts_Succeeds()
    {
        var jeRepo = Substitute.For<IRepository<JournalEntry, Guid>>();
        var numGen = Substitute.For<IDocumentNumberGenerator>();
        var compRepo = Substitute.For<IRepository<Company, Guid>>();
        var periodRepo = Substitute.For<IRepository<AccountingPeriod, Guid>>();
        var fyRepo = Substitute.For<IRepository<FiscalYear, Guid>>();

        var restrictionRepo = Substitute.For<IRepository<CompanyRestrictionEntry, Guid>>();
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var custRepo = Substitute.For<IRepository<Customer, Guid>>();
        var suppRepo = Substitute.For<IRepository<Supplier, Guid>>();
        var acctRepo = Substitute.For<IRepository<Account, Guid>>();
        var whRepo = Substitute.For<IRepository<Warehouse, Guid>>();

        var acctA1 = new Account(_accountA1Id, _companyAId, "1001", "Cash A", AccountType.Asset);
        var acctA2 = new Account(_accountA2Id, _companyAId, "2001", "Payable A", AccountType.Liability);
        var accounts = new List<Account> { acctA1, acctA2 }.AsQueryable();
        acctRepo.GetQueryableAsync().Returns(Task.FromResult(accounts));

        var companyRestriction = new CompanyRestrictionValidationService(
            restrictionRepo, itemRepo, custRepo, suppRepo, acctRepo, whRepo);

        var existingJe = new JournalEntry(Guid.NewGuid(), _companyAId, _fiscalYearId, DateTime.UtcNow);
        existingJe.AddLine(_accountA1Id, 50m, isDebit: true);
        existingJe.AddLine(_accountA2Id, 50m, isDebit: false);
        jeRepo.GetAsync(existingJe.Id, includeDetails: true).Returns(Task.FromResult(existingJe));

        var appService = new JournalEntryAppService(jeRepo, numGen, compRepo, periodRepo, fyRepo);
        ConfigureLazyServiceProvider(appService, companyRestriction);

        var updateDto = new CreateJournalEntryDto
        {
            CompanyId = _companyAId,
            FiscalYearId = _fiscalYearId,
            PostingDate = DateTime.UtcNow,
            Lines = new List<CreateJournalEntryLineDto>
            {
                new() { AccountId = _accountA1Id, Amount = 150m, IsDebit = true },
                new() { AccountId = _accountA2Id, Amount = 150m, IsDebit = false },
            }
        };

        var result = await appService.UpdateAsync(existingJe.Id, updateDto);

        result.ShouldNotBeNull();
        result.TotalDebit.ShouldBe(150m);
        result.TotalCredit.ShouldBe(150m);
        await jeRepo.Received(1).UpdateAsync(Arg.Any<JournalEntry>(), autoSave: true);
    }
}
