using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

public abstract class PurchaseConversionAppServiceTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task ConvertMaterialRequestToPurchaseOrder_Copies_ConversionFactor_And_Deducts_Draft_POs()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var conversionService = GetRequiredService<IPurchaseConversionAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "Purchase Conversion Test Co"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "Supplier ABC"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "ITEM-CONV-1", "Conv Item", ItemType.Goods)
            {
                StandardBuyingPrice = 45m
            }, autoSave: true);

            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "POS", "PurchaseOrder", "PO-"), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-CONV-001", MaterialRequestType.Purchase, DateTime.UtcNow.Date, company.TenantId);
            mr.AddItem(item.Id, "Conv Item", quantity: 10m, uom: "Box", conversionFactor: 5m);
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            // Act 1: Convert MR to PO
            var poDto = await conversionService.ConvertMaterialRequestToPurchaseOrderAsync(mr.Id, supplier.Id);

            poDto.ShouldNotBeNull();
            poDto.Items.Count.ShouldBe(1);
            poDto.Items[0].ItemId.ShouldBe(item.Id);
            poDto.Items[0].Quantity.ShouldBe(10m);
            poDto.Items[0].ConversionFactor.ShouldBe(5m);
            poDto.Items[0].MaterialRequestItemId.ShouldBe(mr.Items.Single().Id);
            poDto.Status.ShouldBe("Draft");

            // Act 2: Second conversion attempt should fail because the first draft PO already covers 10m
            var ex = await Should.ThrowAsync<Volo.Abp.BusinessException>(async () =>
                await conversionService.ConvertMaterialRequestToPurchaseOrderAsync(mr.Id, supplier.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.DocumentAlreadyConverted);
        });
    }
}
