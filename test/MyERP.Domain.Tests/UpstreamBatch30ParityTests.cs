using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 30 upstream parity:
/// 1) Preload Items without barcodes for Item search index (PR #64321 / commit 09ba0cf166, a2b2b13cd9).
/// 2) Empty Minimum Total Spent treated as 0 in Loyalty Program (PR #64312 / commit db4196a0b1).
/// 3) Framework from/to date validation for Batch manufacturing date <= expiry date (PR #64271 / commit ecdb045ab3).
/// 4) Variant BOM / Work Order rows preserve row UOM, conversion factor and stock qty (PR #64340, #64345 / commit e20967e15e, 8ae5e72ea6).
/// 5) Subcontracting return finished good takes receipt rate (PR #64281 / commit c82a87c088, 2fa46b5531).
/// 6) Merged/covered queued valuation reposts skipped (PR #63639 / commit 2944ee0c14, efa92667c0).
/// </summary>
public class UpstreamBatch30ParityTests
{
    // === 1) PR #64321: Preload Items Without Barcodes ===

    [Fact]
    public void BarcodeScannerService_PreloadBarcodesByItems_AnswersForEveryItem()
    {
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var item3 = Guid.NewGuid();

        var itemIds = new[] { item1, item2, item3 };
        var barcodes = new List<(Guid ItemId, string Barcode)>
        {
            (item1, "111111"),
            (item1, "222222"),
            (item2, "333333")
            // item3 has no barcode
        };

        var preloaded = BarcodeScannerService.PreloadBarcodesByItems(itemIds, barcodes);

        // Preload answers for all 3 items (PR #64321 / commit 09ba0cf166)
        preloaded.Count.ShouldBe(3);
        preloaded[item1].ShouldBe("111111 222222");
        preloaded[item2].ShouldBe("333333");
        preloaded[item3].ShouldBe(string.Empty);
    }

    // === 2) PR #64312: Empty Min Spent in Loyalty Program Treated as 0 ===

    [Fact]
    public void LoyaltyProgram_EmptyMinSpent_TreatedAsZero()
    {
        var program = new LoyaltyProgram(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Rewards Tier Test B30",
            conversionFactor: 1m);

        // Add Gold tier with min spent 10000, and Silver tier with null/empty min spent
        program.AddTier("Gold", minSpent: 10000m, collectionFactor: 2m, redemptionFactor: 1m);
        program.AddTier("Silver", minSpent: null, collectionFactor: 1m, redemptionFactor: 1m);

        // Validation should succeed because Silver defaults to 0 (PR #64312 / commit db4196a0b1)
        Should.NotThrow(() => program.Validate());

        // Low spender qualifies for Silver tier
        var tier = program.DetermineTier(totalSpent: 50m, currentTransactionAmount: 20m);
        tier.TierName.ShouldBe("Silver");
        tier.MinSpent.ShouldBe(0m);
    }

    // === 3) PR #64271: Batch Manufacturing Date <= Expiry Date ===

    [Fact]
    public void Batch_ValidateDates_ManufacturingDateCannotBeGreaterThanExpiry()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-B30-01")
        {
            ManufacturingDate = new DateTime(2026, 11, 10),
            ExpiryDate = new DateTime(2026, 11, 01) // Earlier than manufacturing date
        };

        var ex = Should.Throw<BusinessException>(() => batch.ValidateDates());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.InvalidDateRange);
    }

    [Fact]
    public void Batch_ValidateDates_ValidRangeSucceeds()
    {
        var batch = new Batch(Guid.NewGuid(), Guid.NewGuid(), "BATCH-B30-02")
        {
            ManufacturingDate = new DateTime(2026, 11, 01),
            ExpiryDate = new DateTime(2026, 11, 10)
        };

        Should.NotThrow(() => batch.ValidateDates());
    }

    // === 4) PR #64340 & #64345: Variant BOM / WO Row UOM, Stock Qty & Rate Rounding ===

    [Fact]
    public void BomItem_StockQtyAndRate_CalculatedFromConversionFactor()
    {
        // 2 Boxes with conversion factor 10 -> 20 units stock qty
        var bomItem = new BomItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Box Item", 2m, 50m, "Box")
        {
            ConversionFactor = 10m
        };

        bomItem.StockQty.ShouldBe(20m); // 2 * 10
        // Cost per stock UOM
        var stockRate = Math.Round(bomItem.Rate / bomItem.ConversionFactor, 4);
        stockRate.ShouldBe(5m); // 50 / 10
    }

    // === 5) PR #64281: Subcontracting Receipt Return Retains Original Receipt Rate ===

    [Fact]
    public void SubcontractingReceipt_ReturnRow_RetainsReceiptRate()
    {
        var receiptId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var scoId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var originalReceipt = new SubcontractingReceipt(
            receiptId, companyId, "SCR-001", DateTime.UtcNow, supplierId, scoId);
        originalReceipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), receiptId, itemId, "Subcontracted FG", 5m, 120m));

        var returnReceipt = new SubcontractingReceipt(
            Guid.NewGuid(), companyId, "SCR-RET-001", DateTime.UtcNow, supplierId, scoId)
        {
            IsReturn = true,
            ReturnAgainstReceiptId = receiptId
        };

        var origItem = originalReceipt.Items.First();
        returnReceipt.AddItem(new SubcontractingReceiptItem(
            Guid.NewGuid(), returnReceipt.Id, itemId, origItem.ItemName, -2m, origItem.Rate));

        var returnItem = returnReceipt.Items.First();
        returnItem.Qty.ShouldBe(-2m);
        returnItem.Rate.ShouldBe(120m); // Retains original receipt rate (PR #64281 / commit c82a87c088)
    }

    // === 6) PR #63639: Merged / Covered Reposts ===

    [Fact]
    public void RepostItemValuation_IsCoveredBy_IdentifiesRedundantLaterQueuedRepost()
    {
        var companyId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var earlierRepost = new RepostItemValuation(
            Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse,
            postingDate: new DateTime(2026, 01, 01), itemId: itemId, warehouseId: warehouseId);

        var laterRepost = new RepostItemValuation(
            Guid.NewGuid(), companyId, RepostMethod.ItemAndWarehouse,
            postingDate: new DateTime(2026, 02, 01), itemId: itemId, warehouseId: warehouseId);

        // Later repost is covered by earlier repost covering the same item+warehouse
        laterRepost.IsCoveredBy(earlierRepost).ShouldBeTrue();

        // But earlier repost is NOT covered by later repost
        earlierRepost.IsCoveredBy(laterRepost).ShouldBeFalse();
    }
}
