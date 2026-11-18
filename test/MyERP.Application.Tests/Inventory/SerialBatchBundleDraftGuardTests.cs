using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Dtos;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Integration tests verifying ERPNext PR #66994 / #58608 Serial and Batch Bundle guards
/// across DeliveryNote, PurchaseInvoice, StockReconciliation, and PickList Create/Update/Submit.
/// </summary>
public abstract class SerialBatchBundleDraftGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private async Task<(Company Owner, Company Other, Item Item, Warehouse OwnerWh)> SetupBaseMasterDataAsync(string prefix)
    {
        var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
        var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
        var whRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
        var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();

        var owner = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), $"{prefix}-Owner"), autoSave: true);
        var other = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), $"{prefix}-Other"), autoSave: true);
        var item = await itemRepo.InsertAsync(new Item(Guid.NewGuid(), owner.Id, $"{prefix}-ITEM", $"{prefix} Item", ItemType.Goods), autoSave: true);
        var ownerWh = await whRepo.InsertAsync(new Warehouse(Guid.NewGuid(), owner.Id, $"{prefix}-WH"), autoSave: true);
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), owner.Id, $"{prefix}-DN", "DeliveryNote", $"DN{prefix}-"), autoSave: true);
        await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), owner.Id, $"{prefix}-PI", "PurchaseInvoice", $"PI{prefix}-"), autoSave: true);

        return (owner, other, item, ownerWh);
    }

    [Fact]
    public async Task DeliveryNote_CreateUpdateSubmit_EnforcesBundleGuardsAndPersistsBundleId()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (owner, other, item, wh) = await SetupBaseMasterDataAsync("SABB-DN");
            var customerRepo = GetRequiredService<IRepository<Customer, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var dnRepo = GetRequiredService<IRepository<DeliveryNote, Guid>>();
            var dnAppService = GetRequiredService<IDeliveryNoteAppService>();

            var customer = await customerRepo.InsertAsync(new Customer(Guid.NewGuid(), owner.Id, "SABB-DN-Cust"), autoSave: true);

            var foreignBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), other.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "DeliveryNote", Guid.Empty, DateTime.UtcNow), autoSave: true);

            var otherDraftId = Guid.NewGuid();
            var otherDraftBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "DeliveryNote", otherDraftId, DateTime.UtcNow), autoSave: true);

            var unlinkedBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "DeliveryNote", Guid.Empty, DateTime.UtcNow), autoSave: true);

            var exForeign = await Should.ThrowAsync<BusinessException>(() => dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = owner.Id,
                CustomerId = customer.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10, SerialAndBatchBundleId = foreignBundle.Id }
                }
            }));
            exForeign.Code.ShouldBe(MyERPDomainErrorCodes.CompanyRestrictionBlocked);

            var exOtherDraft = await Should.ThrowAsync<BusinessException>(() => dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = owner.Id,
                CustomerId = customer.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10, SerialAndBatchBundleId = otherDraftBundle.Id }
                }
            }));
            exOtherDraft.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var created = await dnAppService.CreateAsync(new CreateDeliveryNoteDto
            {
                CompanyId = owner.Id,
                CustomerId = customer.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10, SerialAndBatchBundleId = unlinkedBundle.Id }
                }
            });
            created.Items.Single().SerialAndBatchBundleId.ShouldBe(unlinkedBundle.Id);

            var ownBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "DeliveryNote", created.Id, DateTime.UtcNow), autoSave: true);

            var updated = await dnAppService.UpdateAsync(created.Id, new CreateDeliveryNoteDto
            {
                CompanyId = owner.Id,
                CustomerId = customer.Id,
                WarehouseId = wh.Id,
                PostingDate = DateTime.UtcNow.Date,
                Items = new List<CreateDeliveryNoteItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 10, SerialAndBatchBundleId = ownBundle.Id }
                }
            });
            updated.Items.Single().SerialAndBatchBundleId.ShouldBe(ownBundle.Id);

            // SubmitAsync rejects if attached bundle is owned by another draft
            var hijackedDn = new DeliveryNote(Guid.NewGuid(), owner.Id, customer.Id, wh.Id, "DN-HIJACK", DateTime.UtcNow.Date);
            hijackedDn.AddItem(item.Id, "Item", 1, 10, 0, "Unit");
            hijackedDn.Items[^1].SerialAndBatchBundleId = otherDraftBundle.Id;
            await dnRepo.InsertAsync(hijackedDn, autoSave: true);

            var exSubmit = await Should.ThrowAsync<BusinessException>(() => dnAppService.SubmitAsync(hijackedDn.Id));
            exSubmit.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task PurchaseInvoice_CreateUpdateSubmit_EnforcesDraftBundleOwnership()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (owner, _, item, wh) = await SetupBaseMasterDataAsync("SABB-PI");
            var supplierRepo = GetRequiredService<IRepository<Supplier, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var piRepo = GetRequiredService<IRepository<PurchaseInvoice, Guid>>();
            var piAppService = GetRequiredService<IPurchaseInvoiceAppService>();

            var supplier = await supplierRepo.InsertAsync(new Supplier(Guid.NewGuid(), owner.Id, "SABB-PI-Supp"), autoSave: true);

            var otherDraftBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Inward, "PurchaseInvoice", Guid.NewGuid(), DateTime.UtcNow), autoSave: true);

            var cancelledBundle = new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Inward, "PurchaseInvoice", Guid.Empty, DateTime.UtcNow);
            cancelledBundle.Cancel();
            await bundleRepo.InsertAsync(cancelledBundle, autoSave: true);

            var unlinkedBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Inward, "PurchaseInvoice", Guid.Empty, DateTime.UtcNow), autoSave: true);

            var exOtherDraft = await Should.ThrowAsync<BusinessException>(() => piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = owner.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 20, WarehouseId = wh.Id, SerialAndBatchBundleId = otherDraftBundle.Id }
                }
            }));
            exOtherDraft.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var exCancelled = await Should.ThrowAsync<BusinessException>(() => piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = owner.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 20, WarehouseId = wh.Id, SerialAndBatchBundleId = cancelledBundle.Id }
                }
            }));
            exCancelled.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);

            var created = await piAppService.CreateAsync(new CreatePurchaseInvoiceDto
            {
                CompanyId = owner.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 20, WarehouseId = wh.Id, SerialAndBatchBundleId = unlinkedBundle.Id }
                }
            });
            created.Items.Single().SerialAndBatchBundleId.ShouldBe(unlinkedBundle.Id);

            var exUpdate = await Should.ThrowAsync<BusinessException>(() => piAppService.UpdateAsync(created.Id, new CreatePurchaseInvoiceDto
            {
                CompanyId = owner.Id,
                SupplierId = supplier.Id,
                IssueDate = DateTime.UtcNow.Date,
                Items = new List<CreatePurchaseInvoiceItemDto>
                {
                    new() { ItemId = item.Id, Description = "Item", Quantity = 1, UnitPrice = 20, WarehouseId = wh.Id, SerialAndBatchBundleId = otherDraftBundle.Id }
                }
            }));
            exUpdate.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var hijackedPi = new PurchaseInvoice(Guid.NewGuid(), owner.Id, supplier.Id, "PI-HIJACK", DateTime.UtcNow.Date);
            hijackedPi.AddItem(item.Id, "Item", 1, 20, 0, "Unit", wh.Id);
            hijackedPi.Items.Last().SerialAndBatchBundleId = otherDraftBundle.Id;
            await piRepo.InsertAsync(hijackedPi, autoSave: true);

            var exSubmit = await Should.ThrowAsync<BusinessException>(() => piAppService.SubmitAsync(hijackedPi.Id));
            exSubmit.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task StockReconciliationAndPickList_CreateAndSubmit_EnforceDraftBundleOwnership()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var (owner, other, item, wh) = await SetupBaseMasterDataAsync("SABB-INV");
            var fyRepo = GetRequiredService<IRepository<FiscalYear, Guid>>();
            var bundleRepo = GetRequiredService<IRepository<SerialAndBatchBundle, Guid>>();
            var srRepo = GetRequiredService<IRepository<StockReconciliation, Guid>>();
            var plRepo = GetRequiredService<IRepository<PickList, Guid>>();
            var srAppService = GetRequiredService<IStockReconciliationAppService>();
            var plAppService = GetRequiredService<IPickListAppService>();

            var today = DateTime.UtcNow.Date;
            await fyRepo.InsertAsync(new FiscalYear(
                Guid.NewGuid(), owner.Id, "FY-SABB", new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)), autoSave: true);

            var foreignBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), other.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "PickList", Guid.Empty, DateTime.UtcNow), autoSave: true);

            var otherSrBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Inward, "StockReconciliation", Guid.NewGuid(), DateTime.UtcNow), autoSave: true);

            var otherPlBundle = await bundleRepo.InsertAsync(new SerialAndBatchBundle(
                Guid.NewGuid(), owner.Id, item.Id, wh.Id,
                BundleTransactionType.Outward, "PickList", Guid.NewGuid(), DateTime.UtcNow), autoSave: true);

            var exSrNew = await Should.ThrowAsync<BusinessException>(() => srAppService.CreateAsync(new CreateStockReconciliationDto
            {
                CompanyId = owner.Id,
                PostingDate = today,
                Purpose = "Stock Reconciliation",
                Items =
                [
                    new() { ItemId = item.Id, WarehouseId = wh.Id, NewQuantity = 2, NewValuationRate = 10, SerialAndBatchBundleId = otherSrBundle.Id }
                ]
            }));
            exSrNew.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var exSrCurrent = await Should.ThrowAsync<BusinessException>(() => srAppService.CreateAsync(new CreateStockReconciliationDto
            {
                CompanyId = owner.Id,
                PostingDate = today,
                Purpose = "Stock Reconciliation",
                Items =
                [
                    new() { ItemId = item.Id, WarehouseId = wh.Id, NewQuantity = 2, NewValuationRate = 10, CurrentSerialAndBatchBundleId = otherSrBundle.Id }
                ]
            }));
            exSrCurrent.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var hijackedSr = new StockReconciliation(Guid.NewGuid(), owner.Id, today);
            hijackedSr.AddItem(item.Id, wh.Id, 2, 10, stockUom: "Unit", serialAndBatchBundleId: otherSrBundle.Id);
            await srRepo.InsertAsync(hijackedSr, autoSave: true);

            var exSrSubmit = await Should.ThrowAsync<BusinessException>(() => srAppService.SubmitAsync(hijackedSr.Id));
            exSrSubmit.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var exPlOtherDraft = await Should.ThrowAsync<BusinessException>(() => plAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = owner.Id,
                Purpose = "Delivery",
                Items =
                [
                    new() { ItemId = item.Id, WarehouseId = wh.Id, Qty = 1, SerialAndBatchBundleId = otherPlBundle.Id }
                ]
            }));
            exPlOtherDraft.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);

            var exPlForeign = await Should.ThrowAsync<BusinessException>(() => plAppService.CreateAsync(new CreatePickListDto
            {
                CompanyId = owner.Id,
                Purpose = "Delivery",
                Items =
                [
                    new() { ItemId = item.Id, WarehouseId = wh.Id, Qty = 1, SerialAndBatchBundleId = foreignBundle.Id }
                ]
            }));
            exPlForeign.Code.ShouldBe(MyERPDomainErrorCodes.CompanyRestrictionBlocked);

            var hijackedPl = new PickList(Guid.NewGuid(), owner.Id, "Delivery");
            hijackedPl.AddItem(item.Id, wh.Id, 1, serialAndBatchBundleId: otherPlBundle.Id);
            await plRepo.InsertAsync(hijackedPl, autoSave: true);

            var exPlSubmit = await Should.ThrowAsync<BusinessException>(() => plAppService.SubmitAsync(hijackedPl.Id));
            exPlSubmit.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }
}
