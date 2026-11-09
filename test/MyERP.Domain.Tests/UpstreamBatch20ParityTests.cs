using System;
using MyERP.Inventory.DomainServices;
using MyERP.Purchasing.DomainServices;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests;

/// <summary>
/// Unit tests for upstream parity batch 20:
/// 1) Subcontracting Receipt Supplier Warehouse Mapping for Supplied Items (PR #64867 / commits 1bcdf9c96f &amp; 7b124a874c).
/// 2) Stock Entry Return Row Signed Quantity Invariant (PR #64866 / commits f841624ed7 &amp; bb5656ba05).
/// </summary>
public class UpstreamBatch20ParityTests
{
    // === 1) Subcontracting Supplied Item Bundle Parameters ===

    [Fact]
    public void SubcontractingManager_ResolveSuppliedItemBundleParameters_RegularReceipt_ReturnsOutwardTransaction()
    {
        var rawMaterialId = Guid.NewGuid();
        var supplierWarehouseId = Guid.NewGuid();

        var (itemId, warehouseId, txType) = SubcontractingManager.ResolveSuppliedItemBundleParameters(
            rawMaterialId, supplierWarehouseId, isReturn: false);

        itemId.ShouldBe(rawMaterialId);
        warehouseId.ShouldBe(supplierWarehouseId);
        txType.ShouldBe("Outward");
    }

    [Fact]
    public void SubcontractingManager_ResolveSuppliedItemBundleParameters_ReturnReceipt_ReturnsInwardTransaction()
    {
        var rawMaterialId = Guid.NewGuid();
        var supplierWarehouseId = Guid.NewGuid();

        var (itemId, warehouseId, txType) = SubcontractingManager.ResolveSuppliedItemBundleParameters(
            rawMaterialId, supplierWarehouseId, isReturn: true);

        itemId.ShouldBe(rawMaterialId);
        warehouseId.ShouldBe(supplierWarehouseId);
        txType.ShouldBe("Inward");
    }

    // === 2) Stock Entry Return Row Signed Quantity Invariant ===

    [Theory]
    [InlineData("Stock Entry", true, 10, 10)]
    [InlineData("StockEntry", true, 25.5, 25.5)]
    [InlineData("STOCK ENTRY", true, 5, 5)]
    [InlineData("Stock Entry", false, 10, 10)]
    public void StockEntryManager_GetSignedReturnRowQty_StockEntry_AlwaysPreservesPositiveQuantity(
        string docType, bool isReturn, decimal qty, decimal expected)
    {
        var result = StockEntryManager.GetSignedReturnRowQty(docType, isReturn, qty);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Purchase Receipt", true, 10, -10)]
    [InlineData("Sales Invoice", true, 25.5, -25.5)]
    [InlineData("Delivery Note", true, 5, -5)]
    [InlineData("Purchase Invoice", true, 12, -12)]
    [InlineData("Subcontracting Receipt", true, 8, -8)]
    public void StockEntryManager_GetSignedReturnRowQty_OtherDocuments_ReturnsNegativeQuantityOnReturn(
        string docType, bool isReturn, decimal qty, decimal expected)
    {
        var result = StockEntryManager.GetSignedReturnRowQty(docType, isReturn, qty);
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Purchase Receipt", false, 10, 10)]
    [InlineData("Sales Invoice", false, 25.5, 25.5)]
    [InlineData("Delivery Note", false, 0, 0)]
    public void StockEntryManager_GetSignedReturnRowQty_NonReturns_LeavesQuantityUnchanged(
        string docType, bool isReturn, decimal qty, decimal expected)
    {
        var result = StockEntryManager.GetSignedReturnRowQty(docType, isReturn, qty);
        result.ShouldBe(expected);
    }
}
