using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 6:
/// - d0796ff33b &amp; 0de4680ea9: Pick List keeps ordered conversion factor on a pick list row only for the ordered item and UOM.
/// - aeb5278994, 0146c3b6d3 &amp; 7a711015db: Subcontracting Receipt return row treated as rejected only while flagged and in the rejected warehouse.
/// </summary>
public class UpstreamBatch6ParityTests
{
    [Fact]
    public void PickListManager_ResolveRowUomAndStockQty_PreservesOrderedConversionFactorOnlyForOrderedItem_d0796ff33b_0de4680ea9()
    {
        var orderedRowId = Guid.NewGuid();
        var orderedItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();

        // Sales Order / Material Request row was ordered with 1 Box = 10 Nos,
        // before the Item's Box conversion factor in item master was edited to 25.
        var orderedFactors = new Dictionary<(Guid RowId, Guid ItemId, string Uom), decimal>
        {
            [(orderedRowId, orderedItemId, "BOX")] = 10m
        };

        // 1) Matching ordered row, item, and UOM -> keeps ordered conversion factor 10 (not item master 25)
        var (uom1, factor1, stockQty1) = PickListManager.ResolveRowUomAndStockQty(
            orderedItemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 2m,
            sourceDocumentItemId: orderedRowId,
            productBundleItemId: null,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 25m);

        uom1.ShouldBe("Box");
        factor1.ShouldBe(10m);
        stockQty1.ShouldBe(20m);

        // 2) Same ordered row ID, but different item (e.g. replaced on pick list row per 0de4680ea9) -> uses item master factor
        var (_, factor2, stockQty2) = PickListManager.ResolveRowUomAndStockQty(
            otherItemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 2m,
            sourceDocumentItemId: orderedRowId,
            productBundleItemId: null,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 6m);

        factor2.ShouldBe(6m);
        stockQty2.ShouldBe(12m);

        // 3) Product bundle component row linked to ordered bundle row -> uses component's own item master factor
        var (_, factor3, stockQty3) = PickListManager.ResolveRowUomAndStockQty(
            orderedItemId,
            stockUom: "Nos",
            rowUom: "Box",
            qty: 2m,
            sourceDocumentItemId: orderedRowId,
            productBundleItemId: Guid.NewGuid(),
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 25m);

        factor3.ShouldBe(25m);
        stockQty3.ShouldBe(50m);

        // 4) Row UOM equals stock UOM -> always conversion factor 1
        var (_, factor4, stockQty4) = PickListManager.ResolveRowUomAndStockQty(
            orderedItemId,
            stockUom: "Nos",
            rowUom: "Nos",
            qty: 3m,
            sourceDocumentItemId: orderedRowId,
            productBundleItemId: null,
            orderedConversionFactors: orderedFactors,
            itemMasterConversionFactor: 25m);

        factor4.ShouldBe(1m);
        stockQty4.ShouldBe(3m);
    }

    [Fact]
    public void SubcontractingManager_IsRejectedSubcontractingReturn_And_GetSubcontractingReturnWarehouse_aeb5278994_0146c3b6d3_7a711015db()
    {
        var acceptedWh = Guid.NewGuid();
        var rejectedWh = Guid.NewGuid();

        // 1) Flagged AND in rejected warehouse -> true, return warehouse is rejectedWh
        SubcontractingManager.IsRejectedSubcontractingReturn(true, rejectedWh, rejectedWh).ShouldBeTrue();
        SubcontractingManager.GetSubcontractingReturnWarehouse(true, rejectedWh, acceptedWh, rejectedWh).ShouldBe(rejectedWh);

        // 2) Flagged, but warehouse was changed to accepted warehouse -> false, return warehouse is acceptedWh
        SubcontractingManager.IsRejectedSubcontractingReturn(true, acceptedWh, rejectedWh).ShouldBeFalse();
        SubcontractingManager.GetSubcontractingReturnWarehouse(true, acceptedWh, acceptedWh, rejectedWh).ShouldBe(acceptedWh);

        // 3) Unflagged, even if warehouse is rejected warehouse -> false, return warehouse is acceptedWh
        SubcontractingManager.IsRejectedSubcontractingReturn(false, rejectedWh, rejectedWh).ShouldBeFalse();
        SubcontractingManager.GetSubcontractingReturnWarehouse(false, rejectedWh, acceptedWh, rejectedWh).ShouldBe(acceptedWh);
    }
}
