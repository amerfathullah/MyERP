using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class JobCardCompanyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_WorkstationFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var wsRepo = GetRequiredService<IRepository<Workstation, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Guard Owner Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Guard Other Co"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "JC-GUARD-FG", "JC Guard FG", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-JC-GUARD", fgItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), ownerCompany.Id, "WO-JC-GUARD", fgItem.Id, bom.Id, 10);
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var crossCoWs = await wsRepo.InsertAsync(
                new Workstation(Guid.NewGuid(), otherCompany.Id, "Cross-Co Workstation"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                jobCardAppService.CreateAsync(new CreateJobCardDto
                {
                    CompanyId = ownerCompany.Id,
                    WorkOrderId = wo.Id,
                    OperationId = Guid.NewGuid(),
                    ForQuantity = 5,
                    SequenceId = 1,
                    WorkstationId = crossCoWs.Id
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WorkOrderStoppedOrClosed_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var wsRepo = GetRequiredService<IRepository<Workstation, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Stopped Owner Co"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "JC-STOP-FG", "JC Stop FG", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-JC-STOP", fgItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), ownerCompany.Id, "WO-JC-STOP", fgItem.Id, bom.Id, 10);
            wo.Submit();
            wo.Start();
            wo.Stop();
            await woRepo.InsertAsync(wo, autoSave: true);

            var ws = await wsRepo.InsertAsync(
                new Workstation(Guid.NewGuid(), ownerCompany.Id, "Owner Workstation"), autoSave: true);

            await Should.ThrowAsync<BusinessException>(() =>
                jobCardAppService.CreateAsync(new CreateJobCardDto
                {
                    CompanyId = ownerCompany.Id,
                    WorkOrderId = wo.Id,
                    OperationId = Guid.NewGuid(),
                    ForQuantity = 5,
                    SequenceId = 1,
                    WorkstationId = ws.Id
                }));
        });
    }

    [Fact]
    public async Task UpdateAsync_WorkstationFromDifferentCompany_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var wsRepo = GetRequiredService<IRepository<Workstation, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var ownerCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Update Owner Co"), autoSave: true);
            var otherCompany = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "JC Update Other Co"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), ownerCompany.Id, "JC-UPD-FG", "JC Upd FG", ItemType.Goods), autoSave: true);
            var bom = await bomRepo.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), ownerCompany.Id, "BOM-JC-UPD", fgItem.Id) { Quantity = 1, IsActive = true }, autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), ownerCompany.Id, "WO-JC-UPD", fgItem.Id, bom.Id, 10);
            wo.Submit();
            await woRepo.InsertAsync(wo, autoSave: true);

            var ownerWs = await wsRepo.InsertAsync(
                new Workstation(Guid.NewGuid(), ownerCompany.Id, "Owner WS"), autoSave: true);
            var crossCoWs = await wsRepo.InsertAsync(
                new Workstation(Guid.NewGuid(), otherCompany.Id, "Cross-Co WS Upd"), autoSave: true);

            var jc = await jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = ownerCompany.Id,
                WorkOrderId = wo.Id,
                OperationId = Guid.NewGuid(),
                ForQuantity = 5,
                SequenceId = 1,
                WorkstationId = ownerWs.Id
            });

            await Should.ThrowAsync<BusinessException>(() =>
                jobCardAppService.UpdateAsync(jc.Id, new CreateJobCardDto
                {
                    CompanyId = ownerCompany.Id,
                    WorkOrderId = wo.Id,
                    OperationId = jc.OperationId,
                    ForQuantity = 5,
                    SequenceId = 1,
                    WorkstationId = crossCoWs.Id
                }));
        });
    }
}
