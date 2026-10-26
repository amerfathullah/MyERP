using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Sales;

public class LoyaltyPointServiceTests
{
    private static LoyaltyPointService CreateService(IEnumerable<LoyaltyPointEntry> entries)
    {
        var programRepo = Substitute.For<IRepository<LoyaltyProgram, Guid>>();
        var entryRepo = Substitute.For<IRepository<LoyaltyPointEntry, Guid>>();
        entryRepo.GetQueryableAsync().Returns(entries.AsQueryable());
        return new LoyaltyPointService(programRepo, entryRepo);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAvailablePointsAsync_SubtractsRedeemedFromEarned()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var entries = new[]
        {
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 100, new DateTime(2026, 1, 1)),
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, -30, new DateTime(2026, 2, 1)),
        };
        var service = CreateService(entries);

        var available = await service.GetAvailablePointsAsync(customerId, programId, new DateTime(2026, 6, 1));

        available.ShouldBe(70);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAvailablePointsAsync_ExcludesExpiredPoints()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var entries = new[]
        {
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 100, new DateTime(2026, 1, 1), expiryDate: new DateTime(2026, 3, 1)),
        };
        var service = CreateService(entries);

        var available = await service.GetAvailablePointsAsync(customerId, programId, new DateTime(2026, 6, 1));

        available.ShouldBe(0);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAvailablePointsAsync_NeverGoesNegative()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var entries = new[]
        {
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 20, new DateTime(2026, 1, 1)),
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, -50, new DateTime(2026, 2, 1)),
        };
        var service = CreateService(entries);

        var available = await service.GetAvailablePointsAsync(customerId, programId, new DateTime(2026, 6, 1));

        available.ShouldBe(0);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAvailablePointsAsync_IgnoresOtherCustomersAndPrograms()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var entries = new[]
        {
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 100, new DateTime(2026, 1, 1)),
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), programId, 500, new DateTime(2026, 1, 1)),
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, Guid.NewGuid(), 500, new DateTime(2026, 1, 1)),
        };
        var service = CreateService(entries);

        var available = await service.GetAvailablePointsAsync(customerId, programId, new DateTime(2026, 6, 1));

        available.ShouldBe(100);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAvailablePointsAsync_WithExcludeInvoiceId_ExcludesPointsEarnedByThatInvoice()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        var entries = new[]
        {
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 100, new DateTime(2026, 1, 1)) { InvoiceId = invoiceA },
            new LoyaltyPointEntry(Guid.NewGuid(), Guid.NewGuid(), customerId, programId, 50, new DateTime(2026, 2, 1)) { InvoiceId = invoiceB },
        };
        var service = CreateService(entries);

        var available = await service.GetAvailablePointsAsync(customerId, programId, new DateTime(2026, 6, 1), excludeInvoiceId: invoiceB);

        available.ShouldBe(100);
    }

    [Fact]
    public async System.Threading.Tasks.Task RedeemPointsAsync_SkipsEarningEntryFromSameInvoice()
    {
        var customerId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var invoiceA = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();

        var program = new LoyaltyProgram(programId, companyId, "Gold Program", 1m);
        program.AddTier("Gold Tier", 0m, 1m, 1m);

        var earnA = new LoyaltyPointEntry(Guid.NewGuid(), companyId, customerId, programId, 50, new DateTime(2026, 1, 1))
        {
            InvoiceId = invoiceA,
            TierName = "Gold Tier"
        };
        var earnB = new LoyaltyPointEntry(Guid.NewGuid(), companyId, customerId, programId, 30, new DateTime(2026, 2, 1))
        {
            InvoiceId = invoiceB,
            TierName = "Gold Tier"
        };

        var entryList = new List<LoyaltyPointEntry> { earnA, earnB };

        var programRepo = Substitute.For<IRepository<LoyaltyProgram, Guid>>();
        programRepo.GetAsync(programId).Returns(System.Threading.Tasks.Task.FromResult(program));
        var entryRepo = Substitute.For<IRepository<LoyaltyPointEntry, Guid>>();
        entryRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(entryList.AsQueryable()));
        entryRepo.InsertAsync(Arg.Do<LoyaltyPointEntry>(e => entryList.Add(e))).Returns(System.Threading.Tasks.Task.FromResult(earnA));

        var service = new LoyaltyPointService(programRepo, entryRepo);

        // Redeem 40 points on Invoice B: Invoice B's own 30 points must be skipped, redeeming from Invoice A
        var result = await service.RedeemPointsAsync(
            programId, customerId, 40, new DateTime(2026, 3, 1), companyId,
            invoiceType: "SalesInvoice", invoiceId: invoiceB);

        result.PointsRedeemed.ShouldBe(40);
        var redemption = entryList.FirstOrDefault(e => e.Points < 0);
        redemption.ShouldNotBeNull();
        redemption.RedeemAgainstId.ShouldBe(earnA.Id);
    }
}
