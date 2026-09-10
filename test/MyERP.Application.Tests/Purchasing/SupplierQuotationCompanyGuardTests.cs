using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: every other Purchasing
/// document (PurchaseOrder, PurchaseInvoice, PurchaseReceipt, MaterialRequest, RequestForQuotation)
/// wires CompanyRestrictionValidationService into CreateAsync; Supplier Quotation — the RFQ's own
/// direct reply, and the document a Purchase Order conversion reads Supplier/Item references from
/// — was the one that didn't.
/// </summary>
public abstract class SupplierQuotationCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_SupplierFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<MyERP.Purchasing.Entities.Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var sqAppService = GetRequiredService<ISupplierQuotationAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SQ Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SQ Guard Other Co"), autoSave: true);

            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "SQ-ITEM-001", "SQ Guard Item", ItemType.Goods), autoSave: true);
            var supplier = await supplierRepository.InsertAsync(
                new MyERP.Purchasing.Entities.Supplier(Guid.NewGuid(), otherCompany.Id, "SQ Guard Cross-Co Supplier"), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                sqAppService.CreateAsync(new CreateSupplierQuotationDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    TransactionDate = DateTime.Today,
                    Items =
                    [
                        new CreateSQItemDto { ItemId = item.Id, ItemName = "Widget", Qty = 5m, Rate = 10m }
                    ]
                }));
        });
    }
}
