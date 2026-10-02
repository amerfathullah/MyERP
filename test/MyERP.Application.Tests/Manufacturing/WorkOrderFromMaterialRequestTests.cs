using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace MyERP.Manufacturing;

public abstract class WorkOrderFromMaterialRequestTests<TStartupModule> : MyERPApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public async Task CreateWorkOrdersFromMaterialRequest_WhenDraft_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MFG MR Test Co 1"), autoSave: true);
            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-DRAFT-001", MaterialRequestType.Manufacture, DateTime.UtcNow);
            mr.AddItem(Guid.NewGuid(), "FG Item", 5, "Unit");
            await mrRepo.InsertAsync(mr, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                mfgAppService.CreateWorkOrdersFromMaterialRequestAsync(mr.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
        });
    }

    [Fact]
    public async Task CreateWorkOrdersFromMaterialRequest_WhenNotManufacture_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MFG MR Test Co 2"), autoSave: true);
            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-PURCHASE-001", MaterialRequestType.Purchase, DateTime.UtcNow);
            mr.AddItem(Guid.NewGuid(), "Raw Mat", 5, "Unit");
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                mfgAppService.CreateWorkOrdersFromMaterialRequestAsync(mr.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateWorkOrdersFromMaterialRequest_WhenStopped_Throws()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MFG MR Test Co 3"), autoSave: true);
            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-STOPPED-001", MaterialRequestType.Manufacture, DateTime.UtcNow);
            mr.AddItem(Guid.NewGuid(), "FG Item", 5, "Unit");
            mr.Submit();
            mr.Stop();
            await mrRepo.InsertAsync(mr, autoSave: true);

            var ex = await Should.ThrowAsync<BusinessException>(() =>
                mfgAppService.CreateWorkOrdersFromMaterialRequestAsync(mr.Id));
            ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        });
    }

    [Fact]
    public async Task CreateWorkOrdersFromMaterialRequest_Success_AndReversalOnCancel()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var companyRepo = GetRequiredService<IRepository<Company, Guid>>();
            var itemRepo = GetRequiredService<IRepository<Item, Guid>>();
            var warehouseRepo = GetRequiredService<IRepository<Warehouse, Guid>>();
            var bomRepo = GetRequiredService<IRepository<BillOfMaterials, Guid>>();
            var mrRepo = GetRequiredService<IRepository<MaterialRequest, Guid>>();
            var woRepo = GetRequiredService<IRepository<WorkOrder, Guid>>();
            var mfgAppService = GetRequiredService<IManufacturingAppService>();
            var mrAppService = GetRequiredService<IMaterialRequestAppService>();

            var company = await companyRepo.InsertAsync(new Company(Guid.NewGuid(), "MFG MR Flow Co"), autoSave: true);
            var wipWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "WIP Wh Flow"), autoSave: true);
            var fgWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "FG Wh Flow"), autoSave: true);
            var rmWh = await warehouseRepo.InsertAsync(new Warehouse(Guid.NewGuid(), company.Id, "RM Wh Flow"), autoSave: true);

            company.DefaultWipWarehouseId = wipWh.Id;
            company.DefaultFgWarehouseId = fgWh.Id;
            await companyRepo.UpdateAsync(company, autoSave: true);

            var seriesRepo = GetRequiredService<IRepository<DocumentSeries, Guid>>();
            await seriesRepo.InsertAsync(new DocumentSeries(Guid.NewGuid(), company.Id, "Work Order Series", "WO", "WO-"), autoSave: true);

            var fgItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "FG-MR-FLOW-01", "FG Item Flow", ItemType.Goods), autoSave: true);
            var rmItem = await itemRepo.InsertAsync(
                new Item(Guid.NewGuid(), company.Id, "RM-MR-FLOW-01", "RM Item Flow", ItemType.Goods), autoSave: true);

            var bom = new BillOfMaterials(Guid.NewGuid(), company.Id, "BOM-MR-FLOW-01", fgItem.Id)
            {
                Quantity = 1,
                IsActive = true,
                IsDefault = true,
                SourceWarehouseId = rmWh.Id,
                TargetWarehouseId = fgWh.Id,
            };
            bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, rmItem.Id, "RM Item Flow", 2, 10));
            await bomRepo.InsertAsync(bom, autoSave: true);

            var mr = new MaterialRequest(Guid.NewGuid(), company.Id, "MR-MFG-001", MaterialRequestType.Manufacture, DateTime.UtcNow)
            {
                RequiredByDate = DateTime.UtcNow.AddDays(7),
            };
            mr.AddItem(fgItem.Id, fgItem.ItemName, 10, "Unit", fgWh.Id);
            mr.Submit();
            await mrRepo.InsertAsync(mr, autoSave: true);

            var mrItemId = mr.Items[0].Id;

            // 1. Create Work Orders via Manufacturing AppService
            var batchResult = await mfgAppService.CreateWorkOrdersFromMaterialRequestAsync(mr.Id);
            batchResult.ShouldNotBeNull();
            batchResult.CreatedCount.ShouldBe(1);
            batchResult.SkippedCount.ShouldBe(0);
            batchResult.WorkOrders.Count.ShouldBe(1);

            var createdWoInfo = batchResult.WorkOrders[0];
            var createdWo = await woRepo.GetAsync(createdWoInfo.WorkOrderId, includeDetails: true);
            createdWo.ShouldNotBeNull();
            createdWo.ItemId.ShouldBe(fgItem.Id);
            createdWo.BomId.ShouldBe(bom.Id);
            createdWo.Quantity.ShouldBe(10);
            createdWo.MaterialRequestId.ShouldBe(mr.Id);
            createdWo.MaterialRequestItemId.ShouldBe(mrItemId);
            createdWo.FgWarehouseId.ShouldBe(fgWh.Id);

            // Verify Material Request item ordered qty updated
            var reloadedMr = await mrRepo.GetAsync(mr.Id, includeDetails: true);
            var reloadedItem = reloadedMr.Items.First(i => i.Id == mrItemId);
            reloadedItem.OrderedQuantity.ShouldBe(10);

            // 2. Calling RaiseWorkOrdersAsync via IMaterialRequestAppService when fully ordered should skip
            var secondBatchResult = await mrAppService.RaiseWorkOrdersAsync(mr.Id);
            secondBatchResult.CreatedCount.ShouldBe(0);
            secondBatchResult.SkippedCount.ShouldBe(1);

            // 3. Cancel WorkOrder should reverse MR item fulfillment
            await mfgAppService.CancelWorkOrderAsync(createdWo.Id);

            var reloadedMrAfterCancel = await mrRepo.GetAsync(mr.Id, includeDetails: true);
            var reloadedItemAfterCancel = reloadedMrAfterCancel.Items.First(i => i.Id == mrItemId);
            reloadedItemAfterCancel.OrderedQuantity.ShouldBe(0);
        });
    }
}
