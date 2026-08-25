using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
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
/// Regression coverage for SubscriptionAppService.CancelAsync's dead proration logic:
/// SubscriptionBillingEngine.CalculateProrationFactor had zero callers anywhere despite
/// loyalty-subscription-dunning-full.md's own spec ("If cancelling from Active with postpaid
/// billing: Generates a PRORATED final invoice from period start to cancellation date") — CancelAsync
/// just called sub.Cancel() with no billing at all, silently dropping the partial period a postpaid
/// customer had already consumed but never paid for.
/// </summary>
public abstract class SubscriptionCancelProrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CancelAsync_ActivePostpaid_MidPeriod_GeneratesProratedFinalInvoice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var salesInvoiceRepository = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Cancel Proration Test Co"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Sub Cancel Cust"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCANCEL-1", "Sub Cancel Item", ItemType.Goods), autoSave: true);

            // 30-day period, cancelling exactly 10 days in (day 1..10 inclusive elapsed).
            var periodStart = DateTime.UtcNow.Date.AddDays(-9);
            var periodEnd = periodStart.AddDays(29); // 30-day period

            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", periodStart, "Monthly")
            {
                SubscriptionNumber = "SUB-CANCEL-001",
                IsPrepaid = false,
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 300m, itemName: "Sub Cancel Item");
            sub.CurrentInvoiceStart = periodStart;
            sub.CurrentInvoiceEnd = periodEnd;
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            var result = await subscriptionAppService.CancelAsync(sub.Id);

            result.Status.ShouldBe((int)SubscriptionStatus.Cancelled);
            result.FinalProratedInvoice.ShouldNotBeNull();
            // 10 elapsed days / 30 total days × 300 = 100.
            result.FinalProratedInvoice!.GrandTotal.ShouldBe(100m);

            var invoice = await salesInvoiceRepository.GetAsync(result.FinalProratedInvoice.InvoiceId);
            invoice.CustomerId.ShouldBe(customer.Id);
            invoice.Items.Single().UnitPrice.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CancelAsync_Prepaid_MidPeriod_NoFinalInvoice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Cancel Proration Test Co 2"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Sub Cancel Cust 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCANCEL-2", "Sub Cancel Item 2", ItemType.Goods), autoSave: true);

            var periodStart = DateTime.UtcNow.Date.AddDays(-9);
            var periodEnd = periodStart.AddDays(29);

            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", periodStart, "Monthly")
            {
                SubscriptionNumber = "SUB-CANCEL-002",
                IsPrepaid = true, // already paid upfront — cancelling early bills nothing more
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 300m, itemName: "Sub Cancel Item 2");
            sub.CurrentInvoiceStart = periodStart;
            sub.CurrentInvoiceEnd = periodEnd;
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            var result = await subscriptionAppService.CancelAsync(sub.Id);

            result.Status.ShouldBe((int)SubscriptionStatus.Cancelled);
            result.FinalProratedInvoice.ShouldBeNull();
        });
    }

    [Fact]
    public async Task CancelAsync_PostpaidPeriodAlreadyEnded_NoFinalInvoice()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Cancel Proration Test Co 3"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Sub Cancel Cust 3"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCANCEL-3", "Sub Cancel Item 3", ItemType.Goods), autoSave: true);

            // Current period already fully elapsed (ended 5 days ago) — nothing left to prorate.
            var periodStart = DateTime.UtcNow.Date.AddDays(-40);
            var periodEnd = DateTime.UtcNow.Date.AddDays(-5);

            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", periodStart, "Monthly")
            {
                SubscriptionNumber = "SUB-CANCEL-003",
                IsPrepaid = false,
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 300m, itemName: "Sub Cancel Item 3");
            sub.CurrentInvoiceStart = periodStart;
            sub.CurrentInvoiceEnd = periodEnd;
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            var result = await subscriptionAppService.CancelAsync(sub.Id);

            result.Status.ShouldBe((int)SubscriptionStatus.Cancelled);
            result.FinalProratedInvoice.ShouldBeNull();
        });
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subscriptionRepository = GetRequiredService<IRepository<Subscription, Guid>>();
            var subscriptionAppService = GetRequiredService<ISubscriptionAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Subscription Cancel Proration Test Co 4"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "Sub Cancel Cust 4"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SUBCANCEL-4", "Sub Cancel Item 4", ItemType.Goods), autoSave: true);

            var periodStart = DateTime.UtcNow.Date.AddDays(-9);
            var sub = new Subscription(Guid.NewGuid(), company.Id, customer.Id, "Customer", periodStart, "Monthly")
            {
                SubscriptionNumber = "SUB-CANCEL-004",
            };
            sub.AddPlan(item.Id, qty: 1m, rate: 300m, itemName: "Sub Cancel Item 4");
            sub.CurrentInvoiceStart = periodStart;
            sub.CurrentInvoiceEnd = periodStart.AddDays(29);
            sub.Cancel();
            await subscriptionRepository.InsertAsync(sub, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => subscriptionAppService.CancelAsync(sub.Id));
        });
    }
}
