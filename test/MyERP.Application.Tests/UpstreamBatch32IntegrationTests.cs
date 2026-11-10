using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP;

public abstract class UpstreamBatch32IntegrationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task InactiveSalesItems_ExcludesItemTemplates()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var inactiveService = GetRequiredService<IInactiveSalesItemsAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Inactive Items Co B32") { CurrencyCode = "MYR" },
                autoSave: true);

            // Item 1: Standard active stock item (not a template)
            var standardItem = new Item(
                Guid.NewGuid(), company.Id, "ITEM-STD-B32", "Standard Stock Item B32", ItemType.Goods)
            {
                MaintainStock = true,
                HasVariants = false,
                IsActive = true
            };
            await itemRepo.InsertAsync(standardItem, autoSave: true);

            // Item 2: Item template with variants (HasVariants = true)
            // PR #63466 (commit 119201aecc): templates must be excluded from Inactive Sales Items
            var templateItem = new Item(
                Guid.NewGuid(), company.Id, "ITEM-TMPL-B32", "Template Item B32", ItemType.Goods)
            {
                MaintainStock = true,
                HasVariants = true,
                IsActive = true
            };
            await itemRepo.InsertAsync(templateItem, autoSave: true);

            var report = await inactiveService.GetReportAsync(new InactiveSalesItemsFilterDto
            {
                CompanyId = company.Id,
                DaysSinceLastOrder = 30,
                DocType = "Sales Order"
            });

            report.ShouldNotBeNull();
            report.Items.Count.ShouldBe(1);
            report.Items[0].ItemId.ShouldBe(standardItem.Id);
            report.Items[0].ItemCode.ShouldBe("ITEM-STD-B32");

            // Verify template item is excluded
            report.Items.ShouldNotContain(i => i.ItemId == templateItem.Id);
        });
    }

    [Fact]
    public async Task InactiveSalesItems_WithSales_FiltersByDaysThreshold()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var soRepo = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var inactiveService = GetRequiredService<IInactiveSalesItemsAppService>();

            var company = await companyRepo.InsertAsync(
                new Company(Guid.NewGuid(), "Inactive Sales Threshold Co B32") { CurrencyCode = "MYR" },
                autoSave: true);

            var customer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), company.Id, "Cust B32"),
                autoSave: true);

            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "ITEM-SOLD-B32", "Sold Item B32", ItemType.Goods)
                {
                    MaintainStock = true,
                    HasVariants = false,
                    IsActive = true
                },
                autoSave: true);

            var today = DateTime.UtcNow.Date;
            var orderDate = today.AddDays(-40);

            var order = new SalesOrder(
                Guid.NewGuid(), company.Id, customer.Id, "SO-B32-01", orderDate);
            order.AddItem(item.Id, "Sold Item Description", 5m, 100m, 0m);
            order.Submit();
            await soRepo.InsertAsync(order, autoSave: true);

            // Threshold: 30 days. Order was 40 days ago (> 30) -> inactive!
            var report30 = await inactiveService.GetReportAsync(new InactiveSalesItemsFilterDto
            {
                CompanyId = company.Id,
                DaysSinceLastOrder = 30,
                DocType = "Sales Order",
                AsOfDate = today
            });

            report30.Items.Count.ShouldBe(1);
            report30.Items[0].ItemId.ShouldBe(item.Id);
            report30.Items[0].DaysSinceLastOrder.ShouldBe(40);
            report30.Items[0].Quantity.ShouldBe(5m);

            // Threshold: 50 days. Order was 40 days ago (< 50) -> active, not in report!
            var report50 = await inactiveService.GetReportAsync(new InactiveSalesItemsFilterDto
            {
                CompanyId = company.Id,
                DaysSinceLastOrder = 50,
                DocType = "Sales Order",
                AsOfDate = today
            });

            report50.Items.Count.ShouldBe(0);
        });
    }
}
