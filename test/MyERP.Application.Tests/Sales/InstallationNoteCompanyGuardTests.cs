using System;
using System.Collections.Generic;
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
/// Regression coverage for a real gap found via ERPNext validate() parity: InstallationNoteAppService
/// never wired CompanyRestrictionValidationService (or even a plain company-match check against its
/// own Delivery Note), unlike the other Selling documents fixed this session. An Installation Note
/// could reference a Delivery Note, Customer or Item belonging to a different Company entirely.
/// </summary>
public abstract class InstallationNoteCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_DeliveryNoteFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var installationNoteAppService = GetRequiredService<IInstallationNoteAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "IN Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "IN Guard Other Co"), autoSave: true);

            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), otherCompany.Id, "IN Guard Customer"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "IN Guard WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "IN-ITEM-001", "Installation Guard Item", ItemType.Goods), autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), otherCompany.Id, customer.Id, warehouse.Id, "DN-IN-001", DateTime.UtcNow);
            dn.AddItem(item.Id, "Widget", quantity: 10m, unitPrice: 5m, taxAmount: 0m);
            await dnRepository.InsertAsync(dn, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                installationNoteAppService.CreateAsync(new CreateInstallationNoteDto
                {
                    CompanyId = ownerCompany.Id,
                    CustomerId = customer.Id,
                    DeliveryNoteId = dn.Id,
                    InstallationDate = DateTime.UtcNow,
                    Items = new List<InstallationNoteItemDto>
                    {
                        new() { ItemId = item.Id, Qty = 1m }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_SameCompanyEverywhere_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepository = GetRequiredService<IRepository<Customer, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var dnRepository = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var installationNoteAppService = GetRequiredService<IInstallationNoteAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "IN Guard Happy Co"), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "IN Series Guard Happy", "IN", "INGH-"), autoSave: true);
            var customer = await customerRepository.InsertAsync(new Customer(Guid.NewGuid(), company.Id, "IN Guard Happy Customer"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "IN Guard Happy WH"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "IN-ITEM-002", "Installation Happy Item", ItemType.Goods), autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), company.Id, customer.Id, warehouse.Id, "DN-IN-002", DateTime.UtcNow);
            dn.AddItem(item.Id, "Widget", quantity: 10m, unitPrice: 5m, taxAmount: 0m);
            await dnRepository.InsertAsync(dn, autoSave: true);

            var dto = await installationNoteAppService.CreateAsync(new CreateInstallationNoteDto
            {
                CompanyId = company.Id,
                CustomerId = customer.Id,
                DeliveryNoteId = dn.Id,
                InstallationDate = DateTime.UtcNow,
                Items = new List<InstallationNoteItemDto>
                {
                    new() { ItemId = item.Id, Qty = 1m }
                }
            });

            dto.CustomerId.ShouldBe(customer.Id);
        });
    }
}
