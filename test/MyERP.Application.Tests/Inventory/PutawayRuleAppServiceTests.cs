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
/// Regression coverage for the validations ERPNext's PutawayRule.validate performs and this
/// AppService was missing entirely: one rule per (item, warehouse), warehouse must belong to the
/// rule's company, and priority has a floor of 1.
///
/// The duplicate check matters beyond tidiness — PutawayService reads each matching rule's free
/// space from the same Bin row, so two rules on one warehouse would each hand out that warehouse's
/// full free capacity during allocation.
/// </summary>
public abstract class PutawayRuleAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_RejectsDuplicateRuleForSameItemAndWarehouse()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var appService = GetRequiredService<IPutawayRuleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PUT-001", "Putaway Item", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Putaway Main"), autoSave: true);

            var input = new CreateUpdatePutawayRuleDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                StockCapacity = 100m,
                Priority = 1,
            };

            await appService.CreateAsync(input);

            var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(input));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.PutawayRuleDuplicate);
        });
    }

    [Fact]
    public async Task CreateAsync_RejectsWarehouseFromAnotherCompany()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var appService = GetRequiredService<IPutawayRuleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Co A"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Co B"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PUT-002", "Putaway Item 2", ItemType.Goods), autoSave: true);
            var foreignWarehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "Other Co Warehouse"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(new CreateUpdatePutawayRuleDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = foreignWarehouse.Id,
                StockCapacity = 100m,
                Priority = 1,
            }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PutawayRuleWarehouseCompanyMismatch);
        });
    }

    [Fact]
    public async Task CreateAsync_RejectsPriorityBelowOne()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var appService = GetRequiredService<IPutawayRuleAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "Putaway Co C"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "PUT-003", "Putaway Item 3", ItemType.Goods), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), company.Id, "Putaway Main C"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => appService.CreateAsync(new CreateUpdatePutawayRuleDto
            {
                CompanyId = company.Id,
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                StockCapacity = 100m,
                Priority = 0,
            }));

            ex.Code.ShouldBe(MyERPDomainErrorCodes.PutawayRulePriorityInvalid);
        });
    }
}
