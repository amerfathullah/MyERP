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

public abstract class SupplierQuotationCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_RfqFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var rfqRepo = GetRequiredService<IRepository<RequestForQuotation, Guid>>();
            var sqAppService = GetRequiredService<ISupplierQuotationAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SQ Guard Owner Co 1"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SQ Guard Other Co 1"), autoSave: true);

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SQ Guard Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), ownerCompany.Id, "SQ-ITEM-1", "SQ Item 1", ItemType.Goods), autoSave: true);

            var crossRfq = new RequestForQuotation(Guid.NewGuid(), otherCompany.Id, "RFQ-CROSS-1", DateTime.UtcNow.Date);
            await rfqRepo.InsertAsync(crossRfq, autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                sqAppService.CreateAsync(new CreateSupplierQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    TransactionDate = DateTime.UtcNow.Date,
                    RequestForQuotationId = crossRfq.Id,
                    Items = new[]
                    {
                        new CreateSQItemDto { ItemId = item.Id, ItemName = "SQ Item 1", Qty = 1, Rate = 100 }
                    }
                }));
        });
    }
}
