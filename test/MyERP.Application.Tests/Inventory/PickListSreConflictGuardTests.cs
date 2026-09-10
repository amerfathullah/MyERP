using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for gotcha #3533: PickListAppService.CreateAsync's SRE conflict check
/// filtered on Status == DocumentStatus.Posted, but StockReservationEntry.Submit() only ever
/// transitions Draft -> DocumentStatus.Submitted — nothing in the domain ever sets an SRE's
/// Status to Posted. The guard could therefore never match any row and was permanent dead code.
/// Found while implementing the analogous StockReservationEntry conflict guard on
/// StockReconciliationAppService (commit faa0328a).
/// </summary>
public abstract class PickListSreConflictGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_SalesOrderWithActiveSre_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var sreRepository = GetRequiredService<IRepository<StockReservationEntry, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList SRE Guard Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "PL-SRE-ITEM-001", "PickList SRE Guard Item", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PickList SRE Guard WH"), autoSave: true);

            var salesOrderId = Guid.NewGuid();
            var sre = new StockReservationEntry(Guid.NewGuid(), company.Id, item.Id, warehouse.Id,
                "SalesOrder", salesOrderId, reservedQty: 10m);
            sre.Submit();
            await sreRepository.InsertAsync(sre, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                pickListAppService.CreateAsync(new CreatePickListDto
                {
                    CompanyId = company.Id,
                    Purpose = "Delivery",
                    SalesOrderId = salesOrderId,
                    Items =
                    [
                        new CreatePickListItemDto { ItemId = item.Id, WarehouseId = warehouse.Id, Qty = 5m }
                    ]
                }));
            ex.Code.ShouldBe("MyERP:13020");
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderWithNoActiveSre_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList SRE Guard Happy Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "PL-SRE-ITEM-002", "PickList SRE Guard Item 2", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PickList SRE Guard Happy WH"), autoSave: true);

            var dto = await pickListAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Delivery",
                SalesOrderId = Guid.NewGuid(),
                Items =
                [
                    new CreatePickListItemDto { ItemId = item.Id, WarehouseId = warehouse.Id, Qty = 5m }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
