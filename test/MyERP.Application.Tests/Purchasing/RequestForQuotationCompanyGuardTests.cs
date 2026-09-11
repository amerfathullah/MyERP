using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class RequestForQuotationCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WarehouseFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var rfqAppService = GetRequiredService<IRequestForQuotationAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Other Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "RFQ Guard Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "RFQ-ITEM-1", "RFQ Item 1", ItemType.Goods), autoSave: true);
            var crossWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), otherCompany.Id, "Cross Warehouse 1"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                rfqAppService.CreateAsync(new CreateRfqDto
                {
                    CompanyId = ownerCompany.Id,
                    TransactionDate = DateTime.UtcNow.Date,
                    Suppliers = new List<CreateRfqSupplierDto>
                    {
                        new() { SupplierId = supplier.Id }
                    },
                    Items = new List<CreateRfqItemDto>
                    {
                        new() { ItemId = item.Id, Description = "RFQ Item 1", Qty = 1, WarehouseId = crossWh.Id }
                    }
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_MaterialRequestItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var rfqAppService = GetRequiredService<IRequestForQuotationAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Owner Co 2"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "RFQ Guard Other Co 2"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "RFQ Guard Supp 2"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "RFQ-ITEM-2", "RFQ Item 2", ItemType.Goods), autoSave: true);
            var otherItem = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), otherCompany.Id, "RFQ-ITEM-2-OTHER", "RFQ Item 2 Other", ItemType.Goods), autoSave: true);

            var crossMr = new MaterialRequest(Guid.NewGuid(), otherCompany.Id, "MR-CROSS-2", MaterialRequestType.Purchase, DateTime.UtcNow.Date);
            crossMr.AddItem(otherItem.Id, "Cross MR Item", 5, "Unit");
            await mrRepo.InsertAsync(crossMr, autoSave: true);

            var crossMrItemId = crossMr.Items[0].Id;

            await Should.ThrowAsync<BusinessException>(() =>
                rfqAppService.CreateAsync(new CreateRfqDto
                {
                    CompanyId = ownerCompany.Id,
                    TransactionDate = DateTime.UtcNow.Date,
                    Suppliers = new List<CreateRfqSupplierDto>
                    {
                        new() { SupplierId = supplier.Id }
                    },
                    Items = new List<CreateRfqItemDto>
                    {
                        new() { ItemId = item.Id, Description = "RFQ Item 2", Qty = 1, MaterialRequestItemId = crossMrItemId }
                    }
                }));
        });
    }
}
