using System;
using System.Linq;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Inventory;

public class ItemVariantTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void ItemAttribute_TextAttribute_DefaultState()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Color");
        Assert.Equal("Color", attr.AttributeName);
        Assert.False(attr.IsNumeric);
        Assert.Null(attr.FromRange);
        Assert.Null(attr.ToRange);
        Assert.Null(attr.Increment);
        Assert.Empty(attr.Values);
    }

    [Fact]
    public void ItemAttribute_AddValue()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Size");
        attr.AddValue("Small", "S");
        attr.AddValue("Medium", "M");
        attr.AddValue("Large", "L");

        Assert.Equal(3, attr.Values.Count);
        Assert.Contains(attr.Values, v => v.AttributeValue == "Small" && v.Abbreviation == "S");
    }

    [Fact]
    public void ItemAttribute_AddValue_DuplicateThrows()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Color");
        attr.AddValue("Red", "RED");

        var ex = Assert.Throws<BusinessException>(() => attr.AddValue("Red", "R"));
        Assert.Equal("MyERP:05022", ex.Code);
    }

    [Fact]
    public void ItemAttribute_AddValue_ToNumericThrows()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        attr.SetNumericRange(1m, 10m, 0.5m);

        var ex = Assert.Throws<BusinessException>(() => attr.AddValue("Heavy", "HVY"));
        Assert.Equal("MyERP:05021", ex.Code);
    }

    [Fact]
    public void ItemAttribute_NumericRange_Valid()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        attr.SetNumericRange(1m, 10m, 0.5m);

        Assert.True(attr.IsNumeric);
        Assert.Equal(1m, attr.FromRange);
        Assert.Equal(10m, attr.ToRange);
        Assert.Equal(0.5m, attr.Increment);
    }

    [Fact]
    public void ItemAttribute_NumericRange_InvalidFromGreaterThanTo_Throws()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);

        var ex = Assert.Throws<BusinessException>(() => attr.SetNumericRange(10m, 1m, 0.5m));
        Assert.Equal("MyERP:05019", ex.Code);
    }

    [Fact]
    public void ItemAttribute_NumericRange_ZeroIncrement_Throws()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);

        var ex = Assert.Throws<BusinessException>(() => attr.SetNumericRange(1m, 10m, 0m));
        Assert.Equal("MyERP:05020", ex.Code);
    }

    [Fact]
    public void ItemAttribute_IsValidNumericValue_OnIncrement()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        attr.SetNumericRange(1m, 10m, 0.5m);

        Assert.True(attr.IsValidNumericValue(1m));    // from_range
        Assert.True(attr.IsValidNumericValue(1.5m));  // from + 1 increment
        Assert.True(attr.IsValidNumericValue(5.5m));  // from + 9 increments
        Assert.True(attr.IsValidNumericValue(10m));   // to_range
    }

    [Fact]
    public void ItemAttribute_IsValidNumericValue_OffIncrement()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        attr.SetNumericRange(1m, 10m, 0.5m);

        Assert.False(attr.IsValidNumericValue(1.3m)); // Not on 0.5 boundary
        Assert.False(attr.IsValidNumericValue(2.7m)); // Not aligned
    }

    [Fact]
    public void ItemAttribute_IsValidNumericValue_OutOfRange()
    {
        var attr = new ItemAttribute(Guid.NewGuid(), "Weight", isNumeric: true);
        attr.SetNumericRange(1m, 10m, 0.5m);

        Assert.False(attr.IsValidNumericValue(0.5m));  // Below range
        Assert.False(attr.IsValidNumericValue(10.5m)); // Above range
    }

    [Fact]
    public void Item_HasVariants_Default()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "TSHIRT", "T-Shirt",
            MyERP.Inventory.ItemType.Goods);

        Assert.False(item.HasVariants);
        Assert.Null(item.VariantOfId);
        Assert.Empty(item.VariantAttributes);
    }

    [Fact]
    public void Item_TemplateItem()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "TSHIRT", "T-Shirt",
            MyERP.Inventory.ItemType.Goods);
        item.HasVariants = true;

        Assert.True(item.HasVariants);
    }

    [Fact]
    public void Item_VariantItem()
    {
        var templateId = Guid.NewGuid();
        var item = new Item(Guid.NewGuid(), _companyId, "TSHIRT-RED-L", "T-Shirt Red L",
            MyERP.Inventory.ItemType.Goods);
        item.VariantOfId = templateId;

        Assert.Equal(templateId, item.VariantOfId);
        Assert.False(item.HasVariants); // Variants don't have sub-variants
    }

    [Fact]
    public void ItemVariantAttribute_Properties()
    {
        var itemId = Guid.NewGuid();
        var attrId = Guid.NewGuid();
        var va = new ItemVariantAttribute(Guid.NewGuid(), itemId, attrId, "Red");

        Assert.Equal(itemId, va.ItemId);
        Assert.Equal(attrId, va.ItemAttributeId);
        Assert.Equal("Red", va.AttributeValue);
    }

    [Fact]
    public void Item_VariantAttributes_CanBePopulated()
    {
        var item = new Item(Guid.NewGuid(), _companyId, "TSHIRT-RED-L", "T-Shirt Red Large",
            MyERP.Inventory.ItemType.Goods);

        var colorAttrId = Guid.NewGuid();
        var sizeAttrId = Guid.NewGuid();

        item.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), item.Id, colorAttrId, "Red"));
        item.VariantAttributes.Add(new ItemVariantAttribute(Guid.NewGuid(), item.Id, sizeAttrId, "Large"));

        Assert.Equal(2, item.VariantAttributes.Count);
    }

    [Fact]
    public void ItemAttributeValue_Properties()
    {
        var attrId = Guid.NewGuid();
        var val = new ItemAttributeValue(Guid.NewGuid(), attrId, "Blue", "BLU");

        Assert.Equal(attrId, val.ItemAttributeId);
        Assert.Equal("Blue", val.AttributeValue);
        Assert.Equal("BLU", val.Abbreviation);
    }

    [Fact]
    public void ItemAttribute_NameRequired()
    {
        Assert.Throws<ArgumentException>(() =>
            new ItemAttribute(Guid.NewGuid(), ""));
    }
}
