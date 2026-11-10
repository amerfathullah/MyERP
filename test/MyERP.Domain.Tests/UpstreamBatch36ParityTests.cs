using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.DomainServices;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

public class UpstreamBatch36ParityTests
{
    // === 1) PR #65117 / commit 6ad4805b44: Fold Digits of Any Script in Item Search Index ===

    [Theory]
    [InlineData("ABC١٢٣", "ABC123")] // Arabic-Indic digits
    [InlineData("ABC۱۲۳", "ABC123")] // Extended Arabic-Indic digits (Persian/Urdu)
    [InlineData("ABC१२३", "ABC123")] // Devanagari digits
    [InlineData("ABC❶❷❸", "ABC123")] // Encircled digits
    public void ItemSearchNormalization_FoldText_FoldsDigitsOfAnyScript(string input, string expected)
    {
        var result = ItemSearchNormalizationService.FoldText(input);
        result.ShouldBe(expected);
    }

    // === 2) PR #65115 / commit 51954d883a: Pick What is Left on Each Bundle Component ===

    [Fact]
    public void ProductBundle_GetPackedQtyToPick_UnevenComponentPicking()
    {
        // Bundle parent order qty = 3.
        // Component A: packed 3, already picked 3 -> 0 remaining to pick
        var compARemaining = ProductBundleDecompositionService.GetPackedQtyToPick(
            packedComponentQty: 3m,
            componentPickedQty: 3m,
            parentOrderQty: 3m,
            parentDeliveredQty: 0m,
            precision: 4);
        compARemaining.ShouldBe(0m);

        // Component B: packed 6, already picked 3 -> 3 remaining to pick
        var compBRemaining = ProductBundleDecompositionService.GetPackedQtyToPick(
            packedComponentQty: 6m,
            componentPickedQty: 3m,
            parentOrderQty: 3m,
            parentDeliveredQty: 0m,
            precision: 4);
        compBRemaining.ShouldBe(3m);
    }

    // === 3) PR #65109 / commit d7feab165b: Return Raw Materials to Receipt's Supplier Warehouse ===

    [Fact]
    public void SubcontractingReceipt_ValidateReturnSupplierWarehouse_ThrowsOnMismatch()
    {
        var origSupplierWhId = Guid.NewGuid();
        var mismatchedWhId = Guid.NewGuid();
        var origReceiptId = Guid.NewGuid();

        var retReceipt = new SubcontractingReceipt(
            Guid.NewGuid(), Guid.NewGuid(), "SCR-RET-001", DateTime.UtcNow.Date, Guid.NewGuid(), Guid.NewGuid())
        {
            IsReturn = true,
            ReturnAgainstReceiptId = origReceiptId,
            SupplierWarehouseId = mismatchedWhId
        };

        var ex = Should.Throw<BusinessException>(() =>
            retReceipt.ValidateReturnSupplierWarehouse(origSupplierWhId, "SCR-001"));

        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
    }

    [Fact]
    public void SubcontractingReceipt_ValidateReturnSupplierWarehouse_SucceedsOnMatch()
    {
        var supplierWhId = Guid.NewGuid();
        var origReceiptId = Guid.NewGuid();

        var retReceipt = new SubcontractingReceipt(
            Guid.NewGuid(), Guid.NewGuid(), "SCR-RET-002", DateTime.UtcNow.Date, Guid.NewGuid(), Guid.NewGuid())
        {
            IsReturn = true,
            ReturnAgainstReceiptId = origReceiptId,
            SupplierWarehouseId = supplierWhId
        };

        // Should not throw
        retReceipt.ValidateReturnSupplierWarehouse(supplierWhId, "SCR-001");
    }
}
