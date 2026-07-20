using System;
using System.Linq;
using MyERP.Manufacturing.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

/// <summary>
/// Tests for BOM Operations integration with existing manufacturing entities.
/// </summary>
public class BomOperationIntegrationTests
{
    [Fact]
    public void BOM_CreateWithOperations_FullCostCalculation()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-001", Guid.NewGuid());
        bom.Quantity = 1;

        // Add raw materials
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Steel Plate", 2, 50m));
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Bolts", 8, 2.50m));

        // Add operations
        var op1 = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 30m);
        op1.CalculateCost(100m); // 30/60 * 100 = 50
        bom.AddOperation(op1);

        var op2 = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 20, 15m);
        op2.CalculateCost(80m); // 15/60 * 80 = 20
        bom.AddOperation(op2);

        bom.RecalculateCost();

        Assert.Equal(120m, bom.TotalMaterialCost); // 2*50 + 8*2.50
        Assert.Equal(70m, bom.OperatingCost);      // 50 + 20
        Assert.Equal(190m, bom.TotalCost);         // 120 + 70
    }

    [Fact]
    public void BOM_Operations_AutoIncludedInCollection()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-002", Guid.NewGuid());

        var op = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 5m);
        bom.AddOperation(op);

        Assert.Single(bom.Operations);
        Assert.Equal(10, bom.Operations.First().SequenceId);
    }

    [Fact]
    public void BOM_Operations_MonotonicSequence_MultipleValid()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-003", Guid.NewGuid());

        bom.AddOperation(new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 5m));
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 20, 10m));
        bom.AddOperation(new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 30, 15m));

        Assert.Equal(3, bom.Operations.Count);
    }

    [Fact]
    public void BomOperation_GetJobCardCount_ForWorkOrder()
    {
        // A BOM with batch-size operations drives Job Card creation for Work Orders
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 20m);
        op.BatchSize = 50;

        // Work Order for 200 units → 4 Job Cards per operation
        Assert.Equal(4, op.GetJobCardCount(200));
        // Work Order for 1 unit → 1 Job Card minimum
        Assert.Equal(1, op.GetJobCardCount(1));
    }

    [Fact]
    public void BomOperation_CostAffectsBomTotalCost()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-004", Guid.NewGuid());

        // No material, only operation cost
        var op = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 60m);
        op.CalculateCost(200m); // 60/60 * 200 = 200
        bom.AddOperation(op);

        bom.RecalculateCost();

        Assert.Equal(0m, bom.TotalMaterialCost);
        Assert.Equal(200m, bom.OperatingCost);
        Assert.Equal(200m, bom.TotalCost);
    }

    [Fact]
    public void BomOperation_SubcontractedOp_TypicalFlow()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 45m);
        op.IsSubcontracted = true;
        op.Description = "Heat Treatment at Vendor A";
        op.CalculateCost(0m); // Subcontracted ops may not have an internal hour rate

        Assert.True(op.IsSubcontracted);
        Assert.Equal(0m, op.OperatingCost); // Cost tracked differently for subcontracted
    }

    [Fact]
    public void BomOperation_FixedTime_AddsToTotalTime()
    {
        var op = new BomOperation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, 5m);
        op.FixedTime = 20m; // 20 min setup

        // For 10 units: 20 + 5*10 = 70
        Assert.Equal(70m, op.GetTotalTime(10));
        // For 1 unit: 20 + 5*1 = 25
        Assert.Equal(25m, op.GetTotalTime(1));
    }

    [Fact]
    public void BOM_RoutingId_CanBeSet()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-INT-005", Guid.NewGuid());
        var routingId = Guid.NewGuid();
        bom.RoutingId = routingId;
        Assert.Equal(routingId, bom.RoutingId);
    }

    [Fact]
    public void BOM_Operations_RemoveAndRecalculate()
    {
        var bomId = Guid.NewGuid();
        var bom = new BillOfMaterials(bomId, Guid.NewGuid(), "BOM-INT-006", Guid.NewGuid());
        bom.Items.Add(new BomItem(Guid.NewGuid(), bomId, Guid.NewGuid(), "Part", 1, 100m));

        var op = new BomOperation(Guid.NewGuid(), bomId, Guid.NewGuid(), 10, 30m);
        op.CalculateCost(60m); // 30
        bom.Operations.Add(op);
        bom.RecalculateCost();
        Assert.Equal(130m, bom.TotalCost); // 100 + 30

        // Remove operation
        bom.Operations.Clear();
        bom.RecalculateCost();
        Assert.Equal(100m, bom.TotalCost); // Only material now
    }

    [Fact]
    public void BOM_OperatingCost_DefaultsZero()
    {
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-INT-007", Guid.NewGuid());
        Assert.Equal(0m, bom.OperatingCost);
    }
}
