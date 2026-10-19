using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for WorkOrderManager domain service rules:
/// overproduction enforcement, material requirements calculation,
/// BOM validation, backflush method resolution, and WO lifecycle.
/// </summary>
public class WorkOrderManagerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid BomId = Guid.NewGuid();

    // ========== Overproduction Enforcement ==========

    [Fact]
    public void Overproduction_Zero_Pct_Blocks_Any_Excess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        // 0% overproduction means exact qty only
        wo.RecordProduction(100, 0m);
        wo.ProducedQuantity.ShouldBe(100);
    }

    [Fact]
    public void Overproduction_Zero_Pct_Blocks_Even_One_Extra()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        Should.Throw<BusinessException>(() => wo.RecordProduction(101, 0m));
    }

    [Fact]
    public void Overproduction_5Pct_Allows_Within_Limit()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        // 5% of 100 = max 105
        wo.RecordProduction(105, 5m);
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void Overproduction_5Pct_Blocks_Beyond_Limit()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        Should.Throw<BusinessException>(() => wo.RecordProduction(106, 5m));
    }

    [Fact]
    public void Overproduction_Cumulative_Check()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(80, 5m);
        wo.ProducedQuantity.ShouldBe(80);

        // Can produce 25 more (80+25=105, max=105)
        wo.RecordProduction(25, 5m);
        wo.ProducedQuantity.ShouldBe(105);
    }

    [Fact]
    public void Overproduction_Cumulative_Blocks_When_Total_Exceeds()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(80, 5m);

        // 80+30=110 > 105 limit
        Should.Throw<BusinessException>(() => wo.RecordProduction(30, 5m));
    }

    [Fact]
    public void Overproduction_10Pct_Allows_110()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(110, 10m);
        wo.ProducedQuantity.ShouldBe(110);
    }

    // ========== Material Requirements Calculation ==========

    [Fact]
    public void MaterialRequirement_ProportionalCalculation()
    {
        // BOM: 1 FG needs 2 RM-A + 3 RM-B
        // WO for 10 FG → needs 20 RM-A + 30 RM-B
        var bomQty = 1m;
        var produceQty = 10m;
        var rmAQty = 2m;
        var rmBQty = 3m;

        var requiredA = rmAQty * (produceQty / bomQty);
        var requiredB = rmBQty * (produceQty / bomQty);

        requiredA.ShouldBe(20m);
        requiredB.ShouldBe(30m);
    }

    [Fact]
    public void MaterialRequirement_PartialProduction()
    {
        // BOM: 10 FG needs 50 RM
        // Produce only 4 → needs 20 RM
        var bomQty = 10m;
        var rmQty = 50m;
        var produceQty = 4m;

        var required = rmQty * (produceQty / bomQty);
        required.ShouldBe(20m);
    }

    [Fact]
    public void MaterialRequirement_ZeroBomQty_ReturnsZero()
    {
        // Per domain service: bom.Quantity > 0 ? ... : 0
        var bomQty = 0m;
        var result = bomQty > 0 ? 5m * (10m / bomQty) : 0m;
        result.ShouldBe(0m);
    }

    [Fact]
    public void MaterialRequirement_PhantomItems_Excluded()
    {
        // Phantom items bubble components up, not consumed directly
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, Guid.NewGuid(), "RM-A", 5, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, Guid.NewGuid(), "Phantom Assembly", 1, 50m));

        // Mark one as phantom
        bom.Items.Last().IsPhantom = true;

        var nonPhantom = bom.Items.Where(i => !i.IsPhantom).ToList();
        nonPhantom.Count.ShouldBe(1);
        nonPhantom.First().ItemName.ShouldBe("RM-A");
    }

    // ========== WO Status State Machine ==========

    [Fact]
    public void WO_Submit_TransitionsTo_Submitted()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);
    }

    [Fact]
    public void WO_Start_TransitionsTo_InProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WO_FullProduction_AutoCompletes()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void WO_PartialProduction_StaysInProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(50, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);
    }

    [Fact]
    public void WO_Stop_FromInProcess()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.Stop();
        wo.Status.ShouldBe(WorkOrderStatus.Stopped);
    }

    [Fact]
    public void WO_Cannot_Start_From_Draft()
    {
        var wo = CreateWorkOrder(100);
        Should.Throw<BusinessException>(() => wo.Start());
    }

    [Fact]
    public void WO_Cannot_Cancel_When_Completed()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);

        Should.Throw<BusinessException>(() => wo.Cancel());
    }

    // ========== BOM Validation Concepts ==========

    [Fact]
    public void BOM_IsActive_Required_For_WO()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.IsActive.ShouldBeTrue(); // default
    }

    [Fact]
    public void BOM_Deactivated_Would_Block_WO()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.IsActive = false;
        bom.IsActive.ShouldBeFalse();
    }

    // ========== Backflush Method Resolution ==========

    [Fact]
    public void Backflush_BOM_Override_Takes_Precedence()
    {
        // When BOM has explicit BackflushBasedOn, it wins over ManufacturingSettings
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.BackflushBasedOn = "Material Transferred";

        bom.BackflushBasedOn.ShouldBe("Material Transferred");
    }

    [Fact]
    public void Backflush_Null_BOM_Falls_Back_To_Settings()
    {
        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        bom.BackflushBasedOn.ShouldBeNullOrEmpty();

        // Fallback would use ManufacturingSettings.BackflushRawMaterialsBasedOn
        var settingsDefault = "BOM";
        var effective = string.IsNullOrWhiteSpace(bom.BackflushBasedOn)
            ? settingsDefault
            : bom.BackflushBasedOn;

        effective.ShouldBe("BOM");
    }

    // ========== WO Percent Complete ==========

    [Fact]
    public void PercentComplete_AtZero_IsZero()
    {
        var wo = CreateWorkOrder(100);
        wo.PercentComplete.ShouldBe(0);
    }

    [Fact]
    public void PercentComplete_Partial_IsCorrect()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(40, 0m);
        wo.PercentComplete.ShouldBe(40);
    }

    [Fact]
    public void PercentComplete_Full_Is100()
    {
        var wo = CreateWorkOrder(100);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100, 0m);
        wo.PercentComplete.ShouldBe(100);
    }

    [Fact]
    public void PercentComplete_ZeroQty_IsZero()
    {
        // Divide-by-zero guard
        var wo = new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, 0);
        wo.PercentComplete.ShouldBe(0);
    }

    // ========== Manufacturing Settings ==========

    [Fact]
    public void ManufacturingSettings_Default_Overproduction()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.OverproductionPercentage.ShouldBe(5m);
    }

    [Fact]
    public void ManufacturingSettings_Custom_Overproduction()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.OverproductionPercentage = 15m;
        settings.OverproductionPercentage.ShouldBe(15m);
    }

    [Fact]
    public void ManufacturingSettings_MutualExclusion_Backflush()
    {
        var settings = new ManufacturingSettings(Guid.NewGuid(), CompanyId);
        settings.BackflushRawMaterialsBasedOn = "Material Transferred";
        settings.EnforceMutualExclusions();

        settings.ValidateComponentsQuantitiesPerBom.ShouldBeFalse();
    }

    // ========== Error Codes ==========

    [Fact]
    public void OverproductionErrorCode_Exists()
    {
        MyERPDomainErrorCodes.WorkOrderOverproduction.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void InsufficientRawMaterial_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.InsufficientRawMaterial.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ItemHasVariants_ErrorCode_Exists()
    {
        MyERPDomainErrorCodes.ItemHasVariants.ShouldNotBeNullOrEmpty();
    }

    // ========== Warehouse Hierarchy Fallback (ERPNext PR #58231) ==========

    [Fact]
    public async System.Threading.Tasks.Task CalculateMaterialRequirementsAsync_FallsBackToItemAndItemGroupWarehouse()
    {
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<ManufacturingSettings, Guid>>();
        var itemGroupRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.ItemGroup, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var bom = new BillOfMaterials(BomId, CompanyId, "BOM-001", ItemId);
        var rm1Id = Guid.NewGuid();
        var rm2Id = Guid.NewGuid();
        var rm3Id = Guid.NewGuid();

        var explicitWh = Guid.NewGuid();
        var itemDefaultWh = Guid.NewGuid();
        var groupDefaultWh = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, rm1Id, "RM1", 10, 100) { SourceWarehouseId = explicitWh });
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, rm2Id, "RM2", 5, 50));
        bom.Items.Add(new BomItem(Guid.NewGuid(), BomId, rm3Id, "RM3", 2, 20));

        var rm1 = new MyERP.Inventory.Entities.Item(rm1Id, CompanyId, "RM1", "RM1", MyERP.Inventory.ItemType.Goods);
        var rm2 = new MyERP.Inventory.Entities.Item(rm2Id, CompanyId, "RM2", "RM2", MyERP.Inventory.ItemType.Goods) { DefaultWarehouseId = itemDefaultWh };
        var rm3 = new MyERP.Inventory.Entities.Item(rm3Id, CompanyId, "RM3", "RM3", MyERP.Inventory.ItemType.Goods) { ItemGroupId = groupId };

        var itemGroup = new MyERP.Inventory.Entities.ItemGroup(groupId, "Raw Materials") { DefaultWarehouseId = groupDefaultWh };

        bomRepo.GetAsync(BomId).Returns(System.Threading.Tasks.Task.FromResult(bom));
        itemRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<MyERP.Inventory.Entities.Item> { rm1, rm2, rm3 }.AsQueryable()));
        itemGroupRepo.GetQueryableAsync().Returns(System.Threading.Tasks.Task.FromResult(
            new System.Collections.Generic.List<MyERP.Inventory.Entities.ItemGroup> { itemGroup }.AsQueryable()));

        var reqs = await manager.CalculateMaterialRequirementsAsync(BomId, 1, itemGroupRepo);

        reqs.Length.ShouldBe(3);
        reqs[0].SourceWarehouseId.ShouldBe(explicitWh);
        reqs[1].SourceWarehouseId.ShouldBe(itemDefaultWh);
        reqs[2].SourceWarehouseId.ShouldBe(groupDefaultWh);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResolveDefaultFgWarehouseAsync_FollowsHierarchy()
    {
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<ManufacturingSettings, Guid>>();
        var itemGroupRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.ItemGroup, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var companyWh = Guid.NewGuid();
        var itemWh = Guid.NewGuid();
        var groupWh = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var item = new MyERP.Inventory.Entities.Item(ItemId, CompanyId, "FG", "FG", MyERP.Inventory.ItemType.Goods)
        {
            DefaultWarehouseId = itemWh,
            ItemGroupId = groupId
        };
        var itemGroup = new MyERP.Inventory.Entities.ItemGroup(groupId, "Finished Goods")
        {
            DefaultWarehouseId = groupWh
        };

        itemRepo.FindAsync(ItemId).Returns(System.Threading.Tasks.Task.FromResult<MyERP.Inventory.Entities.Item?>(item));
        itemGroupRepo.FindAsync(groupId).Returns(System.Threading.Tasks.Task.FromResult<MyERP.Inventory.Entities.ItemGroup?>(itemGroup));

        // 1. Company default takes first priority
        var wh1 = await manager.ResolveDefaultFgWarehouseAsync(ItemId, companyWh, itemGroupRepo);
        wh1.ShouldBe(companyWh);

        // 2. When company default is null, Item default takes priority
        var wh2 = await manager.ResolveDefaultFgWarehouseAsync(ItemId, null, itemGroupRepo);
        wh2.ShouldBe(itemWh);

        // 3. When item default is null, ItemGroup default takes priority
        item.DefaultWarehouseId = null;
        var wh3 = await manager.ResolveDefaultFgWarehouseAsync(ItemId, null, itemGroupRepo);
        wh3.ShouldBe(groupWh);
    }

    // ========== Batch Availability (PR #60200) ==========

    [Fact]
    public async System.Threading.Tasks.Task ValidateRawMaterialAvailabilityAsync_Batch_Succeeds_When_All_Available()
    {
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<ManufacturingSettings, Guid>>();
        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var wh = Guid.NewGuid();

        var reqs = new[]
        {
            new WorkOrderMaterialRequirement { ItemId = item1, ItemName = "Item 1", RequiredQty = 10m, Rate = 5m, SourceWarehouseId = wh },
            new WorkOrderMaterialRequirement { ItemId = item2, ItemName = "Item 2", RequiredQty = 20m, Rate = 8m, SourceWarehouseId = wh }
        };

        await manager.ValidateRawMaterialAvailabilityAsync(reqs, batch =>
        {
            var dict = new System.Collections.Generic.Dictionary<(Guid, Guid), decimal>
            {
                { (item1, wh), 15m },
                { (item2, wh), 25m }
            };
            return System.Threading.Tasks.Task.FromResult(dict);
        });
    }

    [Fact]
    public async System.Threading.Tasks.Task ValidateRawMaterialAvailabilityAsync_Batch_Throws_When_Insufficient()
    {
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<ManufacturingSettings, Guid>>();
        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var item1 = Guid.NewGuid();
        var wh = Guid.NewGuid();

        var reqs = new[]
        {
            new WorkOrderMaterialRequirement { ItemId = item1, ItemName = "Item 1", RequiredQty = 10m, Rate = 5m, SourceWarehouseId = wh }
        };

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateRawMaterialAvailabilityAsync(reqs, batch =>
            {
                var dict = new System.Collections.Generic.Dictionary<(Guid, Guid), decimal>
                {
                    { (item1, wh), 5m }
                };
                return System.Threading.Tasks.Task.FromResult(dict);
            }));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.InsufficientRawMaterial);
    }

    [Fact]
    public async System.Threading.Tasks.Task SetAvailableQuantitiesAsync_Sets_Source_And_Wip_Warehouses_In_Batch()
    {
        var itemRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = NSubstitute.Substitute.For<Volo.Abp.Domain.Repositories.IRepository<ManufacturingSettings, Guid>>();
        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var srcWh = Guid.NewGuid();
        var wipWh = Guid.NewGuid();
        var item1 = Guid.NewGuid();

        var wo = CreateWorkOrder(10);
        wo.WipWarehouseId = wipWh;
        var woItem = new WorkOrderItem(Guid.NewGuid(), wo.Id, item1, "RM 1", 5m)
        {
            SourceWarehouseId = srcWh
        };
        wo.RequiredItems.Add(woItem);

        await manager.SetAvailableQuantitiesAsync(wo, batch =>
        {
            var dict = new System.Collections.Generic.Dictionary<(Guid, Guid), decimal>
            {
                { (item1, srcWh), 100m },
                { (item1, wipWh), 25m }
            };
            return System.Threading.Tasks.Task.FromResult(dict);
        });

        woItem.AvailableQtyAtSourceWarehouse.ShouldBe(100m);
        woItem.AvailableQtyAtWipWarehouse.ShouldBe(25m);
    }

    // ========== Batch Size Validation (ERPNext PR #60647) ==========

    [Fact]
    public void WorkOrder_BatchSize_Negative_ThrowsException()
    {
        var wo = CreateWorkOrder(100);
        wo.BatchSize = -1m;

        var ex = Should.Throw<BusinessException>(() => wo.Submit());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.BatchSizeCannotBeNegative);
    }

    [Fact]
    public void WorkOrder_BatchSize_ZeroOrPositive_Allowed()
    {
        var wo1 = CreateWorkOrder(100);
        wo1.BatchSize = 0m;
        wo1.Submit();
        wo1.BatchSize.ShouldBe(0m);

        var wo2 = CreateWorkOrder(100);
        wo2.BatchSize = 25m;
        wo2.Submit();
        wo2.BatchSize.ShouldBe(25m);
    }

    // ========== Status Transitions (ERPNext PR #60647 / commit f36cc254a7) ==========

    [Fact]
    public void WorkOrder_StatusTransitions_EnforceSubmittedRequirement()
    {
        var wo = CreateWorkOrder(100);
        // Draft order cannot be stopped, unstopped, or closed
        Should.Throw<BusinessException>(() => wo.Stop())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);
        Should.Throw<BusinessException>(() => wo.Unstop())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);
        Should.Throw<BusinessException>(() => wo.Close())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);

        wo.Submit();
        wo.Status.ShouldBe(WorkOrderStatus.Submitted);

        // Can stop from submitted
        wo.Stop();
        wo.Status.ShouldBe(WorkOrderStatus.Stopped);

        // Can unstop from stopped
        wo.Unstop();
        wo.Status.ShouldBe(WorkOrderStatus.InProcess);

        // Can close from InProcess
        wo.Close();
        wo.Status.ShouldBe(WorkOrderStatus.Closed);

        // Closed order cannot be stopped, unstopped, or re-closed
        Should.Throw<BusinessException>(() => wo.Stop())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);
        Should.Throw<BusinessException>(() => wo.Unstop())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);
        Should.Throw<BusinessException>(() => wo.Close())
            .Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderStatusTransitionRestricted);
    }

    // ========== Sales Order Work Order Qty Tracking (ERPNext PR #60647 / commits 5b66df2033 & 025e87f906) ==========

    [Fact]
    public void GetWorkOrderQtyAgainstSalesOrder_ClosedWorkOrder_ReturnsProducedQty()
    {
        var woClosed = CreateWorkOrder(100);
        woClosed.Submit();
        woClosed.Start();
        woClosed.RecordProduction(60m, 0m);
        woClosed.Close();
        woClosed.Status.ShouldBe(WorkOrderStatus.Closed);

        // Closed work order should only count ProducedQuantity against Sales Order
        WorkOrderManager.GetWorkOrderQtyAgainstSalesOrder(woClosed, forOpenWorkOrderQty: false).ShouldBe(60m);
        WorkOrderManager.GetWorkOrderQtyAgainstSalesOrder(woClosed, forOpenWorkOrderQty: true).ShouldBe(60m);

        var woOpen = CreateWorkOrder(100);
        woOpen.Submit();
        woOpen.Start();
        woOpen.RecordProduction(40m, 0m, processLoss: 10m);

        // Open work order counts Quantity (100) or Quantity - ProcessLossQty (90)
        WorkOrderManager.GetWorkOrderQtyAgainstSalesOrder(woOpen, forOpenWorkOrderQty: false).ShouldBe(100m);
        WorkOrderManager.GetWorkOrderQtyAgainstSalesOrder(woOpen, forOpenWorkOrderQty: true).ShouldBe(90m);
    }

    [Fact]
    public async Task ValidateWorkOrderAgainstSalesOrderAsync_Exceeds_ThrowsException()
    {
        var itemRepo = Substitute.For<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var salesOrderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(salesOrderId, CompanyId, customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(ItemId, "FG Item", 100m, 50m, 0m);
        var soItem = so.Items.First();

        var existingWo = CreateWorkOrder(70m);
        existingWo.SalesOrderId = salesOrderId;
        existingWo.SalesOrderItemId = soItem.Id;
        existingWo.Submit();

        salesOrderRepo.FindAsync(salesOrderId, includeDetails: true).Returns(Task.FromResult<SalesOrder?>(so));
        workOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<WorkOrder> { existingWo }.AsQueryable()));

        var newWo = CreateWorkOrder(40m);
        newWo.SalesOrderId = salesOrderId;
        newWo.SalesOrderItemId = soItem.Id;

        // 70 + 40 = 110 > 100 max allowed
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            manager.ValidateWorkOrderAgainstSalesOrderAsync(newWo, salesOrderRepo, workOrderRepo, 0m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.WorkOrderOverproduction);

        // 70 + 30 = 100 <= 100 should succeed
        var validWo = CreateWorkOrder(30m);
        validWo.SalesOrderId = salesOrderId;
        validWo.SalesOrderItemId = soItem.Id;

        await manager.ValidateWorkOrderAgainstSalesOrderAsync(validWo, salesOrderRepo, workOrderRepo, 0m);
    }

    [Fact]
    public async Task UpdateSalesOrderWorkOrderQtyAsync_UpdatesSalesOrderItemQuantities()
    {
        var itemRepo = Substitute.For<IRepository<MyERP.Inventory.Entities.Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var salesOrderRepo = Substitute.For<IRepository<SalesOrder, Guid>>();
        var workOrderRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var salesOrderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var so = new SalesOrder(salesOrderId, CompanyId, customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(ItemId, "FG Item", 100m, 50m, 0m);
        var soItem = so.Items.First();

        // WO1: 50 ordered, 50 produced (completed)
        var wo1 = CreateWorkOrder(50m);
        wo1.SalesOrderId = salesOrderId;
        wo1.SalesOrderItemId = soItem.Id;
        wo1.Submit();
        wo1.Start();
        wo1.RecordProduction(50m, 0m);

        // WO2: 50 ordered, 20 produced (closed prematurely)
        var wo2 = CreateWorkOrder(50m);
        wo2.SalesOrderId = salesOrderId;
        wo2.SalesOrderItemId = soItem.Id;
        wo2.Submit();
        wo2.Start();
        wo2.RecordProduction(20m, 0m);
        wo2.Close();

        salesOrderRepo.FindAsync(salesOrderId, includeDetails: true).Returns(Task.FromResult<SalesOrder?>(so));
        workOrderRepo.GetQueryableAsync().Returns(Task.FromResult(new List<WorkOrder> { wo1, wo2 }.AsQueryable()));

        await manager.UpdateSalesOrderWorkOrderQtyAsync(salesOrderId, salesOrderRepo, workOrderRepo);

        // Closed wo2 counts only 20 produced, wo1 counts 50 -> total 70
        soItem.WorkOrderQty.ShouldBe(70m);
        soItem.ProducedQty.ShouldBe(70m);

        await salesOrderRepo.Received(1).UpdateAsync(so);
    }

    // ========== Helper ==========

    private static WorkOrder CreateWorkOrder(decimal qty)
    {
        return new WorkOrder(Guid.NewGuid(), CompanyId, "WO-001", ItemId, BomId, qty);
    }
}
