using System;
using System.Collections.Generic;
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

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check
/// sweep: SubcontractingAppService.CreateOrderAsync/CreateReceiptAsync only validated Items via
/// ItemTransactionValidationService (which just checks IsActive, not company ownership) and never
/// wired CompanyRestrictionValidationService — unlike PurchaseOrder/PurchaseReceipt, which both do.
/// A cross-company Supplier or Item could be used on a Subcontracting Order/Receipt with no guard.
/// </summary>
public abstract class SubcontractingCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateOrderAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var subcontractingAppService = GetRequiredService<ISubcontractingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCO Guard Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCO Guard Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCO-GUARD-1", "SCO Guard Item", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                subcontractingAppService.CreateOrderAsync(new CreateSubcontractingOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScoItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "Cross-co item", Qty = 1m, Rate = 10m }
                    }
                }));
        });
    }
}
