using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Regression coverage for a real gap found via ERPNext validate() parity: ERPNext's Job Card
/// validate_job_card_qty/get_allowed_wo_qty rejects planning more Job Card quantity for a Work
/// Order operation than the Work Order's quantity plus the configured overproduction percentage
/// allows. MyERP's JobCardAppService.CreateAsync/UpdateAsync applied ForQuantity with no such
/// check at all — a Job Card could be created (or edited) with a quantity far beyond what the
/// Work Order calls for, with the only overproduction guard living at production-completion time
/// (WorkOrder.RecordProduction), which is too late to stop over-planning.
/// </summary>
public abstract class JobCardOverproductionQtyGuardTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateAsync_ExceedsWorkOrderQtyPlusOverproduction_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Qty Guard Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCQ1", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCQ1", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCQ1", fgItem.Id, bom.Id, quantity: 100m);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            var operationId = Guid.NewGuid();

            // Default overproduction is 5% (no ManufacturingSettings row), so allowed qty is 105.
            await Should.ThrowAsync<BusinessException>(() =>
                jobCardAppService.CreateAsync(new CreateJobCardDto
                {
                    CompanyId = company.Id,
                    WorkOrderId = wo.Id,
                    OperationId = operationId,
                    ForQuantity = 200m,
                }));
        });
    }

    [Fact]
    public async Task CreateAsync_WithinWorkOrderQtyPlusOverproduction_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Qty Happy Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCQ2", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCQ2", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCQ2", fgItem.Id, bom.Id, quantity: 100m);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            var dto = await jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = Guid.NewGuid(),
                ForQuantity = 100m,
            });

            dto.ForQuantity.ShouldBe(100m);
        });
    }

    [Fact]
    public async Task CreateAsync_SecondJobCardOnSameOperationPushesOverAllowed_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Qty Stack Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCQ3", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCQ3", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCQ3", fgItem.Id, bom.Id, quantity: 100m);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            var operationId = Guid.NewGuid();

            await jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = operationId,
                ForQuantity = 60m,
            });

            // 60 + 60 = 120 > 105 (100 + 5% default overproduction).
            await Should.ThrowAsync<BusinessException>(() =>
                jobCardAppService.CreateAsync(new CreateJobCardDto
                {
                    CompanyId = company.Id,
                    WorkOrderId = wo.Id,
                    OperationId = operationId,
                    ForQuantity = 60m,
                }));
        });
    }
}
