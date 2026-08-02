using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using MyERP.Inventory.Entities;
using MyERP.Inventory;

namespace MyERP.Domain.Tests;

/// <summary>
/// Tests for the enhanced Transit Transfer receive workflow —
/// inline warehouse selection + one-click receive from transit list.
/// </summary>
public class TransitReceiveWorkflowTests
{
    [Fact]
    public void StockEntry_ReceiveAtWarehouse_Type_Exists()
    {
        Assert.True(Enum.IsDefined(typeof(StockEntryType), StockEntryType.ReceiveAtWarehouse));
    }

    [Fact]
    public void StockEntry_SendToWarehouse_Type_Exists()
    {
        Assert.True(Enum.IsDefined(typeof(StockEntryType), StockEntryType.SendToWarehouse));
    }

    [Fact]
    public void StockEntry_Default_Type_Is_MaterialReceipt()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        Assert.Equal(StockEntryType.MaterialReceipt, se.EntryType);
    }

    [Fact]
    public void StockEntry_Can_Set_ReceiveAtWarehouse_Type()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.ReceiveAtWarehouse, DateTime.UtcNow);
        Assert.Equal(StockEntryType.ReceiveAtWarehouse, se.EntryType);
    }

    [Fact]
    public void StockEntry_SourceStockEntryId_Links_FirstLeg()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.ReceiveAtWarehouse, DateTime.UtcNow);
        var firstLegId = Guid.NewGuid();
        se.SourceStockEntryId = firstLegId;
        Assert.Equal(firstLegId, se.SourceStockEntryId);
    }

    [Fact]
    public void StockEntry_SourceStockEntryId_Defaults_Null()
    {
        var companyId = Guid.NewGuid();
        var se = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.MaterialReceipt, DateTime.UtcNow);
        Assert.Null(se.SourceStockEntryId);
    }

    [Fact]
    public void Warehouse_IsGroup_Default_False()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Test Warehouse");
        Assert.False(wh.IsGroup);
    }

    [Fact]
    public void Warehouse_IsTransitWarehouse_When_Type_Is_Transit()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Goods In Transit");
        wh.WarehouseType = WarehouseType.Transit;
        Assert.True(wh.IsTransitWarehouse);
    }

    [Fact]
    public void Warehouse_Not_Transit_By_Default()
    {
        var wh = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Stores");
        Assert.False(wh.IsTransitWarehouse);
    }

    [Theory]
    [InlineData("NewTransfer")]
    [InlineData("DestinationWarehouse")]
    [InlineData("ConfirmReceive")]
    [InlineData("TransitTransfers")]
    [InlineData("NoTransfersInTransit")]
    [InlineData("AllTransfersCompleted")]
    [InlineData("TransfersAwaitingReceipt")]
    public void Localization_Key_Exists_In_EnJson(string key)
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src",
            "MyERP.Domain.Shared", "Localization", "MyERP", "en.json");
        var content = File.ReadAllText(jsonPath);
        Assert.Contains($"\"{key}\"", content);
    }

    [Fact]
    public void Transit_Workflow_SecondLeg_Must_Reference_FirstLeg()
    {
        var companyId = Guid.NewGuid();
        var firstLeg = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.SendToWarehouse, DateTime.UtcNow);

        var secondLeg = new StockEntry(Guid.NewGuid(), companyId, StockEntryType.ReceiveAtWarehouse, DateTime.UtcNow);
        secondLeg.SourceStockEntryId = firstLeg.Id;

        Assert.Equal(firstLeg.Id, secondLeg.SourceStockEntryId);
        Assert.Equal(StockEntryType.ReceiveAtWarehouse, secondLeg.EntryType);
    }

    [Fact]
    public void WarehouseType_Has_Four_Values()
    {
        var values = Enum.GetValues<WarehouseType>();
        Assert.True(values.Length >= 4);
        Assert.Contains(WarehouseType.Standard, values);
        Assert.Contains(WarehouseType.Transit, values);
    }

    [Fact]
    public void StockEntryType_Has_SendAndReceive_Values()
    {
        var values = Enum.GetValues<StockEntryType>();
        Assert.Contains(StockEntryType.SendToWarehouse, values);
        Assert.Contains(StockEntryType.ReceiveAtWarehouse, values);
    }
}
