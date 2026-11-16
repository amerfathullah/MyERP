using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Core;

public abstract class InterCompanyInvoiceEndpointTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task SalesInvoice_InternalCustomer_GetAsyncAndCreateInterCompanyPurchaseInvoice_Work()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();

            var seller = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Seller Co"), autoSave: true);
            var buyer = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Buyer Co"), autoSave: true);

            var internalCustomer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), seller.Id, "Buyer (Internal Customer)") { RepresentsCompanyId = buyer.Id },
                autoSave: true);
            await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), buyer.Id, "Seller (Internal Supplier)") { RepresentsCompanyId = seller.Id },
                autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), seller.Id, "IC-ITEM-01", "Inter-Company Widget", ItemType.Service),
                autoSave: true);
            await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), buyer.Id, "IC-ITEM-01", "Inter-Company Widget", ItemType.Service),
                autoSave: true);

            var si = new SalesInvoice(Guid.NewGuid(), seller.Id, internalCustomer.Id, "SI-IC-ENDP-01", DateTime.UtcNow.Date);
            si.AddItem(item.Id, "Inter-Company Widget", 2m, 50m, 0m);
            si.Submit();
            si.Post();
            await siRepo.InsertAsync(si, autoSave: true);

            var dto = await siAppService.GetAsync(si.Id);
            dto.IsInternalCustomer.ShouldBeTrue();
            dto.InterCompanyPurchaseInvoiceId.ShouldBeNull();

            var createdPi = await siAppService.CreateInterCompanyPurchaseInvoiceAsync(si.Id);
            createdPi.InterCompanyInvoiceId.ShouldBe(si.Id);
            createdPi.CompanyId.ShouldBe(buyer.Id);
            createdPi.GrandTotal.ShouldBe(100m);

            var reloadedDto = await siAppService.GetAsync(si.Id);
            reloadedDto.InterCompanyPurchaseInvoiceId.ShouldBe(createdPi.Id);
            reloadedDto.InterCompanyPurchaseInvoiceNumber.ShouldBe("IC-SI-IC-ENDP-01");

            // Cancelling the posted inter-company PI exposes creation again in GetAsync
            var piEntity = await piRepo.GetAsync(createdPi.Id);
            piEntity.Submit();
            piEntity.Post();
            piEntity.Cancel();
            await piRepo.UpdateAsync(piEntity, autoSave: true);

            var afterCancelDto = await siAppService.GetAsync(si.Id);
            afterCancelDto.InterCompanyPurchaseInvoiceId.ShouldBeNull();
        });
    }

    [Fact]
    public async Task PurchaseInvoice_InternalSupplier_GetAsyncAndCreateInterCompanySalesInvoice_Work()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var buyer = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC PI Buyer Co"), autoSave: true);
            var seller = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC PI Seller Co"), autoSave: true);

            var internalSupplier = await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), buyer.Id, "Seller (Internal Supplier)") { RepresentsCompanyId = seller.Id },
                autoSave: true);
            await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), seller.Id, "Buyer (Internal Customer)") { RepresentsCompanyId = buyer.Id },
                autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), buyer.Id, "IC-ITEM-02", "Inter-Company Part", ItemType.Service),
                autoSave: true);
            await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), seller.Id, "IC-ITEM-02", "Inter-Company Part", ItemType.Service),
                autoSave: true);

            var pi = new PurchaseInvoice(Guid.NewGuid(), buyer.Id, internalSupplier.Id, "PI-IC-ENDP-01", DateTime.UtcNow.Date);
            pi.AddItem(item.Id, "Inter-Company Part", 3m, 40m, 0m);
            pi.Submit();
            pi.Post();
            await piRepo.InsertAsync(pi, autoSave: true);

            var dto = await piAppService.GetAsync(pi.Id);
            dto.IsInternalSupplier.ShouldBeTrue();
            dto.InterCompanyInvoiceId.ShouldBeNull();

            var createdSi = await piAppService.CreateInterCompanySalesInvoiceAsync(pi.Id);
            createdSi.InterCompanyPurchaseInvoiceId.ShouldBe(pi.Id);
            createdSi.CompanyId.ShouldBe(seller.Id);
            createdSi.GrandTotal.ShouldBe(120m);
        });
    }
}
