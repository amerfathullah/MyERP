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
}
