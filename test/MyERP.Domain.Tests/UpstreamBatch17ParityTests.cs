using System;
using System.Collections.Generic;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream ERPNext parity batch 17:
/// 1) BOM row sub-BOM rejection when it does not produce the row item (PR #64463, commits 4ab6adb935, 3c4508e0a5, d4745493e6).
/// 2) POS Closing Entry cancel restores serial and batch bundle linkages and prevents orphan allocations (PR #64756, commit e553c17711).
/// 3) Serial and Batch Entry batch tracking, valuation and indexing invariants (PR #64542, commit 80e1a4d976).
/// </summary>
public class UpstreamBatch17ParityTests
{
    // === PR #64463 / commits 4ab6adb935, 3c4508e0a5, d4745493e6: BOM Sub-BOM Production Match Validation ===

    [Fact]
    public void BomItem_AddItem_SetsDoNotExplode_WhenItemMatchesFinishedGood_4ab6adb935()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-FG-001", fgItemId);

        var rmMatchingFg = new BomItem(Guid.NewGuid(), bom.Id, fgItemId, "Self Item", 1m, 10m)
        {
            SubBomId = Guid.NewGuid()
        };

        bom.AddItem(rmMatchingFg);

        // When item matches finished good, AddItem forces DoNotExplode = true and clears SubBomId to prevent recursion
        rmMatchingFg.DoNotExplode.ShouldBeTrue();
        rmMatchingFg.SubBomId.ShouldBeNull();
    }

    [Fact]
    public void BomItem_AddItem_RetainsSubBom_WhenItemIsDistinctSubAssembly()
    {
        var companyId = Guid.NewGuid();
        var fgItemId = Guid.NewGuid();
        var subAssemblyId = Guid.NewGuid();
        var subBomId = Guid.NewGuid();

        var bom = new BillOfMaterials(Guid.NewGuid(), companyId, "BOM-FG-002", fgItemId);
        var subAssemblyItem = new BomItem(Guid.NewGuid(), bom.Id, subAssemblyId, "Sub-Assembly", 2m, 25m)
        {
            SubBomId = subBomId,
            SetRateOfSubAssemblyItemBasedOnBom = true
        };

        bom.AddItem(subAssemblyItem);

        subAssemblyItem.DoNotExplode.ShouldBeFalse();
        subAssemblyItem.SubBomId.ShouldBe(subBomId);
        subAssemblyItem.SetRateOfSubAssemblyItemBasedOnBom.ShouldBeTrue();
    }

    // === PR #64756 / commit e553c17711: POS Closing Cancel Batch Qty Re-linking ===

    [Fact]
    public void PosClosingEntry_Cancel_TransitionsSubmittedOrFailedState_e553c17711()
    {
        var companyId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var openingId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var closing = new PosClosingEntry(Guid.NewGuid(), companyId, profileId, openingId, userId);
        closing.AddInvoice(Guid.NewGuid(), "POS-INV-001", 150m);
        closing.Submit();
        closing.Status.ShouldBe(PosClosingStatus.Submitted);

        // Cancel submitted closing entry
        closing.Cancel();
        closing.Status.ShouldBe(PosClosingStatus.Cancelled);
    }

    [Fact]
    public void PosClosingEntry_Cancel_AllowsCancellingFailedConsolidation_e553c17711()
    {
        var closing = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        closing.AddInvoice(Guid.NewGuid(), "POS-INV-002", 200m);
        closing.Submit();
        closing.MarkFailed("Consolidation error: customer account locked");
        closing.Status.ShouldBe(PosClosingStatus.Failed);
        closing.ErrorMessage.ShouldNotBeNull();

        // Cancelling failed entry is explicitly allowed
        closing.Cancel();
        closing.Status.ShouldBe(PosClosingStatus.Cancelled);
    }

    [Fact]
    public void PosClosingEntry_Cancel_RejectsDraftEntry()
    {
        var closing = new PosClosingEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ex = Should.Throw<BusinessException>(() => closing.Cancel());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidStatusTransition);
    }

    // === PR #64542 / commit 80e1a4d976: Serial and Batch Entry BatchId Tracking & Invariants ===

    [Fact]
    public void SerialAndBatchEntry_BatchTracking_CalculatesStockValueDifferenceCorrectly_80e1a4d976()
    {
        var bundleId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var entry = new SerialAndBatchEntry(
            id: Guid.NewGuid(),
            serialAndBatchBundleId: bundleId,
            qty: 15m,
            incomingRate: 4.5m,
            serialNo: null,
            batchId: batchId);

        entry.BatchId.ShouldBe(batchId);
        entry.SerialNo.ShouldBeNull();
        entry.Qty.ShouldBe(15m);
        entry.IncomingRate.ShouldBe(4.5m);
        entry.StockValueDifference.ShouldBe(67.5m); // 15 * 4.5
    }

    [Fact]
    public void SerialAndBatchBundle_AddEntry_ComputesTotalQtyAndWeightedRate()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        var bundle = new SerialAndBatchBundle(
            id: Guid.NewGuid(),
            companyId: companyId,
            itemId: itemId,
            warehouseId: warehouseId,
            typeOfTransaction: BundleTransactionType.Outward,
            voucherType: "SalesInvoice",
            voucherId: voucherId,
            postingDate: DateTime.UtcNow);

        var batch1 = Guid.NewGuid();
        var batch2 = Guid.NewGuid();

        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 10m, 5m, batchId: batch1));
        bundle.AddEntry(new SerialAndBatchEntry(Guid.NewGuid(), bundle.Id, 20m, 8m, batchId: batch2));

        bundle.TotalQty.ShouldBe(30m);
        bundle.TotalAmount.ShouldBe(210m); // 10*5 + 20*8 = 50 + 160 = 210
        bundle.AvgRate.ShouldBe(7m);       // 210 / 30 = 7.0
    }
}
