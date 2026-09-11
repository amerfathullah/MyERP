using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Maintenance;

public abstract class MaintenanceScheduleCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_CustomerFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS Cust Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS Cust Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "MS-ITEM-OWNER", "MS Item Owner", ItemType.Goods), autoSave: true);
            var customerOther = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "MS Cust Other"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                scheduleAppService.CreateAsync(new CreateMaintenanceScheduleDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customerOther.Id,
                    Items = new List<CreateMaintenanceScheduleItemDto>
                    {
                        new()
                        {
                            ItemId = item.Id,
                            StartDate = DateTime.UtcNow,
                            EndDate = DateTime.UtcNow.AddMonths(1),
                            Periodicity = MaintenancePeriodicity.Monthly
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SalesOrderFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var soRepository = GetRequiredService<IRepository<SalesOrder, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS SO Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS SO Other Co"), autoSave: true);

            var customerOwner = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), ownerCompany.Id, "MS Cust Owner"), autoSave: true);
            var customerOther = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "MS Cust Other 2"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "MS-SO-ITEM-OWNER", "MS SO Item Owner", ItemType.Goods), autoSave: true);
            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "MS-SO-ITEM-OTHER", "MS SO Item Other", ItemType.Goods), autoSave: true);

            var crossCoSo = new SalesOrder(Guid.NewGuid(), otherCompany.Id, customerOther.Id, "SO-MS-CROSS", DateTime.UtcNow);
            crossCoSo.AddItem(itemOther.Id, "Widget Other", 1, 50, 0);
            await soRepository.InsertAsync(crossCoSo, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                scheduleAppService.CreateAsync(new CreateMaintenanceScheduleDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customerOwner.Id,
                    SalesOrderId = crossCoSo.Id,
                    Items = new List<CreateMaintenanceScheduleItemDto>
                    {
                        new()
                        {
                            ItemId = itemOwner.Id,
                            StartDate = DateTime.UtcNow,
                            EndDate = DateTime.UtcNow.AddMonths(1),
                            Periodicity = MaintenancePeriodicity.Monthly
                        }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scheduleAppService = GetRequiredService<IMaintenanceScheduleAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS Item Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MS Item Other Co"), autoSave: true);

            var customerOwner = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), ownerCompany.Id, "MS Item Cust Owner"), autoSave: true);

            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "MS-ITEM-OTHER-CO", "MS Item Other Co", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                scheduleAppService.CreateAsync(new CreateMaintenanceScheduleDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customerOwner.Id,
                    Items = new List<CreateMaintenanceScheduleItemDto>
                    {
                        new()
                        {
                            ItemId = itemOther.Id,
                            StartDate = DateTime.UtcNow,
                            EndDate = DateTime.UtcNow.AddMonths(1),
                            Periodicity = MaintenancePeriodicity.Monthly
                        }
                    }
                }));
        });
    }
}
