using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for wiring DiscountCeilingValidationService into
/// PricingRuleApplicationService.ApplyToItemsAsync — the ceiling service (Item.MaxDiscount, Gotcha
/// #3222) had zero callers anywhere, so a Pricing Rule could silently discount an item past its own
/// master-configured ceiling on every Sales Order/Sales Invoice creation.
/// </summary>
public abstract class PricingRuleDiscountCeilingTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ApplyToItemsAsync_Selling_RuleExceedsItemMaxDiscount_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (item, rule) = await SeedAsync(maxDiscount: 10m, ruleDiscountPct: 20m, applicableFor: "Selling");
            var service = GetRequiredService<PricingRuleApplicationService>();

            var context = new PricingRuleContext { ItemId = item.Id, ItemName = item.ItemName, Qty = 1, Rate = 100m };

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                service.ApplyToItemsAsync(new() { context }, DateTime.Today, "Selling"));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.MaxDiscountExceeded);
        });
    }

    [Fact]
    public async Task ApplyToItemsAsync_Selling_RuleWithinItemMaxDiscount_Applies()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (item, rule) = await SeedAsync(maxDiscount: 10m, ruleDiscountPct: 5m, applicableFor: "Selling");
            var service = GetRequiredService<PricingRuleApplicationService>();

            var context = new PricingRuleContext { ItemId = item.Id, ItemName = item.ItemName, Qty = 1, Rate = 100m };

            var applied = await service.ApplyToItemsAsync(new() { context }, DateTime.Today, "Selling");

            applied.Count.ShouldBe(1);
            context.DiscountedRate.ShouldBe(95m);
        });
    }

    [Fact]
    public async Task ApplyToItemsAsync_Buying_RuleExceedsItemMaxDiscount_DoesNotThrow()
    {
        // MaxDiscount caps what a customer can be discounted (Selling); it must not block a bigger
        // discount a supplier grants on a Purchase Order.
        await WithUnitOfWorkAsync(async () =>
        {
            var (item, rule) = await SeedAsync(maxDiscount: 10m, ruleDiscountPct: 20m, applicableFor: "Buying");
            var service = GetRequiredService<PricingRuleApplicationService>();

            var context = new PricingRuleContext { ItemId = item.Id, ItemName = item.ItemName, Qty = 1, Rate = 100m };

            var applied = await service.ApplyToItemsAsync(new() { context }, DateTime.Today, "Buying");

            applied.Count.ShouldBe(1);
            context.DiscountedRate.ShouldBe(80m);
        });
    }

    private async Task<(Item Item, PricingRule Rule)> SeedAsync(decimal maxDiscount, decimal ruleDiscountPct, string applicableFor)
    {
        var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
        var ruleRepository = GetRequiredService<IRepository<PricingRule, Guid>>();

        var company = await companyRepository.InsertAsync(
            new Company(Guid.NewGuid(), $"Pricing Rule Discount Ceiling Test Co {applicableFor} {ruleDiscountPct}"), autoSave: true);

        var item = await itemRepository.InsertAsync(
            new Item(Guid.NewGuid(), company.Id, $"ITEM-PRDC-{applicableFor}-{ruleDiscountPct}", "Test Item", ItemType.Goods)
            {
                MaxDiscount = maxDiscount,
            }, autoSave: true);

        var rule = new PricingRule(Guid.NewGuid(), $"Test Rule {applicableFor} {ruleDiscountPct}",
            PricingRuleApplyOn.ItemCode, PricingRuleType.Discount)
        {
            ApplyOnId = item.Id,
            ApplicableFor = applicableFor,
            DiscountPercentage = ruleDiscountPct,
        };
        await ruleRepository.InsertAsync(rule, autoSave: true);

        return (item, rule);
    }
}
