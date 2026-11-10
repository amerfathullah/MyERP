using System;
using System.Collections.Generic;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP;

public class UpstreamBatch35ParityTests
{
    // === 1) PR #65051 / commit fe42da0c74: Pick List Bundle Sizing with Repeated Components ===

    [Fact]
    public void ComputePickedQtyForBundle_WithRepeatedComponent_AccumulatesRequiredQtyAndCalculatesBundleCount()
    {
        var soItemId = Guid.NewGuid();
        var componentAId = Guid.NewGuid();
        var componentBId = Guid.NewGuid();

        // Bundle requires 3 of A (e.g. 1 in Box + 2 in Nos) and 1 of B
        var requiredQuantities = new Dictionary<Guid, decimal>
        {
            [componentAId] = 3m,
            [componentBId] = 1m
        };

        // Pick List has picked items: 6 of A and 2 of B
        var whId = Guid.NewGuid();
        var pickItems = new List<PickListItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), componentAId, whId, 4m, 4m, "A", null) { SourceDocumentItemId = soItemId },
            new(Guid.NewGuid(), Guid.NewGuid(), componentAId, whId, 2m, 2m, "A", null) { SourceDocumentItemId = soItemId },
            new(Guid.NewGuid(), Guid.NewGuid(), componentBId, whId, 2m, 2m, "B", null) { SourceDocumentItemId = soItemId }
        };

        var bundleCount = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, requiredQuantities);

        // 6 of A / 3 = 2 bundles; 2 of B / 1 = 2 bundles -> min is 2
        bundleCount.ShouldBe(2);
    }

    [Fact]
    public void ComputePickedQtyForBundle_PartialPick_LimitsToBottleneckComponent()
    {
        var soItemId = Guid.NewGuid();
        var comp1 = Guid.NewGuid();
        var comp2 = Guid.NewGuid();

        var requiredQuantities = new Dictionary<Guid, decimal>
        {
            [comp1] = 2m,
            [comp2] = 5m
        };

        // Pick List has 10 of comp1 (5 bundles worth) but only 5 of comp2 (1 bundle worth)
        var whId = Guid.NewGuid();
        var pickItems = new List<PickListItem>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), comp1, whId, 10m, 10m, "Comp1", null) { SourceDocumentItemId = soItemId },
            new(Guid.NewGuid(), Guid.NewGuid(), comp2, whId, 5m, 5m, "Comp2", null) { SourceDocumentItemId = soItemId }
        };

        var bundleCount = PickListManager.ComputePickedQtyForBundle(pickItems, soItemId, requiredQuantities);

        bundleCount.ShouldBe(1);
    }

    // === 2) PR #65052 / PR #65053: Product Bundle Version & Item Mapping ===

    [Fact]
    public void SalesInvoiceItem_ProductBundleId_SetsAndGrossProfitCalculates()
    {
        var invoiceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var bundleVersionId = Guid.NewGuid();

        var item = new SalesInvoiceItem(Guid.NewGuid(), invoiceId, itemId, "Bundle Parent Item", 2m, 100m, 0m, "Nos")
        {
            ProductBundleId = bundleVersionId,
            ValuationRate = 60m // 2 components @ 30m each = 60m total cost per bundle unit
        };

        item.ProductBundleId.ShouldBe(bundleVersionId);
        item.ValuationRate.ShouldBe(60m);
        // Gross profit = (StockUomRate - ValuationRate) * StockQty = (100 - 60) * 2 = 80
        item.GrossProfit.ShouldBe(80m);
    }

    [Fact]
    public void SalesInvoiceItemDto_And_CreateSalesInvoiceItemDto_HaveProductBundleId()
    {
        var bundleVersionId = Guid.NewGuid();
        var createDto = new CreateSalesInvoiceItemDto
        {
            ItemId = Guid.NewGuid(),
            Description = "Test Bundle",
            Quantity = 1m,
            UnitPrice = 50m,
            ProductBundleId = bundleVersionId
        };

        var itemDto = new SalesInvoiceItemDto
        {
            Id = Guid.NewGuid(),
            ItemId = createDto.ItemId,
            Description = createDto.Description,
            Quantity = 1m,
            UnitPrice = 50m,
            ProductBundleId = createDto.ProductBundleId
        };

        createDto.ProductBundleId.ShouldBe(bundleVersionId);
        itemDto.ProductBundleId.ShouldBe(bundleVersionId);
    }
}
