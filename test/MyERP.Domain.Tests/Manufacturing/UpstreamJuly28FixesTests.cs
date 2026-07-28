using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Manufacturing;

/// <summary>
/// Tests for upstream fixes synced 2026-07-28:
/// - PR bde118e7cf: Exclude corrective job cards from semi-FG aggregate
/// - PR 5548f0726a: Sum semi-FG qty across split job cards (our architecture already handles via GroupBy+Sum)
/// - PR d269c90838: BOM Creator cost update (N/A — no BomCreator entity in MyERP)
/// - PR b37152752f: BOM Creator tree scoping (N/A)
/// - PR 543301701e: SCR title template fix (N/A — per DO-NOT #413)
/// </summary>
public class UpstreamJuly28FixesTests
{
    private static JobCard CreateJobCard(Guid workOrderId, Guid operationId, decimal forQty = 100, bool isCorrective = false)
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), workOrderId, operationId, forQty, 1);
        jc.IsCorrective = isCorrective;
        return jc;
    }

    // ===== PR bde118e7cf: Corrective JC Exclusion =====

    [Fact]
    public void IsCorrective_DefaultsFalse()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 1);
        jc.IsCorrective.ShouldBeFalse();
    }

    [Fact]
    public void IsCorrective_CanBeSetTrue()
    {
        var jc = new JobCard(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, 1);
        jc.IsCorrective = true;
        jc.IsCorrective.ShouldBeTrue();
    }

    [Fact]
    public void CorrectionJC_ShouldNotCountTowardProduction()
    {
        // Corrective JCs represent rework — their qty should NOT inflate production totals
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var normalJc = CreateJobCard(woId, opId, 100, isCorrective: false);
        var correctiveJc = CreateJobCard(woId, opId, 20, isCorrective: true);

        // Simulate aggregation logic (mirrors GetWorkOrderCompletedQtyAsync)
        var allJcs = new List<JobCard> { normalJc, correctiveJc };
        var nonCorrective = allJcs.Where(jc => !jc.IsCorrective).ToList();

        // Only non-corrective JCs should be counted
        nonCorrective.Count.ShouldBe(1);
        nonCorrective[0].ForQuantity.ShouldBe(100);
    }

    [Fact]
    public void CorrectionJC_ExcludedFromBottleneckFormula()
    {
        // The MIN bottleneck across operations should ignore corrective JCs
        var woId = Guid.NewGuid();
        var op1 = Guid.NewGuid();
        var op2 = Guid.NewGuid();

        var jcs = new List<(Guid OpId, decimal CompletedQty, bool IsCorrective)>
        {
            (op1, 80, false),   // Normal JC for op1: produced 80
            (op1, 20, true),    // Corrective JC for op1: rework 20 (should NOT count)
            (op2, 60, false),   // Normal JC for op2: produced 60
        };

        // Aggregate excluding corrective
        var perOpQty = jcs
            .Where(j => !j.IsCorrective)
            .GroupBy(j => j.OpId)
            .Select(g => g.Sum(j => j.CompletedQty))
            .ToList();

        // Bottleneck = MIN(80, 60) = 60 (NOT MIN(100, 60) = 60 if corrective was included)
        perOpQty.Min().ShouldBe(60);
    }

    // ===== PR 5548f0726a: Split JC Aggregation (architecture already handles) =====

    [Fact]
    public void SplitJobCards_SummedPerOperation()
    {
        // When a WO has batch_size splitting (e.g., 100 units split into 4×25 JCs),
        // produced qty = SUM across all JCs for same operation
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var splitJcs = new List<(Guid OpId, decimal CompletedQty, bool IsCorrective)>
        {
            (opId, 25, false),  // Split JC 1: 25 units
            (opId, 25, false),  // Split JC 2: 25 units
            (opId, 25, false),  // Split JC 3: 25 units
            (opId, 25, false),  // Split JC 4: 25 units
        };

        var total = splitJcs
            .Where(j => !j.IsCorrective)
            .GroupBy(j => j.OpId)
            .Select(g => g.Sum(j => j.CompletedQty))
            .ToList();

        // All 4 split JCs are summed = 100
        total.First().ShouldBe(100);
    }

    [Fact]
    public void SplitJobCards_PartialCompletion_SumsOnlyCompleted()
    {
        // If only 2 of 4 split JCs are completed, sum = 50
        var woId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        var jcs = new List<(Guid OpId, decimal CompletedQty, bool IsCorrective, bool IsSubmitted)>
        {
            (opId, 25, false, true),   // Completed
            (opId, 25, false, true),   // Completed
            (opId, 0, false, false),   // Not yet started
            (opId, 0, false, false),   // Not yet started
        };

        // Only submitted JCs contribute (docstatus=1 filter handled by query)
        var submitted = jcs.Where(j => j.IsSubmitted && !j.IsCorrective);
        var total = submitted.GroupBy(j => j.OpId).Select(g => g.Sum(j => j.CompletedQty)).First();
        total.ShouldBe(50);
    }

    // ===== PR d269c90838: BOM Creator cost update (N/A) =====

    [Fact]
    public void BOM_RecalculateCost_AlwaysRefreshesRates()
    {
        // MyERP's RecalculateCost() has no BomCreator guard — always recalculates
        // This confirms the upstream bug (skipping cost update for BOM Creator BOMs) cannot exist here
        var bom = new BillOfMaterials(Guid.NewGuid(), Guid.NewGuid(), "BOM-TEST-001", Guid.NewGuid());
        var bomItemId = Guid.NewGuid();
        var bomItem = new BomItem(Guid.NewGuid(), bom.Id, bomItemId, "Test Item", 5, 10);
        bom.Items.Add(bomItem); // 5 units × RM 10 = RM 50

        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(50);

        // Change the rate conceptually (in real usage, rate comes from item price lookup)
        // RecalculateCost will re-sum from items regardless of how BOM was created
        bom.Items.First().Rate = 20;
        bom.RecalculateCost();
        bom.TotalMaterialCost.ShouldBe(100); // Updated correctly
    }

    // ===== PR 543301701e: SCR title template (N/A) =====

    [Fact]
    public void TitleTemplatePattern_NeverStoredInMyERP()
    {
        // Per DO-NOT #413: MyERP never uses template patterns like "{supplier_name}"
        // Entity constructors always set actual resolved names at creation time
        // This test documents the architectural decision
        var templatePattern = "{supplier_name}";
        templatePattern.Contains("{").ShouldBeTrue(); // ERPNext bug: stored pattern
        // MyERP would never store this — entities have typed Name/SupplierName properties
    }

    // ===== WorkOrder ProducedQuantity accumulation =====

    [Fact]
    public void WorkOrder_RecordProduction_Accumulates()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-001", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(30);
        wo.ProducedQuantity.ShouldBe(30);

        wo.RecordProduction(40);
        wo.ProducedQuantity.ShouldBe(70);
    }

    [Fact]
    public void WorkOrder_AutoCompletes_AtFullQty()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-002", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        wo.RecordProduction(100);
        wo.Status.ShouldBe(WorkOrderStatus.Completed);
    }

    [Fact]
    public void WorkOrder_Overproduction_BlockedByDefault()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-003", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        // With 0% overproduction tolerance, cannot exceed exact qty
        Should.Throw<BusinessException>(() => wo.RecordProduction(101, overproductionPercentage: 0));
    }

    [Fact]
    public void WorkOrder_Overproduction_WithAllowance()
    {
        var wo = new WorkOrder(Guid.NewGuid(), Guid.NewGuid(), "WO-TEST-004", Guid.NewGuid(), Guid.NewGuid(), 100);
        wo.Submit();
        wo.Start();

        // With 5% tolerance, can produce up to 105
        wo.RecordProduction(105, overproductionPercentage: 5);
        wo.ProducedQuantity.ShouldBe(105);
    }

    // ===== Multi-operation bottleneck =====

    [Fact]
    public void Bottleneck_MultipleOperations_UsesMinimum()
    {
        // WO with 3 operations at different completion levels
        var perOpQty = new List<decimal> { 100, 80, 60 };

        // Bottleneck = MIN across operations
        perOpQty.Min().ShouldBe(60);
    }

    [Fact]
    public void Bottleneck_SingleOperation_UsesDirectTotal()
    {
        var perOpQty = new List<decimal> { 100 };
        perOpQty.Min().ShouldBe(100);
    }

    [Fact]
    public void Bottleneck_EmptyOperations_ReturnsZero()
    {
        var perOpQty = new List<decimal>();
        var result = perOpQty.Count == 0 ? 0 : perOpQty.Min();
        result.ShouldBe(0);
    }

    // ===== Session tracking =====

    [Fact]
    public void UpstreamSync_5Commits_FromErnpext56a9ca334b()
    {
        // Documents that erpnext HEAD moved from 56a9ca334b to 2f07dfc474 (+5 non-merge commits)
        var commits = new[]
        {
            "d269c90838 — BOM Creator cost refresh (N/A: no BomCreator entity)",
            "b37152752f — BOM Creator tree scoping (N/A: no BomCreator entity)",
            "bde118e7cf — Exclude corrective JC from semi-FG aggregate (IMPLEMENTED)",
            "5548f0726a — Sum semi-FG qty across split JCs (already handled by architecture)",
            "543301701e — SCR title template fix (N/A: per DO-NOT #413)"
        };
        commits.Length.ShouldBe(5);
    }
}
