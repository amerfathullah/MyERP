using System;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Sales;

public class ProductBundleTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.IsActive.ShouldBeTrue();
        bundle.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AddItem_AddsComponent()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.AddItem(Guid.NewGuid(), 5m, "Widget A");
        bundle.AddItem(Guid.NewGuid(), 2m, "Widget B");
        bundle.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_SetsActive()
    {
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.Deactivate();
        bundle.Activate();
        bundle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void CalculateValuation_SumsComponentRates()
    {
        var itemA = Guid.NewGuid();
        var itemB = Guid.NewGuid();
        var bundle = new ProductBundle(Guid.NewGuid(), Guid.NewGuid());
        bundle.AddItem(itemA, 3m, "A"); // 3 units
        bundle.AddItem(itemB, 2m, "B"); // 2 units

        // Component rates: A=10, B=25
        var valuation = bundle.CalculateValuation(id =>
            id == itemA ? 10m : id == itemB ? 25m : 0m);

        valuation.ShouldBe(80m); // 3×10 + 2×25
    }
}
