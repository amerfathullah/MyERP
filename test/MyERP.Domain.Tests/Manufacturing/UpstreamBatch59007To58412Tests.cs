using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class UpstreamBatch59007To58412Tests
{
    // --- PR #59007 / commit 2ad4a8c4a4: ItemLeadTime duration scaling & MPS cumulative lead time ---

    [Fact]
    public void ItemLeadTime_CalculateLeadTimeDays_ScalesManufacturingDurationByQty()
    {
        var leadTime = new ItemLeadTime(
            Guid.NewGuid(),
            Guid.NewGuid(),
            manufacturingTimeInMins: 1440, // 1 day per unit
            bufferTimeDays: 2,
            purchaseTimeDays: 1);

        // For 5 units: 1440 mins * 5 / 1440 = 5 days + 2 buffer days = 7 days
        var mfgDays = leadTime.CalculateLeadTimeDays(qty: 5, isManufacture: true);
        mfgDays.ShouldBe(7);

        // For non-manufacturing: returns PurchaseTimeDays + BufferTimeDays = 1 + 2 = 3
        var purchaseDays = leadTime.CalculateLeadTimeDays(qty: 5, isManufacture: false);
        purchaseDays.ShouldBe(3);
    }

    [Fact]
    public async Task MasterProductionScheduleService_GetCumulativeLeadTimeDaysAsync_UsesItemLeadTimeScaledByQty()
    {
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var leadTimeRepo = Substitute.For<IRepository<ItemLeadTime, Guid>>();

        var item = new Item(itemId, Guid.NewGuid(), "FG-001", "Finished Good", ItemType.Goods)
        {
            LeadTimeDays = 3
        };
        itemRepo.FindAsync(itemId).Returns(item);

        var leadTime = new ItemLeadTime(
            Guid.NewGuid(),
            itemId,
            manufacturingTimeInMins: 2880, // 2 days per unit
            bufferTimeDays: 1,
            purchaseTimeDays: 2);

        var leadTimes = new List<ItemLeadTime> { leadTime }.AsQueryable();
        leadTimeRepo.GetQueryableAsync().Returns(Task.FromResult(leadTimes));

        var mpsService = new MasterProductionScheduleService(itemRepo, bomRepo, leadTimeRepo);

        // Qty = 2: 2880 * 2 / 1440 = 4 days + 1 buffer = 5 days
        var days = await mpsService.GetCumulativeLeadTimeDaysAsync(itemId, bomId, qty: 2);
        days.ShouldBe(5);
    }

    // --- PR #58412 / commit 35b3358a5d: Accounting Dimension validation on single doctypes and child tables ---

    [Theory]
    [InlineData("ManufacturingSettings")]
    [InlineData("CompanySetting")]
    [InlineData("AccountsConfiguration")]
    [InlineData("SystemSetup")]
    public void AccountingDimension_ValidateDocumentType_BlocksSingleDocTypes(string docType)
    {
        var ex = Should.Throw<BusinessException>(() => AccountingDimension.ValidateDocumentType(docType));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Theory]
    [InlineData("SalesTeam")]
    [InlineData("DeliveryStop")]
    [InlineData("CostCenterAllocation")]
    [InlineData("ProjectMember")]
    public void AccountingDimension_ValidateDocumentType_BlocksChildTables(string docType)
    {
        var ex = Should.Throw<BusinessException>(() => AccountingDimension.ValidateDocumentType(docType));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Theory]
    [InlineData("SalesInvoice")]
    [InlineData("PurchaseInvoice")]
    [InlineData("PaymentEntry")]
    [InlineData("JournalEntry")]
    public void AccountingDimension_ValidateDocumentType_AllowsStandardDocTypes(string docType)
    {
        Should.NotThrow(() => AccountingDimension.ValidateDocumentType(docType));
    }

    // --- PR #58799 & #58847 / commits 1615ecd25e, d64e45582a: WorkOrder ProductionPlan tracking & quantity validation ---

    [Fact]
    public async Task ValidateProductionPlanQuantityAsync_BothItemAndSubAssemblySet_ThrowsException()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-001", Guid.NewGuid(), Guid.NewGuid(), 10m)
        {
            ProductionPlanId = Guid.NewGuid(),
            ProductionPlanItemId = Guid.NewGuid(),
            ProductionPlanSubAssemblyItemId = Guid.NewGuid()
        };

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateProductionPlanQuantityAsync(wo, planRepo, woRepo, 5m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ValidateProductionPlanQuantityAsync_WithinAllowedQty_Passes()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var planId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var plan = new ProductionPlan(planId, companyId, "PP-001", DateTime.UtcNow);
        var planItem = new ProductionPlanItem(planItemId, planId, itemId, "FG Item", bomId, 100m);
        plan.AddPlannedItem(planItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);

        // Previous submitted WO for 40 units with 0 process loss
        var prevWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", itemId, bomId, 40m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
        };
        prevWo.Submit();

        var currentWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-002", itemId, bomId, 60m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
        };

        var workOrders = new List<WorkOrder> { prevWo, currentWo }.AsQueryable();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders));

        // Total allowed: 100 * 1.05 = 105. Committed: 40. Remaining allowed: 65.
        // Current WO qty 60 <= 65 -> Should pass.
        await Should.NotThrowAsync(async () =>
            await manager.ValidateProductionPlanQuantityAsync(currentWo, planRepo, woRepo, 5m));
    }

    [Fact]
    public async Task ValidateProductionPlanQuantityAsync_ExceedsAllowedQty_ThrowsOverproductionException()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var planId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var plan = new ProductionPlan(planId, companyId, "PP-001", DateTime.UtcNow);
        var planItem = new ProductionPlanItem(planItemId, planId, itemId, "FG Item", bomId, 100m);
        plan.AddPlannedItem(planItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);

        var prevWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", itemId, bomId, 50m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
        };
        prevWo.Submit();

        // Total allowed: 100 * 1.05 = 105. Committed: 50. Remaining: 55.
        // Current WO requests 60 -> Exceeds 55!
        var currentWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-002", itemId, bomId, 60m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
        };

        var workOrders = new List<WorkOrder> { prevWo, currentWo }.AsQueryable();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders));

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await manager.ValidateProductionPlanQuantityAsync(currentWo, planRepo, woRepo, 5m));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ProductionPlanQuantityExceeded);
    }

    [Fact]
    public async Task ValidateProductionPlanQuantityAsync_AccountsForProcessLossHeadroom()
    {
        var itemRepo = Substitute.For<IRepository<Item, Guid>>();
        var bomRepo = Substitute.For<IRepository<BillOfMaterials, Guid>>();
        var settingsRepo = Substitute.For<IRepository<ManufacturingSettings, Guid>>();
        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();

        var manager = new WorkOrderManager(itemRepo, bomRepo, settingsRepo);

        var planId = Guid.NewGuid();
        var planItemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var plan = new ProductionPlan(planId, companyId, "PP-001", DateTime.UtcNow);
        var planItem = new ProductionPlanItem(planItemId, planId, itemId, "FG Item", bomId, 100m);
        plan.AddPlannedItem(planItem);

        planRepo.GetAsync(planId, includeDetails: true).Returns(plan);

        // Previous WO was 50 units, but had 10 units process loss -> net committed is 40 units!
        var prevWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-001", itemId, bomId, 50m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
            ProcessLossQty = 10m
        };
        prevWo.Submit();

        // Allowance: 0%. Planned: 100. Committed: 50 - 10 = 40. Remaining allowed: 100 - 40 = 60.
        // Current WO is 60m -> should pass because process loss opened up headroom for replacement!
        var currentWo = new WorkOrder(Guid.NewGuid(), companyId, "WO-002", itemId, bomId, 60m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItemId,
        };

        var workOrders = new List<WorkOrder> { prevWo, currentWo }.AsQueryable();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(workOrders));

        await Should.NotThrowAsync(async () =>
            await manager.ValidateProductionPlanQuantityAsync(currentWo, planRepo, woRepo, 0m));
    }
}
