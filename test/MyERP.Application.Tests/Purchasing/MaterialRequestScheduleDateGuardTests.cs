using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Purchasing.DTOs;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Purchasing;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's
/// buying_controller.py validate_schedule_date() throws "schedule_date cannot be before
/// transaction_date," called from Material Request's validate() (every save) and again from
/// before_update_after_submit() (every post-submit change). MaterialRequestAppService had no such
/// check at all — CreateAsync and SubmitAsync accepted a RequiredByDate before RequestDate with no
/// error.
/// </summary>
public abstract class MaterialRequestScheduleDateGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_RequiredByDateBeforeRequestDate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Schedule Date Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-SCHED-1", "MR Schedule Item", ItemType.Goods), autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() =>
                mrAppService.CreateAsync(new CreateMaterialRequestDto
                {
                    CompanyId = company.Id,
                    RequestType = MaterialRequestType.Purchase,
                    RequestDate = DateTime.Today,
                    RequiredByDate = DateTime.Today.AddDays(-5),
                    Items = new List<CreateMaterialRequestItemDto>
                    {
                        new() { ItemId = item.Id, ItemName = "MR Schedule Item", Quantity = 5m, Uom = "Unit" }
                    }
                }));
        });
    }

    [Fact]
    public async Task SubmitAsync_RequiredByDateEditedBeforeRequestDate_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var mrRepository = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Schedule Date Co 2"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-SCHED-2", "MR Schedule Item 2", ItemType.Goods), autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-SCHED-001", MaterialRequestType.Purchase, DateTime.Today, company.TenantId);
            mr.AddItem(item.Id, "MR Schedule Item 2", quantity: 5m, uom: "Unit");
            mr.RequiredByDate = DateTime.Today.AddDays(-2); // edited to precede RequestDate before submit
            await mrRepository.InsertAsync(mr, autoSave: true);

            await Should.ThrowAsync<Volo.Abp.BusinessException>(() => mrAppService.SubmitAsync(mr.Id));
        });
    }

    [Fact]
    public async Task CreateAsync_RequiredByDateOnOrAfterRequestDate_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<Item, Guid>>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "MR Schedule Date Happy Co"), autoSave: true);
            var item = await itemRepository.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "MR-SCHED-3", "MR Schedule Item 3", ItemType.Goods), autoSave: true);

            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepository.InsertAsync(
                new DocumentSeries(Guid.NewGuid(), company.Id, "MR Series", "MR", "MR-"), autoSave: true);

            var dto = await mrAppService.CreateAsync(new CreateMaterialRequestDto
            {
                CompanyId = company.Id,
                RequestType = MaterialRequestType.Purchase,
                RequestDate = DateTime.Today,
                RequiredByDate = DateTime.Today,
                Items = new List<CreateMaterialRequestItemDto>
                {
                    new() { ItemId = item.Id, ItemName = "MR Schedule Item 3", Quantity = 5m, Uom = "Unit" }
                }
            });

            dto.Id.ShouldNotBe(Guid.Empty);
        });
    }
}
