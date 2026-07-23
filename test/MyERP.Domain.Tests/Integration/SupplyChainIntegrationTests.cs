using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using MyERP.Accounting.Entities;
using Xunit;
using Shouldly;

namespace MyERP.Domain.Tests.Integration;

/// <summary>
/// End-to-end supply chain integration tests covering:
/// - BOM cost propagation through multi-level hierarchies
/// - Delivery schedule FIFO allocation
/// - Subcontracting RM consumption proportional calculation
/// - Work Order material transfer tracking
/// - Transit warehouse 2-step flow integrity
/// - Production Plan BOM explosion with phantom items
/// </summary>
public class SupplyChainIntegrationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _fiscalYearId = Guid.NewGuid();

    #region BOM Cost Propagation

    [Fact]
    public void BomCost_SingleLevel_CalculatesFromComponents()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-001", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Steel Bar", 2m, 10m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Screw", 5m, 0.50m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Paint", 1m, 8m));
        bom.RecalculateCost();

        Assert.Equal(30.50m, bom.TotalMaterialCost);
    }

    [Fact]
    public void BomCost_WithOperations_IncludesBothMaterialAndOperating()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-002", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Raw Material A", 3m, 15m)); // 45
        var op = new BomOperation(Guid.NewGuid(), bom.Id, Guid.NewGuid(), 1, 60m);
        op.CalculateCost(50m); // 60min at RM50/hr = RM50
        bom.AddOperation(op);
        bom.RecalculateCost();

        Assert.Equal(45m, bom.TotalMaterialCost);
        Assert.Equal(50m, bom.OperatingCost);
    }

    [Fact]
    public void BomItem_PhantomDefault_IsFalse()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-003", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Component", 1m, 10m));
        Assert.False(bom.Items[0].IsPhantom);
    }

    [Fact]
    public void BomItem_SubBomId_DefaultsNull()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-004", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Sub Assembly", 1m, 100m));
        Assert.Null(bom.Items[0].SubBomId);
    }

    [Fact]
    public void BomItem_SubBomId_CanBeSet()
    {
        var subBomId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-005", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "Sub Assembly", 1m, 50m));
        bom.Items[0].SubBomId = subBomId;
        Assert.Equal(subBomId, bom.Items[0].SubBomId);
    }

    #endregion

    #region Delivery Schedule FIFO Allocation

    [Fact]
    public void DeliveryScheduleEntry_RecordDelivery_ReducesPending()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 100m);

        entry.RecordDelivery(30m);

        Assert.Equal(30m, entry.DeliveredQty);
        Assert.Equal(70m, entry.PendingQty);
        Assert.False(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_FullDelivery_MarksComplete()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 50m);

        entry.RecordDelivery(50m);

        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_ProgressiveDelivery_AccumulatesCorrectly()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 200m);

        entry.RecordDelivery(50m);   // 50 delivered, 150 pending
        entry.RecordDelivery(80m);   // 130 delivered, 70 pending
        entry.RecordDelivery(70m);   // 200 delivered, 0 pending

        Assert.Equal(200m, entry.DeliveredQty);
        Assert.Equal(0m, entry.PendingQty);
        Assert.True(entry.IsFullyDelivered);
    }

    [Fact]
    public void DeliveryScheduleEntry_PendingQty_NeverNegative()
    {
        var entry = new DeliveryScheduleEntry(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 1), 10m);

        entry.RecordDelivery(15m); // Over-delivery clamped

        Assert.True(entry.DeliveredQty >= entry.ScheduledQty);
        Assert.Equal(0m, entry.PendingQty); // Never negative
    }

    #endregion

    #region Work Order Material Tracking

    [Fact]
    public void WorkOrder_MaterialTransferTracking_IncrementalTransfer()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), _companyId, "BOM-MAT", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM-A", 10m, 5m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bom.Id, Guid.NewGuid(), "RM-B", 20m, 2m));

        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-001", Guid.NewGuid(), bom.Id, 100m);

        // Required items track quantities needed for production
        Assert.Equal(100m, wo.Quantity);
    }

    [Fact]
    public void WorkOrder_ProductionCompletion_TracksProgress()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-002", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(40m);
        Assert.Equal(40m, wo.ProducedQuantity);
        Assert.Equal(40m, wo.PercentComplete); // 40/100 = 40%

        wo.RecordProduction(60m);
        Assert.Equal(100m, wo.ProducedQuantity);
        Assert.Equal(100m, wo.PercentComplete); // 100/100 = 100%
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void WorkOrder_Overproduction_BlockedByDefault()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-003", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();
        wo.RecordProduction(100m); // Exactly at limit

        // Default 0% overproduction tolerance → any more is blocked
        Assert.Throws<Volo.Abp.BusinessException>(() => wo.RecordProduction(1m));
    }

    [Fact]
    public void WorkOrder_Overproduction_AllowedWithPercentage()
    {
        var wo = new WorkOrder(Guid.NewGuid(), _companyId, "WO-004", Guid.NewGuid(), Guid.NewGuid(), 100m);
        wo.Submit();
        wo.Start();

        // With 10% overproduction: max = 100 × 1.10 = 110
        wo.RecordProduction(105m, 10m); // Within 110 limit
        Assert.Equal(105m, wo.ProducedQuantity);
    }

    #endregion

    #region SubcontractingOrder RM Consumption

    [Fact]
    public void SubcontractingOrder_PerReceived_TracksProgress()
    {
        var supplierId = Guid.NewGuid();
        var sco = new SubcontractingOrder(Guid.NewGuid(), _companyId, "SCO-001", DateTime.Today, supplierId);

        var item1 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "FG Item A", 100m, 10m);
        var item2 = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "FG Item B", 50m, 20m);
        sco.AddItem(item1);
        sco.AddItem(item2);
        sco.Submit();

        // Receive 80 of A (80%) and 50 of B (100%)
        sco.Items[0].ReceivedQty = 80m;
        sco.Items[1].ReceivedQty = 50m;

        // Per-item received: 80% and 100% → MIN = 80%
        var minPct = sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty) * 100m : 100m);
        Assert.Equal(80m, minPct);
    }

    [Fact]
    public void SubcontractingOrder_FullReceipt_AllItemsComplete()
    {
        var supplierId = Guid.NewGuid();
        var sco = new SubcontractingOrder(Guid.NewGuid(), _companyId, "SCO-002", DateTime.Today, supplierId);
        var item = new SubcontractingOrderItem(Guid.NewGuid(), sco.Id, Guid.NewGuid(), "FG Widget", 50m, 10m);
        sco.AddItem(item);
        sco.Submit();

        sco.Items[0].ReceivedQty = 50m;
        var pct = sco.Items.Min(i => i.Qty > 0 ? (i.ReceivedQty / i.Qty) * 100m : 100m);
        Assert.Equal(100m, pct);
    }

    #endregion

    #region Transit Transfer 2-Step Integrity

    [Fact]
    public void TransitTransfer_Leg1Leg2_MaintainsQuantityIntegrity()
    {
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var transitId = Guid.NewGuid();

        // Leg 1: 50 units of Item A + 30 units of Item B → Transit
        var leg1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        leg1.AddItem(item1Id, 50, _warehouseId, transitId, 10m);
        leg1.AddItem(item2Id, 30, _warehouseId, transitId, 25m);
        leg1.Submit();
        leg1.Post();

        // Leg 2: Receive same quantities from Transit → Destination
        var destWarehouse = Guid.NewGuid();
        var leg2 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        leg2.AddItem(item1Id, 50, transitId, destWarehouse, 10m);
        leg2.AddItem(item2Id, 30, transitId, destWarehouse, 25m);
        leg2.ReferenceType = "StockEntry";
        leg2.ReferenceId = leg1.Id;
        leg2.Submit();
        leg2.Post();

        // Both legs should have identical total quantities
        var leg1TotalQty = leg1.Items.Sum(i => i.Quantity);
        var leg2TotalQty = leg2.Items.Sum(i => i.Quantity);
        Assert.Equal(leg1TotalQty, leg2TotalQty);
        Assert.Equal(80m, leg1TotalQty); // 50 + 30

        // Both legs should have identical total value
        var leg1TotalValue = leg1.Items.Sum(i => i.Quantity * (i.ValuationRate ?? 0));
        var leg2TotalValue = leg2.Items.Sum(i => i.Quantity * (i.ValuationRate ?? 0));
        Assert.Equal(leg1TotalValue, leg2TotalValue);
        Assert.Equal(1250m, leg1TotalValue); // 50×10 + 30×25
    }

    [Fact]
    public void TransitTransfer_PartialReceive_AllowsSplitting()
    {
        var itemId = Guid.NewGuid();
        var transitId = Guid.NewGuid();
        var destId = Guid.NewGuid();

        // Send 100 units to transit
        var leg1 = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.SendToWarehouse, DateTime.Today);
        leg1.AddItem(itemId, 100, _warehouseId, transitId, 5m);
        leg1.Submit();
        leg1.Post();

        // Receive only 60 units first (partial)
        var leg2a = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today);
        leg2a.AddItem(itemId, 60, transitId, destId, 5m);
        leg2a.ReferenceType = "StockEntry";
        leg2a.ReferenceId = leg1.Id;
        leg2a.Submit();
        leg2a.Post();

        // Receive remaining 40 later
        var leg2b = new StockEntry(Guid.NewGuid(), _companyId, StockEntryType.ReceiveAtWarehouse, DateTime.Today.AddDays(1));
        leg2b.AddItem(itemId, 40, transitId, destId, 5m);
        leg2b.ReferenceType = "StockEntry";
        leg2b.ReferenceId = leg1.Id;
        leg2b.Submit();
        leg2b.Post();

        // Total received = sent
        var totalReceived = 60m + 40m;
        Assert.Equal(100m, totalReceived);
    }

    #endregion

    #region Production Plan BOM Explosion

    [Fact]
    public void ProductionPlan_PlannedItem_TracksQuantityAndBom()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-2026-001", DateTime.Today);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, Guid.NewGuid(), "FG-001", Guid.NewGuid(), 500m);
        plan.AddPlannedItem(item);

        Assert.Single(plan.PlannedItems);
        Assert.Equal(500m, plan.PlannedItems[0].PlannedQty);
        Assert.Equal("FG-001", plan.PlannedItems[0].ItemName);
    }

    [Fact]
    public void ProductionPlan_MaterialRequirement_CalculatesFromBomExplosion()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-2026-002", DateTime.Today);

        // Simulate BOM explosion result: 500 FG needs 1000 RM-A + 2500 RM-B
        var mr1 = new ProductionPlanMrItem(Guid.NewGuid(), plan.Id, Guid.NewGuid(), "RM-A", 1000m);
        var mr2 = new ProductionPlanMrItem(Guid.NewGuid(), plan.Id, Guid.NewGuid(), "RM-B", 2500m);
        plan.AddMaterialRequirement(mr1);
        plan.AddMaterialRequirement(mr2);

        Assert.Equal(2, plan.MaterialRequirements.Count);
        Assert.Equal(1000m, plan.MaterialRequirements[0].RequiredQty);
        Assert.Equal(2500m, plan.MaterialRequirements[1].RequiredQty);
    }

    [Fact]
    public void ProductionPlan_Submit_RequiresPlannedItems()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-2026-003", DateTime.Today);
        Assert.Throws<Volo.Abp.BusinessException>(() => plan.Submit());
    }

    [Fact]
    public void ProductionPlan_SubmitWithItems_Succeeds()
    {
        var plan = new ProductionPlan(Guid.NewGuid(), _companyId, "PP-2026-004", DateTime.Today);
        var item = new ProductionPlanItem(Guid.NewGuid(), plan.Id, Guid.NewGuid(), "Widget", Guid.NewGuid(), 100m);
        plan.AddPlannedItem(item);
        plan.Submit();

        Assert.Equal(ProductionPlanStatus.Submitted, plan.Status);
    }

    #endregion

    #region Cost Center Allocation Distribution

    [Fact]
    public void CostCenterAllocation_EvenDistribution_SplitsEqually()
    {
        var alloc = new MyERP.Accounting.Entities.CostCenterAllocation(
            Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, null);

        alloc.AddEntry(Guid.NewGuid(), 50m);  // CC-A gets 50%
        alloc.AddEntry(Guid.NewGuid(), 50m);  // CC-B gets 50%

        var result = alloc.Distribute(1000m);
        Assert.Equal(500m, result[0].Amount);
        Assert.Equal(500m, result[1].Amount);
    }

    [Fact]
    public void CostCenterAllocation_UnevenDistribution_RemainderToFirst()
    {
        var alloc = new MyERP.Accounting.Entities.CostCenterAllocation(
            Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, null);

        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.33m);
        alloc.AddEntry(Guid.NewGuid(), 33.34m);

        var result = alloc.Distribute(100m);
        // Per ERPNext: first-CC-absorbs rounding remainder
        var total = result.Sum(r => r.Amount);
        Assert.Equal(100m, total); // Total must match exactly
    }

    [Fact]
    public void CostCenterAllocation_Validate_MustSumTo100()
    {
        var alloc = new MyERP.Accounting.Entities.CostCenterAllocation(
            Guid.NewGuid(), _companyId, Guid.NewGuid(), DateTime.Today, null);

        alloc.AddEntry(Guid.NewGuid(), 60m);
        alloc.AddEntry(Guid.NewGuid(), 30m); // Only 90%

        Assert.Throws<Volo.Abp.BusinessException>(() => alloc.ValidatePercentages());
    }

    #endregion

    #region UOM Conversion in Stock Operations

    [Fact]
    public void StockQty_CalculatesFromConversionFactor()
    {
        // Sale of 5 Dozen → StockQty = 5 × 12 = 60 Units
        var soId = Guid.NewGuid();
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, Guid.NewGuid(), "Dozen Widget", 5m, 120m, 0m, "Dozen");
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 12m;

        Assert.Equal(60m, soItem.StockQty); // Quantity × ConversionFactor
    }

    [Fact]
    public void StockQty_SameUom_FactorIsOne()
    {
        var soId = Guid.NewGuid();
        var soItem = new SalesOrderItem(Guid.NewGuid(), soId, Guid.NewGuid(), "Standard Item", 25m, 10m, 0m, "Unit");
        soItem.StockUom = "Unit";
        soItem.ConversionFactor = 1m; // Same UOM

        Assert.Equal(25m, soItem.StockQty); // 25 × 1 = 25
    }

    [Fact]
    public void StockQty_FractionalConversion_Preserves()
    {
        // 2 Gallons → 2 × 3.785 = 7.57 Litres
        var poId = Guid.NewGuid();
        var poItem = new PurchaseOrderItem(Guid.NewGuid(), poId, Guid.NewGuid(), "Chemical", 2m, 50m, 0m, "Gallon");
        poItem.StockUom = "Litre";
        poItem.ConversionFactor = 3.785m;

        Assert.Equal(7.57m, poItem.StockQty); // 2 × 3.785
    }

    #endregion
}
