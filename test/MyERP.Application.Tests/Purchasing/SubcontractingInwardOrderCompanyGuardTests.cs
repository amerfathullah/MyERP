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
/// sweep: SubcontractingInwardOrderAppService.CreateAsync only ran items through
/// ItemTransactionValidationService.ValidateItemAsync (IsActive check only, not company ownership)
/// and never wired CompanyRestrictionValidationService, mirroring the same gap fixed in the
/// sibling SubcontractingAppService (Order/Receipt) this session.
/// </summary>
public abstract class SubcontractingInwardOrderCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ItemFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var supplierRepository = GetRequiredService<IRepository<Supplier, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var scioAppService = GetRequiredService<ISubcontractingInwardOrderAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "SCIO Guard Other Co"), autoSave: true);

            var supplier = await supplierRepository.InsertAsync(new Supplier(Guid.NewGuid(), ownerCompany.Id, "SCIO Guard Supplier"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "SCIO-GUARD-1", "SCIO Guard Item", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                scioAppService.CreateAsync(new CreateSubcontractingInwardOrderDto
                {
                    CompanyId = ownerCompany.Id,
                    SupplierId = supplier.Id,
                    OrderDate = DateTime.UtcNow,
                    Items = new List<CreateScioItemDto>
                    {
                        new() { ItemId = item.Id, Quantity = 1m, Rate = 10m }
                    }
                }));
        });
    }
}
