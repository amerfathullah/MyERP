using System;
using MyERP.Inventory.Entities;
using MyERP.Purchasing;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Shouldly;
using Xunit;

namespace MyERP.Inventory;

/// <summary>
/// Tests for auto-reorder MR type selection from item configuration,
/// shipping rule application, and document numbering.
/// </summary>
public class AutoReorderAndShippingTests
{
    // --- Auto-Reorder MR Type from Item ---

    [Fact]
    public void Item_DefaultMaterialRequestType_DefaultsPurchase()
    {
        var item = CreateItem();
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.Purchase);
    }

    [Fact]
    public void Item_DefaultMaterialRequestType_CanBeSetToTransfer()
    {
        var item = CreateItem();
        item.DefaultMaterialRequestType = MaterialRequestType.MaterialTransfer;
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.MaterialTransfer);
    }

    [Fact]
    public void Item_DefaultMaterialRequestType_CanBeSetToManufacture()
    {
        var item = CreateItem();
        item.DefaultMaterialRequestType = MaterialRequestType.Manufacture;
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.Manufacture);
    }

    [Fact]
    public void Item_ReorderWithManufactureType_Concept()
    {
        // Item configured for in-house manufacturing when stock is low
        var item = CreateItem();
        item.ReorderLevel = 50;
        item.ReorderQty = 200;
        item.DefaultMaterialRequestType = MaterialRequestType.Manufacture;

        // When auto-reorder fires, it should create a Manufacture MR
        // which can then be converted to a Work Order
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.Manufacture);
        item.ReorderLevel.ShouldBe(50);
    }

    [Fact]
    public void Item_ReorderWithTransferType_Concept()
    {
        // Item configured for inter-warehouse transfer when stock is low
        var item = CreateItem();
        item.ReorderLevel = 20;
        item.ReorderQty = 100;
        item.DefaultMaterialRequestType = MaterialRequestType.MaterialTransfer;

        // When auto-reorder fires, it should create a Transfer MR
        // which moves stock from a source warehouse
        item.DefaultMaterialRequestType.ShouldBe(MaterialRequestType.MaterialTransfer);
    }

    // --- Shipping Rule ---

    [Fact]
    public void ShippingRule_FixedMode_ReturnsFixedAmount()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Free Shipping > 100",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.FixedAmount = 25m;

        rule.Calculate(500m).ShouldBe(25m);
        rule.Calculate(50m).ShouldBe(25m);
    }

    [Fact]
    public void ShippingRule_TieredMode_MatchesCorrectTier()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Tiered Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal, Guid.NewGuid());

        rule.AddCondition(0, 100, 15m);     // RM 0-100 → RM 15
        rule.AddCondition(100.01m, 500, 10m); // RM 100-500 → RM 10
        rule.AddCondition(500.01m, 0, 0m);    // RM 500+ → free (catch-all)

        rule.Calculate(50m).ShouldBe(15m);
        rule.Calculate(250m).ShouldBe(10m);
        rule.Calculate(1000m).ShouldBe(0m);
    }

    [Fact]
    public void ShippingRule_CountryFilter_Global()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Global Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());

        // No countries added → applies globally
        rule.AppliesToCountry("MY").ShouldBeTrue();
        rule.AppliesToCountry("SG").ShouldBeTrue();
        rule.AppliesToCountry(null).ShouldBeTrue();
    }

    [Fact]
    public void ShippingRule_CountryFilter_Restricted()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "MY Only Shipping",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.AddCountry("MY");

        rule.AppliesToCountry("MY").ShouldBeTrue();
        rule.AppliesToCountry("my").ShouldBeTrue(); // case insensitive
        rule.AppliesToCountry("SG").ShouldBeFalse();
    }

    [Fact]
    public void ShippingRule_BuyingType_CanBeCreated()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Supplier Freight",
            ShippingRuleType.Buying, ShippingCalculationMode.Fixed, Guid.NewGuid());

        rule.RuleType.ShouldBe(ShippingRuleType.Buying);
    }

    [Fact]
    public void ShippingRule_Validate_NoConditions_ThrowsForTiered()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Empty Tiered",
            ShippingRuleType.Selling, ShippingCalculationMode.BasedOnNetTotal, Guid.NewGuid());

        Should.Throw<Volo.Abp.BusinessException>(() => rule.Validate())
            .Code.ShouldBe("MyERP:03004");
    }

    [Fact]
    public void ShippingRule_Validate_FixedMode_NoCrash()
    {
        var rule = new ShippingRule(Guid.NewGuid(), "Fixed",
            ShippingRuleType.Selling, ShippingCalculationMode.Fixed, Guid.NewGuid());
        rule.FixedAmount = 10m;

        // Should not throw — fixed mode doesn't need conditions
        rule.Validate();
    }

    private static Item CreateItem()
    {
        return new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-001", "Test Item",
            ItemType.Goods);
    }
}
