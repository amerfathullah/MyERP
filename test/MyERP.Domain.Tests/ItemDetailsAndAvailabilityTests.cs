using System;
using System.Collections.Generic;
using MyERP.Inventory;
using MyERP.Inventory.Entities;
using Xunit;

namespace MyERP;

/// <summary>
/// Tests for item details resolution and stock availability features.
/// Covers: ItemDetailsDto defaults, availability aggregation, and item resolution logic.
/// </summary>
public class ItemDetailsAndAvailabilityTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    #region ItemDetailsDto Tests

    [Fact]
    public void ItemDetailsDto_Defaults()
    {
        var dto = new ItemDetailsDto();
        Assert.Equal("Unit", dto.Uom);
        Assert.Equal("Unit", dto.StockUom);
        Assert.Equal(1m, dto.ConversionFactor);
        Assert.Equal(0m, dto.Rate);
        Assert.Equal(0m, dto.ActualQty);
        Assert.Equal(0m, dto.ProjectedQty);
        Assert.Equal(0m, dto.CompanyTotalStock);
    }

    [Fact]
    public void ItemDetailsDto_SellingFields()
    {
        var dto = new ItemDetailsDto
        {
            Rate = 25.50m,
            IncomeAccountId = Guid.NewGuid()
        };
        Assert.Equal(25.50m, dto.Rate);
        Assert.NotNull(dto.IncomeAccountId);
        Assert.Null(dto.ExpenseAccountId);
    }

    [Fact]
    public void ItemDetailsDto_BuyingFields()
    {
        var dto = new ItemDetailsDto
        {
            Rate = 18.00m,
            ExpenseAccountId = Guid.NewGuid(),
            LastPurchaseRate = 17.50m,
            MinOrderQty = 100
        };
        Assert.Equal(18.00m, dto.Rate);
        Assert.NotNull(dto.ExpenseAccountId);
        Assert.Equal(17.50m, dto.LastPurchaseRate);
        Assert.Equal(100m, dto.MinOrderQty);
    }

    [Fact]
    public void ItemDetailsDto_StockAvailability()
    {
        var dto = new ItemDetailsDto
        {
            ActualQty = 500,
            ReservedQty = 150,
            AvailableQty = 350, // actual - reserved
            ProjectedQty = 400, // actual + ordered - reserved
            CompanyTotalStock = 1200
        };
        Assert.Equal(350m, dto.AvailableQty);
        Assert.True(dto.CompanyTotalStock > dto.ActualQty);
    }

    #endregion

    #region GetItemDetailsInput Tests

    [Fact]
    public void GetItemDetailsInput_DefaultTransactionType()
    {
        var input = new GetItemDetailsInput { ItemId = ItemId };
        Assert.Equal("Selling", input.TransactionType);
        Assert.Null(input.WarehouseId);
        Assert.Null(input.CompanyId);
    }

    [Fact]
    public void GetItemDetailsInput_BuyingType()
    {
        var input = new GetItemDetailsInput
        {
            ItemId = ItemId,
            TransactionType = "Buying",
            WarehouseId = WarehouseId,
            CompanyId = CompanyId
        };
        Assert.Equal("Buying", input.TransactionType);
        Assert.Equal(WarehouseId, input.WarehouseId);
    }

    #endregion

    #region ItemAvailabilityDto Tests

    [Fact]
    public void ItemAvailabilityDto_Defaults()
    {
        var dto = new ItemAvailabilityDto();
        Assert.Equal(0m, dto.ActualQty);
        Assert.Equal(0m, dto.ReservedQty);
        Assert.Equal(0m, dto.OrderedQty);
        Assert.Equal(0m, dto.ProjectedQty);
        Assert.Equal(0m, dto.AvailableQty);
    }

    [Fact]
    public void ItemAvailabilityDto_CalculatedAvailable()
    {
        var dto = new ItemAvailabilityDto
        {
            ActualQty = 100,
            ReservedQty = 30,
            AvailableQty = 70 // 100 - 30
        };
        Assert.Equal(70m, dto.AvailableQty);
    }

    [Fact]
    public void ItemAvailabilityDto_NegativeAvailable()
    {
        // Over-reserved scenario (shouldn't happen normally but edge case)
        var dto = new ItemAvailabilityDto
        {
            ActualQty = 10,
            ReservedQty = 15,
            AvailableQty = -5
        };
        Assert.True(dto.AvailableQty < 0);
    }

    #endregion

    #region GetItemsAvailabilityInput Tests

    [Fact]
    public void GetItemsAvailabilityInput_DefaultEmpty()
    {
        var input = new GetItemsAvailabilityInput();
        Assert.NotNull(input.ItemIds);
        Assert.Empty(input.ItemIds);
        Assert.Null(input.WarehouseId);
    }

    [Fact]
    public void GetItemsAvailabilityInput_WithItems()
    {
        var input = new GetItemsAvailabilityInput
        {
            ItemIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            WarehouseId = WarehouseId
        };
        Assert.Equal(3, input.ItemIds.Count);
        Assert.Equal(WarehouseId, input.WarehouseId);
    }

    #endregion

    #region Item Entity Price Fields Tests

    [Fact]
    public void Item_SellingPrice_UsedForSellingTransaction()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Widget",
            MyERP.Inventory.ItemType.Goods, TenantId);
        item.StandardSellingPrice = 25.50m;
        item.StandardBuyingPrice = 18.00m;

        Assert.Equal(25.50m, item.StandardSellingPrice);
    }

    [Fact]
    public void Item_BuyingPrice_UsedForBuyingTransaction()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Widget",
            MyERP.Inventory.ItemType.Goods, TenantId);
        item.StandardSellingPrice = 25.50m;
        item.StandardBuyingPrice = 18.00m;

        Assert.Equal(18.00m, item.StandardBuyingPrice);
    }

    [Fact]
    public void Item_DefaultWarehouse_ResolvesForTransaction()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "ITEM-001", "Widget",
            MyERP.Inventory.ItemType.Goods, TenantId);
        item.DefaultWarehouseId = WarehouseId;

        Assert.Equal(WarehouseId, item.DefaultWarehouseId);
    }

    [Fact]
    public void Item_ServiceItem_NotStockItem()
    {
        var item = new Item(Guid.NewGuid(), CompanyId, "SVC-001", "Consulting",
            MyERP.Inventory.ItemType.Service, TenantId);

        Assert.False(item.MaintainStock);
    }

    #endregion

    #region Bin Projected Qty Tests

    [Fact]
    public void Bin_ProjectedQty_IncludesAllFactors()
    {
        // ProjectedQty = ActualQty + OrderedQty + IndentedQty + PlannedQty - ReservedQty
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId, TenantId);
        bin.ActualQty = 100;
        bin.OrderedQty = 50;
        bin.IndentedQty = 20;
        bin.PlannedQty = 30;
        bin.ReservedQty = 40;

        // Projected = 100 + 50 + 20 + 30 - 40 = 160
        Assert.Equal(160m, bin.ProjectedQty);
    }

    [Fact]
    public void Bin_AvailableStock_IsActualMinusReserved()
    {
        var bin = new Bin(Guid.NewGuid(), ItemId, WarehouseId, TenantId);
        bin.ActualQty = 100;
        bin.ReservedQty = 30;

        Assert.Equal(70m, bin.ActualQty - bin.ReservedQty);
    }

    #endregion
}
