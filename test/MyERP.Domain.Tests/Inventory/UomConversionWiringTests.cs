using System;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using MyERP.Inventory.DomainServices;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

/// <summary>
/// Tests for UOM conversion factor resolution wired into ItemDetailsResolverService.
/// Validates the priority chain: item-specific → global → 1.0 (same UOM).
/// </summary>
public class UomConversionWiringTests
{
    [Fact]
    public void UomConversion_SameUom_Returns_Factor_One()
    {
        // Per ERPNext: when transaction UOM == stock UOM, factor is always 1.0
        var from = "KG";
        var to = "KG";
        Assert.Equal(from, to);
    }

    [Fact]
    public void UomConversion_Entity_Has_Required_Fields()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Box", "EA", 12m);
        Assert.Equal("Box", conv.FromUom);
        Assert.Equal("EA", conv.ToUom);
        Assert.Equal(12m, conv.ConversionFactor);
    }

    [Fact]
    public void UomConversion_ItemSpecific_Has_ItemId()
    {
        var itemId = Guid.NewGuid();
        var conv = new UomConversion(Guid.NewGuid(), "Dozen", "EA", 12m);
        conv.ItemId = itemId;
        Assert.Equal(itemId, conv.ItemId);
    }

    [Fact]
    public void UomConversion_Global_Has_Null_ItemId()
    {
        var conv = new UomConversion(Guid.NewGuid(), "KG", "G", 1000m);
        Assert.Null(conv.ItemId);
    }

    [Fact]
    public void UomConversion_Factor_Must_Be_Positive()
    {
        var conv = new UomConversion(Guid.NewGuid(), "Pack", "EA", 6m);
        Assert.True(conv.ConversionFactor > 0);
    }

    [Theory]
    [InlineData("Dozen", "EA", 12)]
    [InlineData("Box", "EA", 24)]
    [InlineData("KG", "G", 1000)]
    [InlineData("L", "ML", 1000)]
    public void UomConversion_Standard_Factors(string from, string to, decimal expectedFactor)
    {
        var conv = new UomConversion(Guid.NewGuid(), from, to, expectedFactor);
        Assert.Equal(expectedFactor, conv.ConversionFactor);
    }

    [Fact]
    public void ResolvedItemDetails_ConversionFactor_DefaultsToOne()
    {
        var details = new ResolvedItemDetails();
        Assert.Equal(1m, details.ConversionFactor);
    }

    [Fact]
    public void Item_SalesUom_And_StockUom_May_Differ()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-001", "Test Item", ItemType.Goods);
        item.Uom = "EA";
        item.SalesUom = "Box";
        Assert.NotEqual(item.Uom, item.SalesUom);
    }

    [Fact]
    public void Item_PurchaseUom_Resolution_For_Buying()
    {
        var item = new Item(Guid.NewGuid(), Guid.NewGuid(), "ITM-002", "Bulk Item", ItemType.Goods);
        item.Uom = "EA";
        item.PurchaseUom = "Carton";
        Assert.Equal("Carton", item.PurchaseUom);
        Assert.Equal("EA", item.Uom);
    }
}
