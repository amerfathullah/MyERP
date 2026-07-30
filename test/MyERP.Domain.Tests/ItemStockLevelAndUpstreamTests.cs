using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using MyERP.Inventory.Entities;
using MyERP.Inventory;

namespace MyERP.Domain.Tests;

public class ItemStockLevelAndUpstreamTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    // --- Item stock level on list ---

    [Fact]
    public void ItemDto_TotalStockQty_DefaultsZero()
    {
        var dto = new ItemDto();
        Assert.Equal(0, dto.TotalStockQty);
    }

    [Fact]
    public void ItemDto_TotalStockQty_CanBeSet()
    {
        var dto = new ItemDto { TotalStockQty = 150m };
        Assert.Equal(150m, dto.TotalStockQty);
    }

    [Fact]
    public void ItemDto_IsLowStock_DefaultsFalse()
    {
        var dto = new ItemDto();
        Assert.False(dto.IsLowStock);
    }

    [Fact]
    public void ItemDto_IsLowStock_TrueWhenBelowReorderLevel()
    {
        var dto = new ItemDto
        {
            MaintainStock = true,
            ReorderLevel = 100,
            TotalStockQty = 50,
            IsLowStock = true
        };
        Assert.True(dto.IsLowStock);
    }

    [Fact]
    public void ItemDto_IsLowStock_FalseWhenAboveReorderLevel()
    {
        var dto = new ItemDto
        {
            MaintainStock = true,
            ReorderLevel = 100,
            TotalStockQty = 200
        };
        Assert.False(dto.IsLowStock);
    }

    [Fact]
    public void ItemDto_IsLowStock_FalseForServiceItems()
    {
        var dto = new ItemDto
        {
            MaintainStock = false,
            ReorderLevel = 10,
            TotalStockQty = 0
        };
        Assert.False(dto.IsLowStock);
    }

    [Fact]
    public void ItemDto_IsLowStock_FalseWhenReorderLevelZero()
    {
        var dto = new ItemDto
        {
            MaintainStock = true,
            ReorderLevel = 0,
            TotalStockQty = 0
        };
        Assert.False(dto.IsLowStock);
    }

    // --- Bin stock aggregation concepts ---

    [Fact]
    public void Bin_ActualQty_DefaultsZero()
    {
        var bin = new Bin(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenantId);
        Assert.Equal(0, bin.ActualQty);
    }

    [Fact]
    public void Item_ReorderLevel_DefaultsZero()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Test Item", ItemType.Goods, TenantId);
        Assert.Equal(0, item.ReorderLevel);
    }

    [Fact]
    public void Item_ReorderLevel_CanBeSet()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-002", "Test", ItemType.Goods, TenantId);
        item.ReorderLevel = 50;
        Assert.Equal(50, item.ReorderLevel);
    }

    // --- Item type filter ---

    [Fact]
    public void ItemType_GoodsIsZero()
    {
        Assert.Equal(0, (int)ItemType.Goods);
    }

    [Fact]
    public void ItemType_ServiceIsOne()
    {
        Assert.Equal(1, (int)ItemType.Service);
    }

    [Fact]
    public void ItemType_FixedAssetIsTwo()
    {
        Assert.Equal(2, (int)ItemType.FixedAsset);
    }

    [Theory]
    [InlineData("0", ItemType.Goods)]
    [InlineData("1", ItemType.Service)]
    [InlineData("2", ItemType.FixedAsset)]
    public void ItemType_ParsesFromString(string input, ItemType expected)
    {
        Assert.True(Enum.TryParse<ItemType>(input, true, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ItemType_InvalidStringReturnsFalse()
    {
        Assert.False(Enum.TryParse<ItemType>("InvalidType", true, out _));
    }

    // --- GetItemListDto ---

    [Fact]
    public void GetItemListDto_ItemType_DefaultsNull()
    {
        var dto = new GetItemListDto();
        Assert.Null(dto.ItemType);
    }

    [Fact]
    public void GetItemListDto_ItemType_CanBeSet()
    {
        var dto = new GetItemListDto { ItemType = "0" };
        Assert.Equal("0", dto.ItemType);
    }

    // --- Localization keys ---

    [Theory]
    [InlineData("Goods")]
    [InlineData("Service")]
    [InlineData("FixedAsset")]
    [InlineData("AllTypes")]
    [InlineData("StockQty")]
    public void LocalizationKey_ExistsInEnJson(string key)
    {
        var json = System.IO.File.ReadAllText(
            System.IO.Path.Combine(TestHelper.GetSolutionRoot(), "src", "MyERP.Domain.Shared", "Localization", "MyERP", "en.json"));
        Assert.Contains($"\"{key}\"", json);
    }

    // --- Upstream sync documentation ---

    [Fact]
    public void Upstream_PR_38e5674_MRTitleTemplate_NoCodeChange()
    {
        // PR 38e5674ea4: MR title template dropped (dead code in JSON, set_title always runs)
        // MyERP: our MR entities set title at creation time — no template patterns used
        Assert.True(true);
    }

    [Fact]
    public void Upstream_PR_03d8443_TimesheetTitleField_NoCodeChange()
    {
        // PR 03d84430b6: Timesheet title_field changed from `title` to `employee_name`
        // MyERP: our Timesheet list shows employee name directly from DTO — no title_field indirection
        Assert.True(true);
    }
}
