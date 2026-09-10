using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Sales;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// subscription.py validate_end_date() throws if the end date doesn't clear at least one full
/// billing cycle past the start date. MyERP's equivalent, Subscription.ValidateSubscriptionPeriod(),
/// already existed (covered by a domain-level test) but had zero callers anywhere in the actual
/// write path — SubscriptionAppService.CreateAsync only checked `EndDate &lt; StartDate`, so an end
/// date landing inside the first billing cycle (e.g. Yearly billing, end date one day after start)
/// went through with no error.
/// </summary>
public abstract class SubscriptionPeriodGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_EndDateWithinFirstBillingCycle_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Guard Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Subscription Guard Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUB-ITEM-1", "Subscription Guard Item", ItemType.Goods), autoSave: true);

            var startDate = new DateTime(2026, 1, 1);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subscriptionAppService.CreateAsync(new CreateSubscriptionDto
                {
                    CompanyId = company.Id,
                    PartyId = customer.Id,
                    PartyType = "Customer",
                    BillingInterval = "Yearly",
                    StartDate = startDate,
                    EndDate = startDate.AddDays(1), // Nowhere near a full year — must be rejected.
                    Plans =
                    [
                        new CreateSubscriptionPlanDto { ItemId = item.Id, Qty = 1m, Rate = 100m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_EndDateAfterFullBillingCycle_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Guard Happy Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Subscription Guard Happy Customer"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUB-ITEM-2", "Subscription Guard Item 2", ItemType.Goods), autoSave: true);

            var startDate = new DateTime(2026, 1, 1);

            var dto = await subscriptionAppService.CreateAsync(new CreateSubscriptionDto
            {
                CompanyId = company.Id,
                PartyId = customer.Id,
                PartyType = "Customer",
                BillingInterval = "Monthly",
                StartDate = startDate,
                EndDate = startDate.AddMonths(2),
                Plans =
                [
                    new CreateSubscriptionPlanDto { ItemId = item.Id, Qty = 1m, Rate = 50m }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
