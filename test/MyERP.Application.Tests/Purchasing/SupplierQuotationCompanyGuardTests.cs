using System;
using System.Collections.Generic;
using System.Linq;
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

    [Fact]
    public async Task SubmitAndCancel_TogglesRfqSupplierQuoteStatus()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var rfqRepo = GetRequiredService<IRepository<RequestForQuotation, Guid>>();
            var sqAppService = GetRequiredService<ISupplierQuotationAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "SQ Status Co 1"), autoSave: true);
            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), company.Id, "SQ Status Supp 1"), autoSave: true);
            var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), company.Id, "SQ-ITEM-2", "SQ Item 2", ItemType.Goods), autoSave: true);

            await GetRequiredService<IRepository<DocumentSeries, Guid>>().InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "SQ Series", "SQ", "SQST-"), autoSave: true);

            var rfq = new RequestForQuotation(Guid.NewGuid(), company.Id, "RFQ-STATUS-1", DateTime.UtcNow.Date);
            rfq.AddItem(item.Id, "SQ Item 2", 1, "Unit");
            rfq.AddSupplier(supplier.Id, supplier.Name);
            rfq.Submit();
            await rfqRepo.InsertAsync(rfq, autoSave: true);

            var sq = await sqAppService.CreateAsync(new CreateSupplierQuotationDto
            {
                CompanyId = company.Id,
                SupplierId = supplier.Id,
                TransactionDate = DateTime.UtcNow.Date,
                RequestForQuotationId = rfq.Id,
                Items = new[] { new CreateSQItemDto { ItemId = item.Id, ItemName = "SQ Item 2", Qty = 1, Rate = 100 } }
            });
            await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();

            await sqAppService.SubmitAsync(sq.Id);
            await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
            var afterSubmit = (await rfqRepo.WithDetailsAsync()).First(r => r.Id == rfq.Id);
            afterSubmit.Suppliers[0].QuoteStatus.ShouldBe("Received");
            afterSubmit.Suppliers[0].EmailSent.ShouldBeFalse();

            await sqAppService.CancelAsync(sq.Id);
            await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
            var afterCancel = (await rfqRepo.WithDetailsAsync()).First(r => r.Id == rfq.Id);
            afterCancel.Suppliers[0].QuoteStatus.ShouldBe("Pending");
        });
    }

    [Fact]
    public async Task CreateAsync_DisabledSupplier_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var company = await GetRequiredService<IRepository<Company, Guid>>().InsertAsync(new Company(Guid.NewGuid(), "SQ Disabled Co 1"), autoSave: true);
            var supplier = new Supplier(Guid.NewGuid(), company.Id, "SQ Disabled Supp 1") { IsActive = false };
            await GetRequiredService<IRepository<Supplier, Guid>>().InsertAsync(supplier, autoSave: true);
            var item = await GetRequiredService<IRepository<Item, Guid>>().InsertAsync(new Item(Guid.NewGuid(), company.Id, "SQ-ITEM-3", "SQ Item 3", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                GetRequiredService<ISupplierQuotationAppService>().CreateAsync(new CreateSupplierQuotationDto
                {
                    CompanyId = company.Id,
                    SupplierId = supplier.Id,
                    TransactionDate = DateTime.UtcNow.Date,
                    Items = new[] { new CreateSQItemDto { ItemId = item.Id, ItemName = "SQ Item 3", Qty = 1, Rate = 10 } }
                }));
        });
    }
}
