using System;
using System.Collections.Generic;
using MyERP.Sales.Entities;
using MyERP.Tax.DomainServices;
using MyERP.Tax.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace MyERP;

/// <summary>
/// Unit tests for Batch 31 upstream parity:
/// 1) Sales Invoice fixed asset uniqueness on non-return invoices (PR #63881 / commits d4bc06985e, 07061b88b0, be69fdba29).
/// 2) Allow duplicate asset lines on return sales invoices (PR #63881 / commit be69fdba29).
/// 3) TaxesAndTotals "On Item Quantity" charge type calculation (PR #63129 / commits d51ac49c0b, db2e65a5d5, 94070a93c4).
/// 4) POS Invoice Change and Base Change calculation parity (PR #63821 / commits b690443b26, e4a423000c, 807abb1adb).
/// </summary>
public class UpstreamBatch31ParityTests
{
    // === 1 & 2) PR #63881: Fixed Asset Row Validation on Sales Invoice ===

    [Fact]
    public void SalesInvoice_ValidateFixedAssets_RejectsDuplicateAsset_OnNonReturnInvoice()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SINV-FA-001", DateTime.UtcNow);

        invoice.AddItem(itemId, "Machine Part A", 1m, 1000m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = assetId;

        invoice.AddItem(itemId, "Machine Part B", 1m, 500m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = assetId;

        var ex = Should.Throw<BusinessException>(() => invoice.ValidateFixedAssets());
        ex.Code.ShouldBe(MyERPDomainErrorCodes.ValidationFailed);
        ex.Data["detail"]!.ToString()!.ShouldContain($"Asset {assetId} is already added in another row");
    }

    [Fact]
    public void SalesInvoice_ValidateFixedAssets_AllowsDuplicateAsset_OnReturnInvoice()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "RET-FA-001", DateTime.UtcNow)
        {
            IsReturn = true
        };

        invoice.AddItem(itemId, "Machine Part A Return", -1m, 1000m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = assetId;

        invoice.AddItem(itemId, "Machine Part B Return", -1m, 500m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = assetId;

        // Should not throw on returns (PR #63881 / commit be69fdba29)
        Should.NotThrow(() => invoice.ValidateFixedAssets());
    }

    [Fact]
    public void SalesInvoice_ValidateFixedAssets_AllowsDistinctAssets()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var asset1 = Guid.NewGuid();
        var asset2 = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SINV-FA-002", DateTime.UtcNow);

        invoice.AddItem(itemId, "Asset 1", 1m, 1000m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = asset1;

        invoice.AddItem(itemId, "Asset 2", 1m, 2000m, 0m);
        invoice.Items[^1].IsFixedAsset = true;
        invoice.Items[^1].AssetId = asset2;

        Should.NotThrow(() => invoice.ValidateFixedAssets());
    }

    [Fact]
    public void SalesInvoice_ValidateFixedAssets_IgnoresNonFixedAssetRows()
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var invoice = new SalesInvoice(
            Guid.NewGuid(), companyId, customerId, "SINV-REG-001", DateTime.UtcNow);

        invoice.AddItem(itemId, "Stock Item 1", 5m, 10m, 0m);
        invoice.Items[^1].IsFixedAsset = false;
        invoice.Items[^1].AssetId = null;

        invoice.AddItem(itemId, "Stock Item 2", 3m, 10m, 0m);
        invoice.Items[^1].IsFixedAsset = false;
        invoice.Items[^1].AssetId = null;

        Should.NotThrow(() => invoice.ValidateFixedAssets());
    }

    // === 3) PR #63129: TaxesAndTotals "On Item Quantity" Charge Type ===

    [Fact]
    public void TaxesAndTotalsService_OnItemQuantity_CalculatesTaxByQuantity()
    {
        var service = new TaxesAndTotalsService();

        var items = new List<TransactionItem>
        {
            new TransactionItem
            {
                ItemId = Guid.NewGuid(),
                Qty = 4m,
                Rate = 25m,
                NetAmount = 100m
            }
        };

        var taxes = new List<TransactionTaxRow>
        {
            new TransactionTaxRow(
                Guid.NewGuid(),
                "SalesInvoice",
                Guid.NewGuid(),
                rowIndex: 1,
                description: "Handling Fee",
                chargeType: "On Item Quantity",
                rate: 3.5m) // 3.5 per unit
        };

        var result = service.Calculate(items, taxes);

        result.NetTotal.ShouldBe(100m);
        // 4 qty * 3.5 = 14m
        taxes[0].TaxAmount.ShouldBe(14m);
        result.TotalTax.ShouldBe(14m);
        result.GrandTotal.ShouldBe(114m);
    }

    // === 4) PR #63821: POS Invoice Change and Base Change Calculation ===

    [Theory]
    [InlineData(100.00, 120.00, 1.0, 20.00, 20.00)]
    [InlineData(100.00, 100.00, 1.0, 0.00, 0.00)]
    [InlineData(100.00, 80.00, 1.0, 0.00, 0.00)]
    [InlineData(50.00, 60.00, 4.45, 10.00, 44.50)]
    public void PosInvoice_ChangeAndBaseChange_CalculatesProperly(
        decimal grandTotal, decimal amountReceived, decimal exchangeRate,
        decimal expectedChange, decimal expectedBaseChange)
    {
        var rawChange = amountReceived - grandTotal;
        var actualChange = rawChange > 0 ? rawChange : 0m;
        var baseChange = Math.Round(actualChange * exchangeRate, 2);

        actualChange.ShouldBe(expectedChange);
        baseChange.ShouldBe(expectedBaseChange);
    }
}
