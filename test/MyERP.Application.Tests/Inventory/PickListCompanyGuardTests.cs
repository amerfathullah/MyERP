using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// pick_list.py validate_warehouses() blocks a row whose Warehouse belongs to a different company
/// than the Pick List. PickListAppService.CreateAsync had no company-restriction check at all — not
/// for the Warehouse on each row, nor for the direct-pick CustomerId — despite every other
/// transaction AppService referencing a company-restricted master wiring this in.
/// </summary>
public abstract class PickListCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), ownerCompany.Id, "PL-ITEM-001", "PickList Guard Item", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), otherCompany.Id, "PickList Guard Cross-Co WH"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                pickListAppService.CreateAsync(new CreatePickListDto
                {
                    CompanyId = ownerCompany.Id,
                    Purpose = "Material Transfer",
                    Items =
                    [
                        new CreatePickListItemDto { ItemId = item.Id, WarehouseId = warehouse.Id, Qty = 10m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList Guard Cust Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList Guard Cust Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), ownerCompany.Id, "PL-ITEM-002", "PickList Guard Item 2", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), ownerCompany.Id, "PickList Guard WH"), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "PickList Guard Cross-Co Customer"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                pickListAppService.CreateAsync(new CreatePickListDto
                {
                    CompanyId = ownerCompany.Id,
                    Purpose = "Delivery",
                    CustomerId = customer.Id,
                    Items =
                    [
                        new CreatePickListItemDto { ItemId = item.Id, WarehouseId = warehouse.Id, Qty = 5m }
                    ]
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanyWarehouseAndItem_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var pickListAppService = GetRequiredService<IPickListAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "PickList Guard Happy Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "PL-ITEM-003", "PickList Guard Item 3", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "PickList Guard Happy WH"), autoSave: true);

            var dto = await pickListAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = company.Id,
                Purpose = "Material Transfer",
                Items =
                [
                    new CreatePickListItemDto { ItemId = item.Id, WarehouseId = warehouse.Id, Qty = 10m }
                ]
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
