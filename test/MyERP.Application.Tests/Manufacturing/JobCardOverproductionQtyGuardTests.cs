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

    [Fact]
    public async Task CreateAsync_SecondJobCardAllowedWhenFirstHasPendingQty()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();
            var jobCardAppService = GetRequiredService<IJobCardAppService>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Pending Qty Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCP1", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCP1", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCP1", fgItem.Id, bom.Id, quantity: 100m);
            wo.Submit();
            await woRepository.InsertAsync(wo, autoSave: true);

            var operationId = Guid.NewGuid();

            var firstJcDto = await jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = operationId,
                ForQuantity = 60m,
            });

            // Mark 40 units pending on first job card (so only 60 - 40 = 20 units count against allowance)
            var firstJc = await jcRepository.GetAsync(firstJcDto.Id);
            firstJc.SetPendingQty(40m);
            await jcRepository.UpdateAsync(firstJc, autoSave: true);

            // Creating second Job Card with 60 units now succeeds: 20 + 60 = 80 <= 105 (ERPNext PR #58466)
            var secondJcDto = await jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = company.Id,
                WorkOrderId = wo.Id,
                OperationId = operationId,
                ForQuantity = 60m,
            });

            secondJcDto.ForQuantity.ShouldBe(60m);
        });
    }

    [Fact]
    public async Task CompleteAsync_PreviousOperationCompletedWithLoss_DropsExcessFromSecondCardLossAndForQty()
    {
        Guid jc2Id = default;
        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var fyRepository = GetRequiredService<IRepository<MyERP.Accounting.Entities.FiscalYear, Guid>>();
            var seriesRepository = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            var whRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Warehouse, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Prev Loss Co"), autoSave: true);
            await fyRepository.InsertAsync(new MyERP.Accounting.Entities.FiscalYear(
                Guid.NewGuid(), company.Id, "FY26", new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)), autoSave: true);
            await seriesRepository.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "SE Series", "SE", "SE-JCL-"), autoSave: true);
            var fgWh = await whRepository.InsertAsync(new MyERP.Inventory.Entities.Warehouse(Guid.NewGuid(), company.Id, "FG WH JCL"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCL1", "Widget", MyERP.Inventory.ItemType.Goods) { MaintainStock = true }, autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCL1", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCL1", fgItem.Id, bom.Id, quantity: 10m)
            {
                FgWarehouseId = fgWh.Id
            };
            wo.Submit();
            wo.Start();
            await woRepository.InsertAsync(wo, autoSave: true);

            var t0 = new DateTime(2026, 7, 1, 8, 0, 0);
            var jc1 = new JobCard(Guid.NewGuid(), company.Id, wo.Id, op1, forQuantity: 10m, sequenceId: 1);
            jc1.Start();
            jc1.AddTimeLog(t0, t0.AddHours(1), 8m);
            jc1.Complete(); // PendingQty=0, CompletedQty=8, ProcessLossQty=2 -> prev operation done with 2 loss
            await jcRepository.InsertAsync(jc1, autoSave: true);

            var jc2 = new JobCard(Guid.NewGuid(), company.Id, wo.Id, op2, forQuantity: 10m, sequenceId: 2);
            jc2.Start();
            jc2.AddTimeLog(t0.AddHours(2), t0.AddHours(3), 8m);
            await jcRepository.InsertAsync(jc2, autoSave: true);
            jc2Id = jc2.Id;
        });

        var jobCardAppService = GetRequiredService<IJobCardAppService>();
        var completed2 = await jobCardAppService.CompleteAsync(jc2Id);

        // Previous operation's 2 loss units are dropped from jc2.ProcessLossQty (2 -> 0) and jc2.ForQuantity (10 -> 8)
        completed2.CompletedQty.ShouldBe(8m);
        completed2.ProcessLossQty.ShouldBe(0m);
        completed2.ForQuantity.ShouldBe(8m);
        completed2.Status.ShouldBe((int)JobCardStatus.Completed);
    }

    [Fact]
    public async Task CompleteAsync_PreviousOperationHasPendingQty_ThrowsSequenceErrorInsteadOfClaimingLoss()
    {
        Guid jc2Id = default;
        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Prev Pend Co"), autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCP2", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCP2", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCP2", fgItem.Id, bom.Id, quantity: 10m);
            wo.Submit();
            wo.Start();
            await woRepository.InsertAsync(wo, autoSave: true);

            var t0 = new DateTime(2026, 7, 2, 8, 0, 0);
            var jc1 = new JobCard(Guid.NewGuid(), company.Id, wo.Id, op1, forQuantity: 10m, sequenceId: 1);
            jc1.Start();
            jc1.AddTimeLog(t0, t0.AddHours(1), 5m);
            jc1.SetPendingQty(5m);
            jc1.Complete(); // Still has 5m pending to make!
            await jcRepository.InsertAsync(jc1, autoSave: true);

            var jc2 = new JobCard(Guid.NewGuid(), company.Id, wo.Id, op2, forQuantity: 10m, sequenceId: 2);
            jc2.Start();
            jc2.AddTimeLog(t0.AddHours(2), t0.AddHours(3), 4m); // jc2.Complete() would compute ProcessLossQty = 6 -> 4 + 6 = 10 > 5
            await jcRepository.InsertAsync(jc2, autoSave: true);
            jc2Id = jc2.Id;
        });

        var jobCardAppService = GetRequiredService<IJobCardAppService>();
        var ex = await Should.ThrowAsync<BusinessException>(() => jobCardAppService.CompleteAsync(jc2Id));
        ex.Code.ShouldBe("MyERP:10020");
    }

    [Fact]
    public async Task CreateAsync_ExtraJobCardAfterOperationComplete_Throws()
    {
        Guid companyId = default;
        Guid woId = default;
        var opId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepository = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepository = GetRequiredService<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
            var bomRepository = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var woRepository = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var jcRepository = GetRequiredService<IRepository<JobCard, Guid>>();
            var settingsRepository = GetRequiredService<IRepository<ManufacturingSettings, Guid>>();

            var company = await companyRepository.InsertAsync(new Company(Guid.NewGuid(), "JC Complete Guard Co"), autoSave: true);
            companyId = company.Id;
            await settingsRepository.InsertAsync(new ManufacturingSettings(Guid.NewGuid(), company.Id) { OverproductionPercentage = 0m }, autoSave: true);
            var fgItem = await itemRepository.InsertAsync(
                new MyERP.Inventory.Entities.Item(Guid.NewGuid(), company.Id, "FG-JCC1", "Widget", MyERP.Inventory.ItemType.Goods), autoSave: true);
            var bom = await bomRepository.InsertAsync(
                new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-JCC1", fgItem.Id), autoSave: true);

            var wo = new WorkOrder(Guid.NewGuid(), company.Id, "WO-JCC1", fgItem.Id, bom.Id, quantity: 10m);
            wo.Submit();
            wo.Start();
            await woRepository.InsertAsync(wo, autoSave: true);
            woId = wo.Id;

            var t0 = new DateTime(2026, 7, 3, 8, 0, 0);
            var jc1 = new JobCard(Guid.NewGuid(), company.Id, wo.Id, opId, forQuantity: 10m, sequenceId: 1);
            jc1.Start();
            jc1.AddTimeLog(t0, t0.AddHours(1), 10m);
            jc1.Complete();
            await jcRepository.InsertAsync(jc1, autoSave: true);
        });

        var jobCardAppService = GetRequiredService<IJobCardAppService>();
        await Should.ThrowAsync<BusinessException>(() =>
            jobCardAppService.CreateAsync(new CreateJobCardDto
            {
                CompanyId = companyId,
                WorkOrderId = woId,
                OperationId = opId,
                ForQuantity = 10m,
            }));
    }
}

