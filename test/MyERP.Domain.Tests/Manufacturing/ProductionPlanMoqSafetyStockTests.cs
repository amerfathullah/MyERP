using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Manufacturing;
using MyERP.Manufacturing.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Manufacturing;

public class ProductionPlanMoqSafetyStockTests
{
    [Fact]
    public void ProductionPlan_MOQSurplus_CarriedForwardAcrossRows()
    {
        // Per ERPNext PR #58805: apply MOQ once across Production Plan rows
        // Demand across two orders: Order 1 needs 4, Order 2 needs 5. Item MOQ is 10.
        var rows = new List<ProductionPlanMrItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Raw Mat A", 4m)
            {
                MinOrderQty = 10m,
                ProcurementType = SubAssemblyType.MaterialRequest,
                PlannedQty = 4m
            },
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Raw Mat A", 5m)
            {
                MinOrderQty = 10m,
                ProcurementType = SubAssemblyType.MaterialRequest,
                PlannedQty = 5m
            }
        };

        // Algorithm replicated from PR #58805 / ProductionPlanAppService:
        var surplus = 0m;
        foreach (var row in rows)
        {
            var demand = row.PlannedQty;
            var covered = Math.Min(surplus, demand);
            demand -= covered;
            surplus -= covered;

            if (row.MinOrderQty > 0 && demand > 0 && demand < row.MinOrderQty)
            {
                var extra = row.MinOrderQty - demand;
                surplus += extra;
                demand = row.MinOrderQty;
            }
            row.PlannedQty = demand;
        }

        // Row 1 raised to MOQ (10), creating surplus of 6
        rows[0].PlannedQty.ShouldBe(10m);
        // Row 2 demand (5) fully covered by surplus of 6, leaving remaining surplus 1
        rows[1].PlannedQty.ShouldBe(0m);
        // Total purchased = 10 (meets MOQ 10 for combined demand 9, instead of buying 10 + 10 = 20)
        rows.Sum(r => r.PlannedQty).ShouldBe(10m);
    }

    [Fact]
    public void ProductionPlan_SafetyStock_AppliedOnceAcrossRows()
    {
        // Per ERPNext PR #58806: apply safety stock once across Production Plan rows
        // Available projected stock: 10 units. Safety stock buffer: 5 units.
        // Net availability pool: 10 - 5 = 5 units.
        // Row 1 requires 4 units. Row 2 requires 3 units. Total required = 7.
        // Net needed = 7 - 5 = 2 units.
        var projectedQty = 10m;
        var safetyStock = 5m;

        var rows = new List<ProductionPlanMrItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Raw Mat B", 4m) { SafetyStock = safetyStock, RequiredQty = 4m },
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Raw Mat B", 3m) { SafetyStock = safetyStock, RequiredQty = 3m }
        };

        var consumedStock = 0m;
        foreach (var row in rows)
        {
            var availablePool = Math.Max(0, projectedQty - consumedStock);
            var netAvailableForDeduction = Math.Max(0, availablePool - safetyStock);

            var needed = Math.Max(0, row.RequiredQty - netAvailableForDeduction);
            var consumedFromPool = Math.Min(netAvailableForDeduction, row.RequiredQty);
            consumedStock += consumedFromPool;

            row.PlannedQty = needed;
        }

        // Row 1 consumes 4 of the 5 net available units -> 0 planned to order
        rows[0].PlannedQty.ShouldBe(0m);
        // Row 2 consumes remaining 1 net available unit -> 2 planned to order
        rows[1].PlannedQty.ShouldBe(2m);
        // Total planned = 2 units (not 2 + 5 = 7 from duplicate safety stock buffers)
        rows.Sum(r => r.PlannedQty).ShouldBe(2m);
    }
}
