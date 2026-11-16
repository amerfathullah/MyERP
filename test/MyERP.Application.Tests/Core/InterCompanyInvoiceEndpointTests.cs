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
            dto.CanCreateInterCompanyPurchaseInvoice.ShouldBeTrue();
            dto.InterCompanyPurchaseInvoiceId.ShouldBeNull();

            var createdPi = await siAppService.CreateInterCompanyPurchaseInvoiceAsync(si.Id);
            createdPi.InterCompanyInvoiceId.ShouldBe(si.Id);
            createdPi.CompanyId.ShouldBe(buyer.Id);
            createdPi.GrandTotal.ShouldBe(100m);

            var reloadedDto = await siAppService.GetAsync(si.Id);
            reloadedDto.InterCompanyPurchaseInvoiceId.ShouldBe(createdPi.Id);
            reloadedDto.InterCompanyPurchaseInvoiceNumber.ShouldBe("IC-SI-IC-ENDP-01");
            reloadedDto.CanCreateInterCompanyPurchaseInvoice.ShouldBeFalse();

            // Cancelling the posted inter-company PI exposes creation again in GetAsync
            var piEntity = await piRepo.GetAsync(createdPi.Id);
            piEntity.Submit();
            piEntity.Post();
            piEntity.Cancel();
            await piRepo.UpdateAsync(piEntity, autoSave: true);

            var afterCancelDto = await siAppService.GetAsync(si.Id);
            afterCancelDto.InterCompanyPurchaseInvoiceId.ShouldBeNull();
            afterCancelDto.CanCreateInterCompanyPurchaseInvoice.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task SalesInvoice_InternalReturnPurchaseInvoice_CountsAsUninvoiced_AndBlocksReturnCancelWhenOverbilled()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();
            var conversionAppService = GetRequiredService<IPurchaseConversionAppService>();

            var seller = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Ret Seller"), autoSave: true);
            var buyer = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "IC Ret Buyer"), autoSave: true);
            await seriesRepo.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), buyer.Id, "PI Return Series", "PurchaseInvoice", "PI-RET-"),
                autoSave: true);

            var internalCustomer = await customerRepo.InsertAsync(
                new Customer(Guid.NewGuid(), seller.Id, "Buyer (Customer)") { RepresentsCompanyId = buyer.Id },
                autoSave: true);
            await supplierRepo.InsertAsync(
                new Supplier(Guid.NewGuid(), buyer.Id, "Seller (Supplier)") { RepresentsCompanyId = seller.Id },
                autoSave: true);
            var item = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), seller.Id, "IC-RET-01", "Returnable Service", ItemType.Service),
                autoSave: true);
            await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), buyer.Id, "IC-RET-01", "Returnable Service", ItemType.Service),
                autoSave: true);

            var si = new SalesInvoice(Guid.NewGuid(), seller.Id, internalCustomer.Id, "SI-IC-RET-01", DateTime.UtcNow.Date);
            si.AddItem(item.Id, "Returnable Service", 5m, 50m, 0m);
            si.Submit();
            si.Post();
            await siRepo.InsertAsync(si, autoSave: true);

            var pi1Dto = await siAppService.CreateInterCompanyPurchaseInvoiceAsync(si.Id);
            pi1Dto.Items[0].SalesInvoiceItemId.ShouldBe(si.Items[0].Id);

            var pi1Entity = await piRepo.GetAsync(pi1Dto.Id);
            pi1Entity.Submit();
            pi1Entity.Post();
            await piRepo.UpdateAsync(pi1Entity, autoSave: true);

            // Counterpart PI must not expose Make Sales Invoice (PR #66937)
            var pi1Loaded = await piAppService.GetAsync(pi1Entity.Id);
            pi1Loaded.CanCreateInterCompanySalesInvoice.ShouldBeFalse();

            // Full SI invoiced -> button hidden
            var siAfterPi1 = await siAppService.GetAsync(si.Id);
            siAfterPi1.CanCreateInterCompanyPurchaseInvoice.ShouldBeFalse();

            // Partial return (2 of 5) makes 2 units uninvoiced again (PR #66916 & #66937)
            var retDraft = await conversionAppService.MakePurchaseInvoiceReturnAsync(pi1Entity.Id);
            retDraft.Items[0].SalesInvoiceItemId.ShouldBe(si.Items[0].Id);
            var retEntity = await piRepo.GetAsync(retDraft.Id);
            retEntity.ClearItems();
            retEntity.AddItem(pi1Entity.Items[0].ItemId, pi1Entity.Items[0].Description, -2m, pi1Entity.Items[0].UnitPrice, 0m, pi1Entity.Items[0].Uom);
            retEntity.Items[^1].SalesInvoiceItemId = si.Items[0].Id;
            retEntity.Submit();
            retEntity.Post();
            await piRepo.UpdateAsync(retEntity, autoSave: true);

            var siWhilePending = await siAppService.GetAsync(si.Id);
            siWhilePending.CanCreateInterCompanyPurchaseInvoice.ShouldBeTrue();

            var pi2Dto = await siAppService.CreateInterCompanyPurchaseInvoiceAsync(si.Id);
            pi2Dto.Items.Count.ShouldBe(1);
            pi2Dto.Items[0].Quantity.ShouldBe(2m);
            pi2Dto.Items[0].SalesInvoiceItemId.ShouldBe(si.Items[0].Id);

            // All pending qty now covered by PI-2 -> button hidden
            var siAfterPi2 = await siAppService.GetAsync(si.Id);
            siAfterPi2.CanCreateInterCompanyPurchaseInvoice.ShouldBeFalse();

            // Cancelling the return PI would push net billed qty (5 + 2 = 7) above SI qty (5) -> blocked (PR #66916)
            var overEx = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => piAppService.CancelAsync(retEntity.Id));
            overEx.Code.ShouldBe(MyERPDomainErrorCodes.OverBilling);
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
            dto.CanCreateInterCompanySalesInvoice.ShouldBeTrue();
            dto.InterCompanyInvoiceId.ShouldBeNull();

            var createdSi = await piAppService.CreateInterCompanySalesInvoiceAsync(pi.Id);
            createdSi.InterCompanyPurchaseInvoiceId.ShouldBe(pi.Id);
            createdSi.CompanyId.ShouldBe(seller.Id);
            createdSi.GrandTotal.ShouldBe(120m);
            createdSi.Items[0].PurchaseInvoiceItemId.ShouldBe(pi.Items[0].Id);

            var piAfterCreate = await piAppService.GetAsync(pi.Id);
            piAfterCreate.CanCreateInterCompanySalesInvoice.ShouldBeFalse();

            var siRepo = GetRequiredService<IRepository<SalesInvoice, Guid>>();
            var siAppService = GetRequiredService<ISalesInvoiceAppService>();
            var siEntity = await siRepo.GetAsync(createdSi.Id);
            siEntity.Submit();
            siEntity.Post();
            await siRepo.UpdateAsync(siEntity, autoSave: true);

            // Counterpart SI made from PI must not expose Make Purchase Invoice (PR #66937)
            var counterpartSiDto = await siAppService.GetAsync(siEntity.Id);
            counterpartSiDto.CanCreateInterCompanyPurchaseInvoice.ShouldBeFalse();
        });
    }
}
