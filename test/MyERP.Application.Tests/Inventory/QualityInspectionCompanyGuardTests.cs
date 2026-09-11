using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

public abstract class QualityInspectionCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
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
            var qiAppService = GetRequiredService<IQualityInspectionAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI Guard Other Co"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QI-ITEM-OWNER", "QI Item Owner", ItemType.Goods), autoSave: true);

            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "QI-ITEM-OTHER", "QI Item Other", ItemType.Goods), autoSave: true);
            var customer = await customerRepository.InsertAsync(
                new Customer(Guid.NewGuid(), otherCompany.Id, "QI Customer Other"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "QI WH Other"), autoSave: true);

            var dn = new DeliveryNote(Guid.NewGuid(), otherCompany.Id, customer.Id, warehouse.Id, "DN-QI-OTHER-001", DateTime.UtcNow);
            dn.AddItem(itemOther.Id, "Widget Other", 5m, 10m, 0m);
            await dnRepository.InsertAsync(dn, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                qiAppService.CreateAsync(new CreateQualityInspectionDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = itemOwner.Id,
                    InspectionType = InspectionType.Outgoing,
                    ReferenceType = "DeliveryNote",
                    ReferenceId = dn.Id,
                    SampleSize = 1,
                    InspectionDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_PurchaseReceiptFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var warehouseRepository = GetRequiredService<IRepository<Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var prRepository = GetRequiredService<IRepository<PurchaseReceipt, Guid>>();
            var qiAppService = GetRequiredService<IQualityInspectionAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI PR Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI PR Other Co"), autoSave: true);

            var itemOwner = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "QI-PR-ITEM-OWNER", "QI PR Item Owner", ItemType.Goods), autoSave: true);

            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "QI-PR-ITEM-OTHER", "QI PR Item Other", ItemType.Goods), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(
                new Supplier(Guid.NewGuid(), otherCompany.Id, "QI Supplier Other"), autoSave: true);
            var warehouse = await warehouseRepository.InsertAsync(
                new Warehouse(Guid.NewGuid(), otherCompany.Id, "QI PR WH Other"), autoSave: true);

            var pr = new PurchaseReceipt(Guid.NewGuid(), otherCompany.Id, supplier.Id, warehouse.Id, "PR-QI-OTHER-001", DateTime.UtcNow);
            pr.AddItem(itemOther.Id, "Material Other", 10m, 20m, 0m);
            await prRepository.InsertAsync(pr, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                qiAppService.CreateAsync(new CreateQualityInspectionDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = itemOwner.Id,
                    InspectionType = InspectionType.Incoming,
                    ReferenceType = "PurchaseReceipt",
                    ReferenceId = pr.Id,
                    SampleSize = 1,
                    InspectionDate = DateTime.UtcNow
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var qiAppService = GetRequiredService<IQualityInspectionAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI Item Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "QI Item Other Co"), autoSave: true);

            var itemOther = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "QI-ITEM-DIFF-CO", "QI Item Diff Co", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                qiAppService.CreateAsync(new CreateQualityInspectionDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = itemOther.Id,
                    InspectionType = InspectionType.InProcess,
                    SampleSize = 1,
                    InspectionDate = DateTime.UtcNow
                }));
        });
    }
}
