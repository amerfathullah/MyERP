using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// ManufacturingAppService.DeleteBomAsync had zero Angular callers and zero test coverage — neither
/// the deletion itself nor its "block delete while an active Work Order references the BOM" guard
/// were ever exercised. Added a Delete button to bom-detail.component.ts; this covers the App
/// service it now reaches.
/// </summary>
public abstract class DeleteBomActiveWorkOrderGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task DeleteBomAsync_NoWorkOrders_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Delete Bom Test Co 1"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-DELBOM1", "Deletable Widget", ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-DELBOM1", fgItem.Id), autoSave: true);

            await manufacturingAppService.DeleteBomAsync(bom.Id);

            (await bomRepository.FindAsync(bom.Id)).ShouldBeNull();
        });
    }

    [Fact]
    public async Task DeleteBomAsync_ActiveWorkOrderReferencesIt_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Delete Bom Test Co 2"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-DELBOM2", "Blocked Widget", ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-DELBOM2", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-DELBOM2", fgItem.Id, bom.Id, quantity: 10m);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(
                () => manufacturingAppService.DeleteBomAsync(bom.Id));

            (await bomRepository.FindAsync(bom.Id)).ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task DeleteBomAsync_OnlyCancelledWorkOrderReferencesIt_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Delete Bom Test Co 3"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-DELBOM3", "Cancelled-WO Widget", ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-DELBOM3", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-DELBOM3", fgItem.Id, bom.Id, quantity: 10m);
            wo.Submit();
            wo.Cancel();
            await woRepository.InsertAsync(wo, autoSave: true);

            await manufacturingAppService.DeleteBomAsync(bom.Id);

            (await bomRepository.FindAsync(bom.Id)).ShouldBeNull();
        });
    }
}
