using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using MyERP.Accounting;
using MyERP.Accounting.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch30IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task LoyaltyProgram_TierDetermination_TreatsEmptyMinSpentAsZero()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var loyaltyRepo = GetRequiredService<IRepository<LoyaltyProgram, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Loyalty MinSpent Co B30") { CurrencyCode = "MYR" },
                autoSave: true);

            var program = new LoyaltyProgram(
                Guid.NewGuid(),
                company.Id,
                "Loyalty Program B30",
                conversionFactor: 10m);

            // PR #64312 / commit db4196a0b1:
            // Lowest tier with empty/null minSpent should be treated as 0
            program.AddTier("Gold", minSpent: 5000m, collectionFactor: 2m, redemptionFactor: 1m);
            program.AddTier("Bronze", minSpent: null, collectionFactor: 1m, redemptionFactor: 0.5m);

            program.Validate();
            await loyaltyRepo.InsertAsync(program, autoSave: true);

            var retrieved = await loyaltyRepo.GetAsync(program.Id);
            retrieved.ShouldNotBeNull();
            retrieved.Tiers.Count.ShouldBe(2);

            var tier = retrieved.DetermineTier(totalSpent: 100m, currentTransactionAmount: 50m);
            tier.TierName.ShouldBe("Bronze");
            tier.MinSpent.ShouldBe(0m);

            var points = retrieved.CalculatePointsEarned(150m, tier);
            points.ShouldBe(15); // floor(150 / 10) * 1 = 15
        });
    }

    [Fact]
    public async Task StockValuationCorrectionJob_SkipsQueuedRepostCoveredByEarlierPass()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var repostRepo = GetRequiredService<IRepository<RepostItemValuation, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Valuation Repost Merge Co B30") { CurrencyCode = "MYR" },
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-REPOST-B30", "Repost Test Item B30", Inventory.ItemType.Goods),
                autoSave: true);

            var wh = await whRepo.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "WH-REPOST-B30"),
                autoSave: true);

            var postingDate1 = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);
            var postingDate2 = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc);

            var repost1 = new RepostItemValuation(
                Guid.NewGuid(), company.Id, RepostMethod.ItemAndWarehouse,
                postingDate: postingDate1, itemId: item.Id, warehouseId: wh.Id);

            var repost2 = new RepostItemValuation(
                Guid.NewGuid(), company.Id, RepostMethod.ItemAndWarehouse,
                postingDate: postingDate2, itemId: item.Id, warehouseId: wh.Id);

            await repostRepo.InsertAsync(repost1, autoSave: true);
            await repostRepo.InsertAsync(repost2, autoSave: true);

            // Execute correction job (PR #63639 / commit 2944ee0c14)
            var job = new StockValuationCorrectionJob(
                repostRepo,
                sleRepo,
                GetRequiredService<GlRepostService>(),
                GetRequiredService<IServiceProvider>(),
                NullLogger<StockValuationCorrectionJob>.Instance);

            await job.ExecuteAsync(new StockValuationCorrectionJobArgs { CompanyId = company.Id });

            var updatedRepost1 = await repostRepo.GetAsync(repost1.Id);
            var updatedRepost2 = await repostRepo.GetAsync(repost2.Id);

            updatedRepost1.Status.ShouldBe(RepostStatus.Completed);
            updatedRepost2.Status.ShouldBe(RepostStatus.Skipped);
        });
    }

    [Fact]
    public void BankTransactionRule_EvaluateRules_RequiresWritePermission()
    {
        // PR #64324 / commit 7e2507da56:
        // Rule evaluation alters transaction matching state and requires write/edit permission
        var method = typeof(BankTransactionRuleAppService).GetMethod(nameof(BankTransactionRuleAppService.EvaluateRulesAsync));
        method.ShouldNotBeNull();

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.ShouldNotBeNull();
        authAttr.Policy.ShouldBe(MyERPPermissions.PaymentEntries.Edit);
    }
}
