using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Notification.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for wiring StockAlertNotificationService — had zero callers anywhere despite
/// its own doc comment saying it should fire after DN submit / SI UpdateStock / SE Issue-Transfer,
/// AND its notifications targeted Guid.Empty (permanently invisible to any real user), the exact
/// same bug already found and fixed for BusinessNotificationService.NotifyWorkOrderCompletedAsync in
/// an earlier session.
/// </summary>
public abstract class StockAlertNotificationServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CheckAndNotifyAsync_BelowReorderLevel_NotifiesRealRecipients()
    {
        Guid itemId = default, warehouseId = default, companyId = default;
        Guid? tenantId = null;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Stock Alert Test Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stock Alert WH"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SA-LOW", "Below Reorder Item", ItemType.Goods)
                {
                    ReorderLevel = 50m,
                }, autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), item.Id, warehouse.Id) { ActualQty = 10m }, autoSave: true);

            itemId = item.Id;
            warehouseId = warehouse.Id;
            companyId = company.Id;
            tenantId = company.TenantId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var service = GetRequiredService<StockAlertNotificationService>();
            await service.CheckAndNotifyAsync(itemId, warehouseId, companyId, tenantId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var notifications = await notificationRepository.GetListAsync(n => n.Subject.Contains("SA-LOW"));

            notifications.ShouldNotBeEmpty();
            notifications.ShouldAllBe(n => n.UserId != Guid.Empty);
            notifications.ShouldAllBe(n => n.Body!.Contains("Below Reorder Item"));
        });
    }

    [Fact]
    public async Task CheckAndNotifyAsync_AboveReorderLevel_NoNotification()
    {
        Guid itemId = default, warehouseId = default, companyId = default;
        Guid? tenantId = null;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Stock Alert Test Co 2"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Stock Alert WH 2"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SA-OK", "At Reorder Item", ItemType.Goods)
                {
                    ReorderLevel = 50m,
                }, autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), item.Id, warehouse.Id) { ActualQty = 500m }, autoSave: true);

            itemId = item.Id;
            warehouseId = warehouse.Id;
            companyId = company.Id;
            tenantId = company.TenantId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var service = GetRequiredService<StockAlertNotificationService>();
            await service.CheckAndNotifyAsync(itemId, warehouseId, companyId, tenantId);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var notifications = await notificationRepository.GetListAsync(n => n.Subject.Contains("SA-OK"));
            notifications.ShouldBeEmpty();
        });
    }
}
