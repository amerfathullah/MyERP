using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a gap found while sweeping Standard Costing: ItemStandardCostAppService
/// validated and tracked PreviousRate for PPV, but Submit/Cancel never wrote back to
/// Item.StandardBuyingPrice — the field StockValuationService.CalculateStandardCost actually reads.
/// Submitting an Item Standard Cost record had zero effect on valuation.
/// </summary>
public abstract class ItemStandardCostAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SubmitAsync_SyncsItemStandardBuyingPrice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var appService = GetRequiredService<IItemStandardCostAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "ISC Test Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ISC-001", "Standard Cost Item", ItemType.Goods),
                autoSave: true);

            item.StandardBuyingPrice.ShouldBeNull();

            var created = await appService.CreateAsync(new CreateItemStandardCostDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                StandardRate = 42.5m,
                EffectiveDate = DateTime.Today,
            });

            await appService.SubmitAsync(created.Id);

            var reloaded = await itemRepository.GetAsync(item.Id);
            reloaded.StandardBuyingPrice.ShouldBe(42.5m);
        });
    }

    [Fact]
    public async Task CancelAsync_FallsBackToPriorSubmittedRate()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var appService = GetRequiredService<IItemStandardCostAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "ISC Test Co 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ISC-002", "Standard Cost Item 2", ItemType.Goods),
                autoSave: true);

            var first = await appService.CreateAsync(new CreateItemStandardCostDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                StandardRate = 10m,
                EffectiveDate = DateTime.Today.AddDays(-2),
            });
            await appService.SubmitAsync(first.Id);

            var second = await appService.CreateAsync(new CreateItemStandardCostDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                StandardRate = 25m,
                EffectiveDate = DateTime.Today,
            });
            await appService.SubmitAsync(second.Id);

            (await itemRepository.GetAsync(item.Id)).StandardBuyingPrice.ShouldBe(25m);

            // Cancelling the newer record should fall back to the still-submitted older one,
            // not leave the item silently pinned to the cancelled rate.
            await appService.CancelAsync(second.Id);

            (await itemRepository.GetAsync(item.Id)).StandardBuyingPrice.ShouldBe(10m);
        });
    }

    [Fact]
    public async Task CancelAsync_LastRemainingRecord_LeavesItemPriceUntouched()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var appService = GetRequiredService<IItemStandardCostAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "ISC Test Co 3"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ISC-003", "Standard Cost Item 3", ItemType.Goods),
                autoSave: true);

            var created = await appService.CreateAsync(new CreateItemStandardCostDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                StandardRate = 15m,
                EffectiveDate = DateTime.Today,
            });
            await appService.SubmitAsync(created.Id);
            (await itemRepository.GetAsync(item.Id)).StandardBuyingPrice.ShouldBe(15m);

            await appService.CancelAsync(created.Id);

            // No submitted record remains — don't null out a value another workflow may rely on.
            (await itemRepository.GetAsync(item.Id)).StandardBuyingPrice.ShouldBe(15m);
        });
    }
}
