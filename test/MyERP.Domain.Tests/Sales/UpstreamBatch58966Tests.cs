using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class UpstreamBatch58966Tests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    // ==========================================
    // PR #58966: Exclude fully billed orders from invoice picker
    // ==========================================

    [Fact]
    public void HasPotentiallyBillableItems_FullyBilledOrder_ReturnsFalse_EvenWithAllowance()
    {
        // Per ERPNext PR #58966 / commit 5f216c5d55:
        // A Sales Order that is 100% billed must NOT be offered in the invoice picker,
        // even if over-billing allowance would otherwise give monetary headroom.
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-001", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        var item = so.Items[0];
        item.BilledQty = 10m; // 100% billed

        // With 100% over-billing allowance, monetary headroom would be 1000 < 2000,
        // but unbilled pending qty is 0.
        var billable = SalesOrderManager.HasPotentiallyBillableItems(
            so,
            globalOverBillingAllowance: 100m);

        billable.ShouldBeFalse();
    }

    [Fact]
    public void HasPotentiallyBillableItems_FullyBilledOrder_WithItemAllowance_ReturnsFalse()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-002", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        var item = so.Items[0];
        item.BilledQty = 10m; // 100% billed

        var itemAllowances = new Dictionary<Guid, decimal>
        {
            { _itemId, 50m } // 50% item allowance
        };

        var billable = SalesOrderManager.HasPotentiallyBillableItems(
            so,
            itemOverBillingAllowances: itemAllowances);

        billable.ShouldBeFalse();
    }

    [Fact]
    public void HasPotentiallyBillableItems_PartiallyBilledOrder_ReturnsTrue()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-003", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        var item = so.Items[0];
        item.BilledQty = 6m; // 60% billed, 4 pending

        var billable = SalesOrderManager.HasPotentiallyBillableItems(so);

        billable.ShouldBeTrue();
    }

    [Fact]
    public void HasPotentiallyBillableItems_ReturnedQty_NoPendingDeliveredQty_ReturnsFalse()
    {
        // Per ERPNext PR #58966:
        // has_unbilled_delivered_qty = (qty - returned_qty - billed_qty > 0) | (delivered_qty - billed_qty > 0)
        // If all ordered qty was returned and delivered is 0, no unbilled delivered qty remains.
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-004", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        var item = so.Items[0];
        item.ReturnedQty = 10m;
        item.DeliveredQty = 0m;
        item.BilledQty = 0m;

        var billable = SalesOrderManager.HasPotentiallyBillableItems(so);

        billable.ShouldBeFalse();
    }

    [Fact]
    public void HasPotentiallyBillableItems_ClosedOrder_Or_ClosedItem_ReturnsFalse()
    {
        var so = new SalesOrder(Guid.NewGuid(), _companyId, _customerId, "SO-005", DateTime.UtcNow);
        so.AddItem(_itemId, "Widget", 10m, 100m, 0m);
        so.Submit();

        // Close the item row
        so.Items[0].IsClosed = true;

        SalesOrderManager.HasPotentiallyBillableItems(so).ShouldBeFalse();
    }

    // ==========================================
    // PR #58799 & #58847: Production Plan OrderedQty & Process Loss Headroom
    // ==========================================

    [Fact]
    public void ProductionPlanItem_OrderedQty_And_PendingQty_Calculations()
    {
        var planId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var planItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Finished Good", bomId, 100m);

        planItem.PlannedQty.ShouldBe(100m);
        planItem.OrderedQty.ShouldBe(0m);
        planItem.PendingQty.ShouldBe(100m);

        planItem.OrderedQty = 40m;
        planItem.PendingQty.ShouldBe(60m);

        planItem.OrderedQty = 100m;
        planItem.PendingQty.ShouldBe(0m);

        // Excess ordered does not result in negative pending
        planItem.OrderedQty = 110m;
        planItem.PendingQty.ShouldBe(0m);
    }

    [Fact]
    public async Task ValidateProductionPlanQuantity_AllowsReplacementWorkOrder_AfterProcessLoss()
    {
        // Per ERPNext PR #58799 & #58847:
        // Work Order 1 ordered 100, suffered process loss of 20.
        // Net committed qty from WO 1 = 100 - 20 = 80.
        // Replacement Work Order 2 of qty 20 must be allowed against the planned 100 units.
        var planId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-001", DateTime.UtcNow);
        var planItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Product", bomId, 100m);
        plan.AddPlannedItem(planItem);

        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        planRepo.GetAsync(planId, includeDetails: true).Returns(Task.FromResult(plan));

        // Existing Work Order 1: qty = 100, ProcessLossQty = 20
        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, bomId, 100m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItem.Id,
        };
        wo1.Submit();
        wo1.Start();
        wo1.RecordProduction(quantity: 80m, overproductionPercentage: 0m, processLoss: 20m);
        wo1.ProducedQuantity.ShouldBe(80m);
        wo1.ProcessLossQty.ShouldBe(20m);

        // Replacement Work Order 2: qty = 20
        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", _itemId, bomId, 20m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItem.Id,
        };

        var allWos = new List<WorkOrder> { wo1, wo2 }.AsQueryable();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(allWos));

        var woManager = new WorkOrderManager(
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IRepository<BillOfMaterials, Guid>>(),
            Substitute.For<IRepository<ManufacturingSettings, Guid>>());

        // Should NOT throw because 20 <= (100 - (100 - 20)) = 20
        await Should.NotThrowAsync(async () =>
            await woManager.ValidateProductionPlanQuantityAsync(wo2, planRepo, woRepo, overproductionPercentage: 0m));
    }

    [Fact]
    public async Task ValidateProductionPlanQuantity_RejectsExcessReplacementWorkOrder_ExceedingHeadroom()
    {
        // Work Order 1 committed = 100 - 20 = 80.
        // Planned = 100. Headroom = 20.
        // Attempting Work Order 2 with qty = 25 must throw ProductionPlanQuantityExceeded.
        var planId = Guid.NewGuid();
        var bomId = Guid.NewGuid();
        var plan = new ProductionPlan(planId, _companyId, "PP-002", DateTime.UtcNow);
        var planItem = new ProductionPlanItem(Guid.NewGuid(), planId, _itemId, "Product", bomId, 100m);
        plan.AddPlannedItem(planItem);

        var planRepo = Substitute.For<IRepository<ProductionPlan, Guid>>();
        planRepo.GetAsync(planId, includeDetails: true).Returns(Task.FromResult(plan));

        var wo1 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", _itemId, bomId, 100m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItem.Id,
        };
        wo1.Submit();
        wo1.Start();
        wo1.RecordProduction(quantity: 80m, overproductionPercentage: 0m, processLoss: 20m);

        var wo2 = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", _itemId, bomId, 25m)
        {
            ProductionPlanId = planId,
            ProductionPlanItemId = planItem.Id,
        };

        var allWos = new List<WorkOrder> { wo1, wo2 }.AsQueryable();
        var woRepo = Substitute.For<IRepository<WorkOrder, Guid>>();
        woRepo.GetQueryableAsync().Returns(Task.FromResult(allWos));

        var woManager = new WorkOrderManager(
            Substitute.For<IRepository<Item, Guid>>(),
            Substitute.For<IRepository<BillOfMaterials, Guid>>(),
            Substitute.For<IRepository<ManufacturingSettings, Guid>>());

        var ex = await Should.ThrowAsync<BusinessException>(async () =>
            await woManager.ValidateProductionPlanQuantityAsync(wo2, planRepo, woRepo, overproductionPercentage: 0m));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ProductionPlanQuantityExceeded);
        ex.Data["workOrderQty"].ShouldBe(25m);
        ex.Data["maxAllowedQty"].ShouldBe(20m);
    }
}
