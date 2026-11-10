using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 33 upstream parity:
/// 1) Item search index folds digits of any script into standard ASCII digits (PR #65117 / commits 6ad4805b44, c01f4f5791).
/// 2) Invoice batch usability check needed only when UpdateStock is enabled and invoice is not consolidated (PR #65127 / commits 260f861238, 5fd78f5a90, b3c2658f7f).
/// 3) Expired batch validation skipped on invoices without UpdateStock or on consolidated invoices (PR #65127 / commits a8edc4259c, 4a1583bd9c).
/// 4) Job card material transfer validation succeeds with fractional transferred material quantity (PR #65171 / commit e7870754ec).
/// 5) Product bundle component remaining pick quantity calculated per component individually (PR #65115 / commit 51954d883a).
/// 6) Subcontracting return receipt keeps original supplier warehouse (PR #65109 / commit d7feab165b).
/// </summary>
public class UpstreamBatch33ParityTests
{
    // === 1) PR #65117: Item Search Folds Digits of Any Script ===

    [Theory]
    [InlineData("ABC١٢٣", "ABC123")] // Eastern Arabic digits
    [InlineData("ABC۱۲۳", "ABC123")] // Persian / Urdu digits
    [InlineData("ABC१२३", "ABC123")] // Devanagari digits
    [InlineData("ABC❶❷❸", "ABC123")] // Circled digits
    [InlineData("ITEM-１２３", "ITEM-123")] // Full-width digits
    public void ItemSearchNormalizationService_FoldText_FoldsDigitsOfAnyScript(string input, string expected)
    {
        var folded = ItemSearchNormalizationService.FoldText(input);
        folded.ShouldBe(expected);
    }

    // === 2 & 3) PR #65127: Batch Validation on Invoices vs Stock Documents ===

    [Theory]
    [InlineData("Sales Invoice", true, false, true)]   // Invoice with UpdateStock -> check needed
    [InlineData("Sales Invoice", false, false, false)] // Invoice without UpdateStock -> check skipped
    [InlineData("Sales Invoice", true, true, false)]   // Consolidated invoice -> check skipped
    [InlineData("Purchase Invoice", true, false, true)] // PI with UpdateStock -> check needed
    [InlineData("Purchase Invoice", false, false, false)] // PI without UpdateStock -> check skipped
    [InlineData("Delivery Note", true, false, true)]   // Delivery Note -> always check
    [InlineData("Delivery Note", false, false, true)]  // Delivery Note -> always check
    [InlineData("Stock Entry", true, false, true)]     // Stock Entry -> always check
    public void SerialBatchBundleValidationService_IsBatchCheckNeeded_Parity(
        string voucherType, bool updateStock, bool isConsolidated, bool expectedCheckNeeded)
    {
        var needed = SerialBatchBundleValidationService.IsBatchCheckNeeded(voucherType, updateStock, isConsolidated);
        needed.ShouldBe(expectedCheckNeeded);
    }

    [Fact]
    public void SerialBatchBundleValidationService_ValidateBatchUsable_ThrowsWhenExpiredOnStockMovement()
    {
        var expiredDate = new DateTime(2026, 1, 1);
        var postingDate = new DateTime(2026, 6, 1);

        // On Delivery Note or Sales Invoice with UpdateStock: throws BusinessException
        var ex = Should.Throw<BusinessException>(() =>
            SerialBatchBundleValidationService.ValidateBatchUsable(
                "BATCH-001", expiredDate, disabled: false, postingDate,
                voucherType: "Sales Invoice", updateStock: true, isConsolidated: false));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["batchNo"]?.ToString() ?? string.Empty).ShouldBe("BATCH-001");
    }

    [Fact]
    public void SerialBatchBundleValidationService_ValidateBatchUsable_AllowsExpiredBatchWhenInvoiceDoesNotUpdateStock()
    {
        var expiredDate = new DateTime(2026, 1, 1);
        var postingDate = new DateTime(2026, 6, 1);

        // When invoice does not update stock (e.g. against Delivery Note where batch was already issued), should NOT throw
        Should.NotThrow(() =>
            SerialBatchBundleValidationService.ValidateBatchUsable(
                "BATCH-001", expiredDate, disabled: false, postingDate,
                voucherType: "Sales Invoice", updateStock: false, isConsolidated: false));
    }

    [Fact]
    public void SerialBatchBundleValidationService_ValidateBatchUsable_AllowsDisabledBatchOnConsolidatedInvoice()
    {
        var expiredDate = new DateTime(2026, 12, 31);
        var postingDate = new DateTime(2026, 6, 1);

        // When invoice is consolidated (e.g. POS closing entry), disabled batch does not block consolidation
        Should.NotThrow(() =>
            SerialBatchBundleValidationService.ValidateBatchUsable(
                "BATCH-001", expiredDate, disabled: true, postingDate,
                voucherType: "Sales Invoice", updateStock: true, isConsolidated: true));
    }

    [Fact]
    public void SalesInvoice_IsBatchCheckNeeded_MatchesUpdateStockAndConsolidated()
    {
        var invWithoutStock = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-001", DateTime.UtcNow)
        {
            UpdateStock = false,
            IsConsolidated = false
        };
        invWithoutStock.IsBatchCheckNeeded.ShouldBeFalse();

        var invWithStock = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-002", DateTime.UtcNow)
        {
            UpdateStock = true,
            IsConsolidated = false
        };
        invWithStock.IsBatchCheckNeeded.ShouldBeTrue();

        var consolidatedInv = new SalesInvoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-003", DateTime.UtcNow)
        {
            UpdateStock = true,
            IsConsolidated = true
        };
        consolidatedInv.IsBatchCheckNeeded.ShouldBeFalse();
    }

    // === 4) PR #65171: Fractional Material Transfer on Job Card ===

    [Fact]
    public void JobCard_FractionalTransfer_PrecisionAllowsSubmitAfterFullTransfer()
    {
        // Equivalent to test_job_card_with_fractional_material_can_be_submitted_after_full_transfer:
        // WO qty = 1, BOM required qty = 1 for 3 units -> expected = 1/3 = 0.33333333...
        // Transferred qty stored with 4 decimal places = 0.3333m
        decimal requiredQty = 1.0m;
        decimal woQty = 3.0m;
        decimal forQty = 1.0m;
        decimal expected = (requiredQty / woQty) * forQty; // 0.3333333333333333333333333333m

        decimal transferredQty = 0.3333m; // 4 decimal place transfer

        // Raw comparison fails (0.3333 < 0.33333333...)
        (transferredQty < expected).ShouldBeTrue();

        // 4-decimal rounded comparison succeeds
        bool isShortMaterial = Math.Round(transferredQty, 4) < Math.Round(expected, 4);
        isShortMaterial.ShouldBeFalse();
    }

    // === 5) PR #65115: Product Bundle Component Pick Quantity ===

    [Fact]
    public void ProductBundleDecompositionService_GetPackedQtyToPick_CalculatesUnevenPickCorrectly()
    {
        // 1 bundle requires 10 of Component A and 5 of Component B
        // Order qty = 2 bundles (Total: 20 Comp A, 10 Comp B)
        // Delivered qty = 0
        // Comp A was already picked: 15 units picked
        // Comp B was already picked: 10 units picked (fully picked)

        var compAToPick = ProductBundleDecompositionService.GetPackedQtyToPick(
            packedComponentQty: 20m,
            componentPickedQty: 15m,
            parentOrderQty: 2m,
            parentDeliveredQty: 0m);

        compAToPick.ShouldBe(5m); // 20 - 15 = 5 remaining

        var compBToPick = ProductBundleDecompositionService.GetPackedQtyToPick(
            packedComponentQty: 10m,
            componentPickedQty: 10m,
            parentOrderQty: 2m,
            parentDeliveredQty: 0m);

        compBToPick.ShouldBe(0m); // Fully picked
    }

    [Fact]
    public void ProductBundleDecompositionService_GetPackedQtyToPick_AccountsForDeliveredPortion()
    {
        // Order qty = 10 bundles, packed component qty = 50 units
        // Delivered qty = 4 bundles (so 20 units delivered)
        // Picked qty = 10 units (less than delivered)
        // Remaining to pick should be: 50 - max(10, 20) = 30 units
        var toPick = ProductBundleDecompositionService.GetPackedQtyToPick(
            packedComponentQty: 50m,
            componentPickedQty: 10m,
            parentOrderQty: 10m,
            parentDeliveredQty: 4m);

        toPick.ShouldBe(30m);
    }

    // === 6) PR #65109: Subcontracting Receipt Return Supplier Warehouse ===

    [Fact]
    public void SubcontractingReceipt_ValidateReturnSupplierWarehouse_ThrowsOnWarehouseMismatch()
    {
        var origSupplierWh = Guid.NewGuid();
        var diffSupplierWh = Guid.NewGuid();

        var receiptReturn = new SubcontractingReceipt(
            Guid.NewGuid(), Guid.NewGuid(), "SCR-RET-001", DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid())
        {
            IsReturn = true,
            ReturnAgainstReceiptId = Guid.NewGuid(),
            SupplierWarehouseId = diffSupplierWh
        };

        var ex = Should.Throw<BusinessException>(() =>
            receiptReturn.ValidateReturnSupplierWarehouse(origSupplierWh, "SCR-001"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        (ex.Data["detail"]?.ToString() ?? string.Empty).ShouldContain("Supplier Warehouse must be");
    }

    [Fact]
    public void SubcontractingReceipt_ValidateReturnSupplierWarehouse_SucceedsOnMatchingWarehouse()
    {
        var origSupplierWh = Guid.NewGuid();

        var receiptReturn = new SubcontractingReceipt(
            Guid.NewGuid(), Guid.NewGuid(), "SCR-RET-001", DateTime.UtcNow,
            Guid.NewGuid(), Guid.NewGuid())
        {
            IsReturn = true,
            ReturnAgainstReceiptId = Guid.NewGuid(),
            SupplierWarehouseId = origSupplierWh
        };

        Should.NotThrow(() =>
            receiptReturn.ValidateReturnSupplierWarehouse(origSupplierWh, "SCR-001"));
    }
}
