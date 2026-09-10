using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for a real gap found via a CompanyRestrictionValidationService spot-check:
/// CreateBomAsync/UpdateBomAsync never wired the check at all, despite referencing the FG item,
/// every raw-material item, and up to three warehouses — a cross-company reference here propagates
/// silently into every downstream Work Order/Job Card built from the BOM.
/// </summary>
public abstract class BomCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateBomAsync_RawMaterialFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var manufacturingAppService = GetRequiredService<IManufacturingAppService>();

            var ownerCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BOM Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "BOM Guard Other Co"), autoSave: true);

            var fgItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "BOM-GUARD-FG", "BOM Guard FG", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), otherCompany.Id, "BOM-GUARD-RM", "BOM Guard Cross-Co RM", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                manufacturingAppService.CreateBomAsync(new CreateBomDto
                {
                    CompanyId = ownerCompany.Id,
                    ItemId = fgItem.Id,
                    Quantity = 1,
                    Items = new List<CreateBomItemDto>
                    {
                        new() { ItemId = rmItem.Id, ItemName = "Cross-co RM", Quantity = 1m, Rate = 10m }
                    }
                }));
        });
    }
}
