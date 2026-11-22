using System;
using System.Collections.Generic;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Purchasing;
using MyERP.Purchasing.DomainServices;
using MyERP.Purchasing.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Parity tests for ERPNext upstream PRs #67352, #67454, #67367, #67403, #67404,
/// #67477, #67358, #67371, #67405, and #67376 (commits 5026be5275..0c62d7399b).
/// </summary>
public class UpstreamBatch38ParityTests
{
    [Fact]
    public void PR67376_LifoPurchaseReturn_ConsumesMatchingReceiptRateFromTopOfStack()
    {
        var context = new ExpectedValuationReplayEngine.ItemReplayContext(
            ValuationMethod.LIFO,
            HasSerialNo: false,
            HasBatchNo: false,
            UseSerialNoWiseValuation: false,
            DoNotUseBatchwiseValuation: false,
            CurrencyPrecision: 2,
            QtyTolerance: 0.0001m,
            ValueTolerance: 0.01m,
            BatchwiseValuedBatchIds: new HashSet<Guid>());

        var day1 = new DateTime(2026, 11, 1);
        var entries = new List<ExpectedValuationReplayEngine.LedgerEntryInput>
        {
            // Receipt 1: 5 units @ 10
            new(Guid.NewGuid(), day1, day1, day1, "PurchaseReceipt", Guid.NewGuid(), null,
                5m, 10m, 0m, 5m, 10m, 50m, 50m, null, null, null, null, false),
            // Receipt 2: 5 units @ 20 (top of LIFO stack)
            new(Guid.NewGuid(), day1.AddDays(1), day1.AddDays(1), day1.AddDays(1), "PurchaseReceipt", Guid.NewGuid(), null,
                5m, 20m, 0m, 10m, 15m, 150m, 100m, null, null, null, null, false),
            // Return 2 units against Receipt 1 (@ 10) — LIFO must consume the 10 layer, not the 20 top layer
            new(Guid.NewGuid(), day1.AddDays(2), day1.AddDays(2), day1.AddDays(2), "PurchaseReceipt", Guid.NewGuid(), null,
                -2m, 0m, 10m, 8m, 16.25m, 130m, -20m, null, null, null, null, false,
                PurchaseReturnRate: 10m),
        };

        var replay = ExpectedValuationReplayEngine.ReplayLedger(entries, context);
        var returnExpected = replay[2].Expected;

        returnExpected.StockValueDifference.ShouldBe(-20m);
        returnExpected.StockValue.ShouldBe(130m);
        returnExpected.QtyAfterTransaction.ShouldBe(8m);
        returnExpected.Basis.ShouldBe(ExpectedValuationReplayEngine.BasisPurchaseReturn);
    }

    [Fact]
    public void PR67454_ItemDetailsBatchAndSerial_ResolvesWithoutParentDoc()
    {
        var contextPriceListId = Guid.NewGuid();
        var (effectivePriceList, serials) = ItemDetailsResolverService.ResolveBatchAndSerialWithoutDoc(
            docSellingPriceListId: null,
            contextPriceListId: contextPriceListId,
            candidateSerialNos: new[] { "SN-001", "SN-002", "SN-003" },
            docUsedSerialNos: null,
            extraIgnoredSerialNos: new[] { "SN-001" },
            stockQty: 2);

        effectivePriceList.ShouldBe(contextPriceListId);
        serials.ShouldBe(new[] { "SN-002", "SN-003" });
    }

    [Fact]
    public void PR67367_SubcontractingMaterialSentToSupplier_UsesStockUomAndNegatesReturns()
    {
        var rmItemId = Guid.NewGuid();
        var sent = SubcontractingManager.CalculateMaterialSentToSupplierInStockUom(new[]
        {
            (rmItemId, "Nos", 24m, false), // 2 boxes * 12 Nos/box = 24 transfer_qty
            (rmItemId, "Nos", 6m, true),   // return 6 Nos
        });

        sent.ShouldContainKey(rmItemId);
        sent[rmItemId].StockQty.ShouldBe(18m);
        sent[rmItemId].StockUom.ShouldBe("Nos");
        sent[rmItemId].ConversionFactor.ShouldBe(1m);
    }

    [Fact]
    public void PR67403_SubcontractingAdditionalCostAccounts_RefusesNonCompanyCurrencyExceptReturnOrInward()
    {
        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingManager.ValidateAdditionalCostAccountCurrencies(
                "MYR",
                new[] { (1, "Freight USD", (string?)"USD") }));
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("is not in the company currency MYR");

        // Same currency passes
        Should.NotThrow(() =>
            SubcontractingManager.ValidateAdditionalCostAccountCurrencies(
                "MYR",
                new[] { (1, "Freight MYR", (string?)"MYR") }));

        // Return receipt and Subcontracting Inward Order skip validation
        Should.NotThrow(() =>
            SubcontractingManager.ValidateAdditionalCostAccountCurrencies(
                "MYR",
                new[] { (1, "Freight USD", (string?)"USD") },
                isReturn: true));
        Should.NotThrow(() =>
            SubcontractingManager.ValidateAdditionalCostAccountCurrencies(
                "MYR",
                new[] { (1, "Freight USD", (string?)"USD") },
                isInwardOrder: true));
    }

    [Fact]
    public void PR67404_SubcontractingRejectedReturn_DetectsRemainingAndDropsExhaustedRows()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var scoId = Guid.NewGuid();
        var fg1 = Guid.NewGuid();
        var fg2 = Guid.NewGuid();
        var accWh = Guid.NewGuid();
        var rejWh = Guid.NewGuid();

        var receipt = new SubcontractingReceipt(Guid.NewGuid(), companyId, "SCR-001", DateTime.Today, supplierId, scoId)
        {
            WarehouseId = accWh
        };
        receipt.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), receipt.Id, fg1, "FG-1", 8m, 50m)
        {
            WarehouseId = accWh,
            RejectedQty = 2m,
            RejectedWarehouseId = rejWh
        });
        receipt.Submit();

        // Before any return -> rejected qty remains
        SubcontractingManager.HasRejectedQtyToReturn(receipt, Array.Empty<SubcontractingReceipt>()).ShouldBeTrue();

        // Submitted return taking back all 2 rejected units -> no rejected qty left
        var ret = new SubcontractingReceipt(Guid.NewGuid(), companyId, "SCR-RET-1", DateTime.Today, supplierId, scoId)
        {
            IsReturn = true,
            ReturnAgainstReceiptId = receipt.Id,
            WarehouseId = rejWh
        };
        ret.AddItem(new SubcontractingReceiptItem(Guid.NewGuid(), ret.Id, fg1, "FG-1", -2m, 0m)
        {
            WarehouseId = rejWh,
            RejectedWarehouseId = rejWh,
            ReturnQtyFromRejectedWarehouse = true
        });
        ret.Submit();

        SubcontractingManager.HasRejectedQtyToReturn(receipt, new[] { ret }).ShouldBeFalse();

        // FilterReturnableRejectedRows drops exhausted fg1 and keeps fg2 with remaining qty
        var kept = SubcontractingManager.FilterReturnableRejectedRows(
            "Subcontracting Receipt",
            "SCR-001",
            new[]
            {
                (fg1, 2m, 2m),
                (fg2, 5m, 1m),
            });
        kept.Count.ShouldBe(1);
        kept[0].ItemId.ShouldBe(fg2);
        kept[0].RemainingRejectedQty.ShouldBe(4m);

        // Throws when all rows are already fully returned
        var ex = Should.Throw<BusinessException>(() =>
            SubcontractingManager.FilterReturnableRejectedRows(
                "Subcontracting Receipt",
                "SCR-001",
                new[] { (fg1, 2m, 2m) }));
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldBe("All rejected qty of Subcontracting Receipt SCR-001 is already returned.");
    }

    [Fact]
    public void PR67477_ProductionPlanCombineFetchedSoItems_KeepsSalesOrderAndBundleLinks()
    {
        var so1 = Guid.NewGuid();
        var so2 = Guid.NewGuid();
        var soItem1 = Guid.NewGuid();
        var soItem2 = Guid.NewGuid();
        var bundleItem = Guid.NewGuid();
        var fgItem = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var bomId = Guid.NewGuid();

        var combined = ProductionPlanMaterialPlanningService.CombineFetchedSalesOrderItems(
            new[]
            {
                new FetchedSalesOrderPlanRow(so1, soItem1, fgItem, whId, bomId, 4m, bundleItem),
                new FetchedSalesOrderPlanRow(so2, soItem2, fgItem, whId, bomId, 6m, null),
            },
            combineItems: true);

        combined.Count.ShouldBe(1);
        combined[0].PlannedQty.ShouldBe(10m);
        combined[0].SalesOrderLinks.Count.ShouldBe(2);
        combined[0].SalesOrderLinks[0].SalesOrderItemId.ShouldBe(soItem1);
        combined[0].SalesOrderLinks[0].ProductBundleItemId.ShouldBe(bundleItem);
        combined[0].SalesOrderLinks[1].SalesOrderItemId.ShouldBe(soItem2);
    }

    [Fact]
    public void PR67358_SubcontractingReservationPartition_FallsBackToDirectStockWhenPlanRowMissing()
    {
        var planId = Guid.NewGuid();
        var subAsmId = Guid.NewGuid();
        var rmWithPlan = Guid.NewGuid();
        var rmWithoutPlan = Guid.NewGuid();
        var whId = Guid.NewGuid();
        var planRowId = Guid.NewGuid();

        var (planHandoffs, directStock) = SubcontractingManager.PartitionSubcontractingReservationsForProductionPlan(
            planId,
            new[]
            {
                (Guid.NewGuid(), rmWithPlan, (Guid?)whId, (Guid?)subAsmId, 10m, 0m),
                (Guid.NewGuid(), rmWithoutPlan, (Guid?)whId, (Guid?)subAsmId, 5m, 0m),
            },
            new Dictionary<(Guid, Guid), (Guid, decimal)>
            {
                [(subAsmId, rmWithPlan)] = (planRowId, 10m),
            });

        planHandoffs.Count.ShouldBe(1);
        planHandoffs[0].RmItemId.ShouldBe(rmWithPlan);
        planHandoffs[0].ProductionPlanMrItemId.ShouldBe(planRowId);

        directStock.Count.ShouldBe(1);
        directStock[0].RmItemId.ShouldBe(rmWithoutPlan);
        directStock[0].RequiredQty.ShouldBe(5m);
    }

    [Fact]
    public void PR67371_SendToSubcontractorTransfer_RefusesInvalidSubcontractedItemAndRelinksStaleDetail()
    {
        var fgOnOrder = Guid.NewGuid();
        var otherFgOnOrder = Guid.NewGuid();
        var unrelatedFg = Guid.NewGuid();
        var rmId = Guid.NewGuid();
        var rmRowForOtherFg = Guid.NewGuid();
        var rmRowForFgOnOrder = Guid.NewGuid();

        // Missing Subcontracted Item -> throws
        var missingEx = Should.Throw<BusinessException>(() =>
            SubcontractingRmTransferService.ValidateTransferSubcontractedItems(
                "Subcontracting Order",
                "SCO-001",
                new HashSet<Guid> { fgOnOrder },
                new Dictionary<Guid, Guid>(),
                new[] { (1, rmId, "RM-1", (Guid?)null, (string?)null, (Guid?)null) }));
        (missingEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Subcontracted Item is mandatory");

        // Subcontracted Item not on SCO -> throws
        var invalidEx = Should.Throw<BusinessException>(() =>
            SubcontractingRmTransferService.ValidateTransferSubcontractedItems(
                "Subcontracting Order",
                "SCO-001",
                new HashSet<Guid> { fgOnOrder },
                new Dictionary<Guid, Guid>(),
                new[] { (1, rmId, "RM-1", (Guid?)unrelatedFg, (string?)"FG-OTHER", (Guid?)null) }));
        (invalidEx.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("is not a finished good of Subcontracting Order SCO-001");

        // Stale rm_detail pointing to otherFgOnOrder -> re-resolved to rmRowForFgOnOrder
        var resolved = SubcontractingRmTransferService.ValidateTransferSubcontractedItems(
            "Subcontracting Order",
            "SCO-001",
            new HashSet<Guid> { fgOnOrder, otherFgOnOrder },
            new Dictionary<Guid, Guid>
            {
                [rmRowForOtherFg] = otherFgOnOrder,
                [rmRowForFgOnOrder] = fgOnOrder,
            },
            new[] { (1, rmId, "RM-1", (Guid?)fgOnOrder, (string?)"FG-1", (Guid?)rmRowForOtherFg) },
            resolveOrderRmDetailId: (_, _) => rmRowForFgOnOrder);

        resolved.Count.ShouldBe(1);
        resolved[0].ResolvedOrderRmDetailId.ShouldBe(rmRowForFgOnOrder);
    }

    [Fact]
    public void PR67405_SubcontractingReceiptToPurchaseReceipt_IncludesProcessLossQtyInServiceQty()
    {
        // Service ratio 2 service units per FG unit; 8 accepted + 2 process loss = 10 worked units -> 20 service qty
        var (serviceQty, rejectedServiceQty) = SubcontractingManager
            .CalculatePurchaseReceiptServiceQuantitiesFromSubcontractingReceipt(
                serviceToFgRatio: 2m,
                new[]
                {
                    (AcceptedQty: 8m, ProcessLossQty: 2m, RejectedQty: 1m),
                });

        serviceQty.ShouldBe(20m);
        rejectedServiceQty.ShouldBe(2m);
    }
}
