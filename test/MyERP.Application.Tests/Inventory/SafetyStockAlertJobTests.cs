using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.BackgroundJobs;
using MyERP.Inventory.Entities;
using MyERP.Notification.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a gap found while surveying BusinessNotificationService for unwired
/// methods: NotifyLowStockAsync had zero callers anywhere. Item.SafetyStock (a stricter floor than
/// ReorderLevel — the latter already handled proactively by AutoReorderService) was set but never
/// checked against actual stock on hand. New SafetyStockAlertJob closes that gap.
/// </summary>
/// <remarks>
/// Job execution and setup are separate WithUnitOfWorkAsync blocks — see
/// WorkOrderOverdueNotificationRecipientTests for why (background job classes get no automatic
/// UnitOfWork wrapping; a query's deferred execution can run after its per-call implicit UoW has
/// already disposed the DbContext, or fail to see a not-yet-committed insert from an outer shared
/// UoW).
/// </remarks>
public abstract class SafetyStockAlertJobTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ExecuteAsync_BelowSafetyStock_NotifiesRecipients()
    {
        Guid companyId = default;
        Guid? tenantId = null;

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var binRepository = GetRequiredService<IRepository<Bin, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Safety Stock Test Co"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Safety Stock WH"), autoSave: true);

            var lowItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SS-LOW", "Below Safety Stock Item", ItemType.Goods)
                {
                    SafetyStock = 100m,
                }, autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), lowItem.Id, warehouse.Id) { ActualQty = 20m }, autoSave: true);

            var okItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "SS-OK", "At Safety Stock Item", ItemType.Goods)
                {
                    SafetyStock = 50m,
                }, autoSave: true);
            await binRepository.InsertAsync(
                new Bin(Guid.NewGuid(), okItem.Id, warehouse.Id) { ActualQty = 200m }, autoSave: true);

            companyId = company.Id;
            tenantId = company.TenantId;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var job = GetRequiredService<SafetyStockAlertJob>();
            await job.ExecuteAsync(new SafetyStockAlertJobArgs { CompanyId = companyId, TenantId = tenantId });
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var notificationRepository = GetRequiredService<IRepository<AppNotification, Guid>>();
            var notifications = await notificationRepository.GetListAsync(n => n.SourceDocumentType == "Item");

            notifications.ShouldNotBeEmpty();
            notifications.ShouldAllBe(n => n.UserId != Guid.Empty);
            notifications.ShouldAllBe(n => n.Body!.Contains("Below Safety Stock Item"));
            notifications.ShouldNotContain(n => n.Body!.Contains("At Safety Stock Item"));
        });
    }
}
