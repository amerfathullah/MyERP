using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 26:
/// 1) Pick list bundle with repeated component item keeps undelivered bundles pending (PR #65051 / commit fe42da0c74, cfa681da8c).
/// 2) Enforce warehouse access in batch quantity calculations (PR #64314 / commit cbc40ba340, 34e791febb, 55fd06a510).
/// 3) Item search index folds diacritics, kana, and character width (PR #65020 / commit 77c5fd1722, 88fabb805e).
/// </summary>
public class UpstreamBatch26ParityTests
{
    // === 1) PR #65051: Pick List Product Bundle with Repeated Component Item ===

    [Fact]
    public void ComputePickedQtyForBundle_WithRepeatedComponent_CalculatesFullAndPendingBundlesAccurately()
    {
        // Equivalent to test_bundle_with_repeated_component_keeps_undelivered_bundles_pending
        // Product bundle contains:
        // Component X: 1 unit
        // Component X: 2 units
        // Total required per bundle = 3 units of Component X
        var componentId = Guid.NewGuid();
        var soItemId = Guid.NewGuid();

        var requiredQuantities = new Dictionary<Guid, decimal>
        {
            [componentId] = 3m // 1 + 2
        };

        // Pick list picked 9 units of Component X (across locations/rows) for 3 full bundles
        var pickItems = new List<PickListItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), componentId, Guid.NewGuid(), qty: 4m, stockQty: 4m, itemName: "Comp X", batchId: null)
            {
                SourceDocumentItemId = soItemId,
                DeliveredQty = 0m
            },
            new(Guid.NewGuid(), Guid.NewGuid(), componentId, Guid.NewGuid(), qty: 5m, stockQty: 5m, itemName: "Comp X", batchId: null)
            {
                SourceDocumentItemId = soItemId,
                DeliveredQty = 0m
            }
        };

        // 1) Compute full picked bundles
        var pickedBundles = PickListManager.ComputePickedQtyForBundle(
            pickItems, soItemId, requiredQuantities, pendingOnly: false);

        pickedBundles.ShouldBe(3); // 9 / 3 = 3 bundles

        // 2) Partial delivery: 1 bundle delivered (3 units delivered: 1 from row1, 2 from row2)
        pickItems[0].DeliveredQty = 1m;
        pickItems[1].DeliveredQty = 2m;

        // Remaining undelivered units = (4 - 1) + (5 - 2) = 3 + 3 = 6 units
        var pendingBundles = PickListManager.ComputePickedQtyForBundle(
            pickItems, soItemId, requiredQuantities, pendingOnly: true);

        pendingBundles.ShouldBe(2); // 6 / 3 = 2 undelivered bundles remaining pending
    }

    [Fact]
    public void ComputePickedQtyForBundle_WithMultipleDistinctAndRepeatedComponents_TakesMinimum()
    {
        var compA = Guid.NewGuid();
        var compB = Guid.NewGuid();
        var soItemId = Guid.NewGuid();

        // Bundle requires: CompA = 2 (1+1 repeated), CompB = 5
        var requiredQuantities = new Dictionary<Guid, decimal>
        {
            [compA] = 2m,
            [compB] = 5m
        };

        var pickItems = new List<PickListItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), compA, Guid.NewGuid(), qty: 10m, stockQty: 10m, itemName: "Comp A", batchId: null) { SourceDocumentItemId = soItemId },
            new(Guid.NewGuid(), Guid.NewGuid(), compB, Guid.NewGuid(), qty: 15m, stockQty: 15m, itemName: "Comp B", batchId: null) { SourceDocumentItemId = soItemId }
        };

        // CompA allows: 10 / 2 = 5 bundles
        // CompB allows: 15 / 5 = 3 bundles
        // Result is min(5, 3) = 3 bundles
        var pickedBundles = PickListManager.ComputePickedQtyForBundle(
            pickItems, soItemId, requiredQuantities);

        pickedBundles.ShouldBe(3);
    }

    // === 2) PR #64314: Enforce Warehouse Access in Batch Quantities ===

    [Fact]
    public void BatchWarehousePermissionValidator_ValidateWarehouseAccess_AllowedWarehouse_Succeeds()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        var permitted = new HashSet<Guid> { wh1, wh2 };

        Should.NotThrow(() =>
            BatchWarehousePermissionValidator.ValidateWarehouseAccess(wh1, permitted));
    }

    [Fact]
    public void BatchWarehousePermissionValidator_ValidateWarehouseAccess_RestrictedWarehouse_Throws()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        var whRestricted = Guid.NewGuid();
        var permitted = new HashSet<Guid> { wh1, wh2 };

        var ex = Should.Throw<BusinessException>(() =>
            BatchWarehousePermissionValidator.ValidateWarehouseAccess(whRestricted, permitted));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.WarehouseAccessRestricted);
        ex.Data["warehouseId"].ShouldBe(whRestricted);
    }

    [Fact]
    public void BatchWarehousePermissionValidator_FilterByPermittedWarehouses_RestrictsToPermittedOnly()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();
        var whRestricted = Guid.NewGuid();
        var permitted = new HashSet<Guid> { wh1, wh2 };

        var balances = new List<(Guid WhId, decimal Qty)>
        {
            (wh1, 10m),
            (wh2, 20m),
            (whRestricted, 50m)
        };

        var filtered = BatchWarehousePermissionValidator.FilterByPermittedWarehouses(
            balances, b => b.WhId, permitted);

        filtered.Count.ShouldBe(2);
        filtered.ShouldContain(b => b.WhId == wh1 && b.Qty == 10m);
        filtered.ShouldContain(b => b.WhId == wh2 && b.Qty == 20m);
        filtered.ShouldNotContain(b => b.WhId == whRestricted);
    }

    [Fact]
    public void BatchWarehousePermissionValidator_FilterByPermittedWarehouses_UnrestrictedUser_ReturnsAll()
    {
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        var balances = new List<(Guid WhId, decimal Qty)>
        {
            (wh1, 10m),
            (wh2, 20m)
        };

        var filtered = BatchWarehousePermissionValidator.FilterByPermittedWarehouses(
            balances, b => b.WhId, permittedWarehouseIds: null);

        filtered.Count.ShouldBe(2);
    }

    // === 3) PR #65020: Item Search Text Normalization & Folding ===

    [Fact]
    public void ItemSearchNormalizationService_FoldText_AccentedItemsFoldIdentically()
    {
        // Equivalent to test_an_accented_item_is_found_by_an_unaccented_term
        var foldedItem = ItemSearchNormalizationService.FoldText("ZZ-CRÈME-7782");
        var foldedTerm = ItemSearchNormalizationService.FoldText("creme-7782");

        foldedItem.ShouldBe("ZZ-CREME-7782");
        foldedTerm.ShouldBe("creme-7782");

        // Case-folded equality
        foldedItem.ToUpperInvariant().ShouldContain(foldedTerm.ToUpperInvariant());
    }

    [Fact]
    public void ItemSearchNormalizationService_FoldText_FullWidthAndKanaFolding()
    {
        // Full-width ASCII -> standard ASCII
        var fullWidthAscii = ItemSearchNormalizationService.FoldText("ＡＢＣ１２３");
        fullWidthAscii.ShouldBe("ABC123");

        // Half-width Katakana -> Hiragana
        var halfWidthKana = ItemSearchNormalizationService.FoldText("ﾃｽﾄ");
        // Full-width Katakana -> Hiragana
        var fullWidthKana = ItemSearchNormalizationService.FoldText("テスト");
        // Plain Hiragana -> Hiragana
        var hiragana = ItemSearchNormalizationService.FoldText("てすと");

        halfWidthKana.ShouldBe("てすと");
        fullWidthKana.ShouldBe("てすと");
        hiragana.ShouldBe("てすと");

        // All 3 representations fold to the exact same Hiragana string
        halfWidthKana.ShouldBe(fullWidthKana);
        fullWidthKana.ShouldBe(hiragana);
    }

    [Fact]
    public void ItemSearchNormalizationService_FoldText_SmallKanaToFullSize()
    {
        // Small tsu and small ya fold to regular full-size kana, and voicing marks are stripped
        var folded = ItemSearchNormalizationService.FoldText("きっぷ");
        folded.ShouldBe("きつふ");

        var foldedKatakana = ItemSearchNormalizationService.FoldText("キャット");
        foldedKatakana.ShouldBe("きやつと");
    }
}
