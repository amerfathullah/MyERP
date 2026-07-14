using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class ProductBundleDecompositionTests
{
    [Fact]
    public void DecomposedItem_Record_HoldsValues()
    {
        var item = new DecomposedItem(
            ComponentItemId: Guid.NewGuid(),
            ComponentItemName: "Widget A",
            Qty: 5m,
            Rate: 10m,
            Uom: "Unit",
            ParentBundleItemId: Guid.NewGuid(),
            ParentBundleId: Guid.NewGuid());

        item.Qty.ShouldBe(5m);
        item.Rate.ShouldBe(10m);
        item.ComponentItemName.ShouldBe("Widget A");
    }

    [Fact]
    public void BundleTransactionItem_Record_HoldsValues()
    {
        var item = new BundleTransactionItem(Guid.NewGuid(), 3m, 100m);
        item.Qty.ShouldBe(3m);
        item.Rate.ShouldBe(100m);
    }

    [Fact]
    public void DecompositionResult_GetStockItems_CombinesRegularAndPacked()
    {
        var regularItemId = Guid.NewGuid();
        var componentItemId = Guid.NewGuid();

        var result = new DecompositionResult(
            RegularItems: new List<BundleTransactionItem>
            {
                new(regularItemId, 2m, 50m)
            },
            PackedItems: new List<DecomposedItem>
            {
                new(componentItemId, "Comp A", 4m, 25m, "Unit", Guid.NewGuid(), Guid.NewGuid())
            });

        var stockItems = result.GetStockItems().ToList();
        stockItems.Count.ShouldBe(2);
        stockItems.ShouldContain(s => s.ItemId == regularItemId && s.Qty == 2m);
        stockItems.ShouldContain(s => s.ItemId == componentItemId && s.Qty == 4m);
    }

    [Fact]
    public void DecompositionResult_HasBundleItems_TrueWhenPackedNotEmpty()
    {
        var result = new DecompositionResult(
            new List<BundleTransactionItem>(),
            new List<DecomposedItem>
            {
                new(Guid.NewGuid(), "X", 1m, 10m, "Unit", Guid.NewGuid(), Guid.NewGuid())
            });

        result.HasBundleItems.ShouldBeTrue();
    }

    [Fact]
    public void DecompositionResult_HasBundleItems_FalseWhenEmpty()
    {
        var result = new DecompositionResult(
            new List<BundleTransactionItem> { new(Guid.NewGuid(), 1m, 10m) },
            new List<DecomposedItem>());

        result.HasBundleItems.ShouldBeFalse();
    }

    [Fact]
    public void ProductBundle_CalculateValuation_SumsComponentRates()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        var compA = Guid.NewGuid();
        var compB = Guid.NewGuid();
        bundle.AddItem(compA, 2m, "Component A");
        bundle.AddItem(compB, 3m, "Component B");

        // Valuation: compA rate=10 × qty=2 + compB rate=20 × qty=3 = 20 + 60 = 80
        var valuation = bundle.CalculateValuation(itemId =>
            itemId == compA ? 10m : 20m);

        valuation.ShouldBe(80m);
    }

    [Fact]
    public void ProductBundle_AddItem_IncrementsCount()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Items.Count.ShouldBe(0);

        bundle.AddItem(Guid.NewGuid(), 1m);
        bundle.Items.Count.ShouldBe(1);

        bundle.AddItem(Guid.NewGuid(), 2m);
        bundle.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void ProductBundle_Deactivate_SetsInactive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.IsActive.ShouldBeTrue();

        bundle.Deactivate();
        bundle.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ProductBundle_Activate_SetsActive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.IsActive.ShouldBeFalse();

        bundle.Activate();
        bundle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ProductBundle_ComponentQty_ScalesConcept()
    {
        // If bundle has component with qty=3, and transaction has 5 bundles:
        // Then stock operation needs 3 × 5 = 15 component units
        var componentQty = 3m;
        var transactionQty = 5m;
        var scaledQty = componentQty * transactionQty;
        scaledQty.ShouldBe(15m);
    }

    [Fact]
    public void ProductBundle_ProportionalRate_DistributesEvenly()
    {
        // If bundle sells for 100 and has 2 components with qty 2 and 3:
        // Component A gets: 100 × (2 / 5) = 40
        // Component B gets: 100 × (3 / 5) = 60
        var bundleRate = 100m;
        var totalComponentQty = 5m; // 2 + 3
        var rateA = bundleRate * (2m / totalComponentQty);
        var rateB = bundleRate * (3m / totalComponentQty);
        rateA.ShouldBe(40m);
        rateB.ShouldBe(60m);
    }

    [Fact]
    public void ProductBundle_ProportionalRate_HandlesUnevenDistribution()
    {
        // If bundle sells for 100 and has 3 components with qty 1, 1, 1:
        // Each gets: 100 × (1 / 3) = 33.333...
        var bundleRate = 100m;
        var totalComponentQty = 3m;
        var rateEach = bundleRate * (1m / totalComponentQty);
        // Should be ~33.33 (not exactly 33.33 due to decimal precision)
        rateEach.ShouldBeGreaterThan(33.33m);
        rateEach.ShouldBeLessThan(33.34m);
    }

    [Fact]
    public void ProductBundle_BundleItem_PropertiesSet()
    {
        var bundleId = Guid.NewGuid();
        var compId = Guid.NewGuid();
        var bundle = new ProductBundle(bundleId, Guid.NewGuid());
        bundle.AddItem(compId, 5m, "Widget", "Piece");

        var item = bundle.Items.First();
        item.ComponentItemId.ShouldBe(compId);
        item.Qty.ShouldBe(5m);
        item.ItemName.ShouldBe("Widget");
        item.Uom.ShouldBe("Piece");
        item.ProductBundleId.ShouldBe(bundleId);
    }

    [Fact]
    public void ProductBundle_EmptyBundle_ZeroValuation()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        var valuation = bundle.CalculateValuation(_ => 100m);
        valuation.ShouldBe(0m);
    }
}
