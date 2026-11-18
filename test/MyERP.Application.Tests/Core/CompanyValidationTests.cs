using System;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.HumanResources.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Core;

public abstract class CompanyValidationTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task UpdateSettingsAsync_DisablePerpetualInventoryWithActiveSle_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var sleRepo = GetRequiredService<IRepository<StockLedgerEntry, Guid>>();
            var svc = GetRequiredService<CompanyAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Pi Guard Co") { EnablePerpetualInventory = true }, autoSave: true);
            var wh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Main WH"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "PI-G-1", "PI Item", ItemType.Goods), autoSave: true);

            // Disabling when no SLE exists succeeds
            await svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
            {
                EnablePerpetualInventory = false,
            });
            await svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
            {
                EnablePerpetualInventory = true,
            });

            // Insert active SLE -> disabling now throws
            await sleRepo.InsertAsync(
                new StockLedgerEntry(Guid.NewGuid(), company.Id, item.Id, wh.Id, DateTime.UtcNow.Date, 5m, 10m, 5m, 50m),
                autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
                {
                    EnablePerpetualInventory = false,
                }));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task UpdateSettingsAsync_DisabledWarehouseOrNonTransitInTransit_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var svc = GetRequiredService<CompanyAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Wh Guard Co"), autoSave: true);
            var disabledWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Disabled WH") { IsActive = false }, autoSave: true);
            var stdWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Standard WH") { WarehouseType = WarehouseType.Standard }, autoSave: true);
            var transitWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "Transit WH") { WarehouseType = WarehouseType.Transit }, autoSave: true);

            // Disabled default warehouse rejected
            await Should.ThrowAsync<BusinessException>(() =>
                svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
                {
                    EnablePerpetualInventory = true,
                    DefaultWarehouseId = disabledWh.Id,
                }));

            // Non-transit warehouse rejected for DefaultInTransitWarehouseId
            await Should.ThrowAsync<BusinessException>(() =>
                svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
                {
                    EnablePerpetualInventory = true,
                    DefaultInTransitWarehouseId = stdWh.Id,
                }));

            // Transit warehouse accepted for DefaultInTransitWarehouseId
            await svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
            {
                EnablePerpetualInventory = true,
                DefaultWarehouseId = stdWh.Id,
                DefaultInTransitWarehouseId = transitWh.Id,
            });
        });
    }

    [Fact]
    public async Task UpdateSettingsAsync_GroupOrForeignDefaultCostCenter_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var ccRepo = GetRequiredService<IRepository<CostCenter, Guid>>();
            var svc = GetRequiredService<CompanyAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cc Guard Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Cc Other Co"), autoSave: true);
            var groupCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), company.Id, "Group CC", isGroup: true), autoSave: true);
            var foreignCc = await ccRepo.InsertAsync(new CostCenter(Guid.NewGuid(), otherCompany.Id, "Foreign CC"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
                {
                    EnablePerpetualInventory = true,
                    DefaultCostCenterId = groupCc.Id,
                }));

            await Should.ThrowAsync<BusinessException>(() =>
                svc.UpdateSettingsAsync(company.Id, new UpdateCompanySettingsDto
                {
                    EnablePerpetualInventory = true,
                    DefaultCostCenterId = foreignCc.Id,
                }));
        });
    }

    [Fact]
    public async Task DeleteAsync_CompanyWithEmployee_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var empRepo = GetRequiredService<IRepository<Employee, Guid>>();
            var svc = GetRequiredService<CompanyAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Delete Guard Co"), autoSave: true);
            await empRepo.InsertAsync(new Employee(Guid.NewGuid(), company.Id, "EMP-DEL-1", "Alice"), autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() => svc.DeleteAsync(company.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }
}
