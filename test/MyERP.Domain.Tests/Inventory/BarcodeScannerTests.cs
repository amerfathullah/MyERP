using System;
using Xunit;
using MyERP.Inventory.DomainServices;

namespace MyERP.Domain.Tests.Inventory;

public class BarcodeScannerTests
{
    [Fact]
    public void BarcodeScanResult_DefaultState()
    {
        var result = new BarcodeScanResult();
        Assert.False(result.Success);
        Assert.Equal(BarcodeScanType.None, result.ScanType);
        Assert.Equal(string.Empty, result.Barcode);
        Assert.Null(result.ItemId);
        Assert.Null(result.SerialNoId);
        Assert.Null(result.BatchId);
        Assert.Null(result.WarehouseId);
        Assert.True(result.MaintainStock);
    }

    [Fact]
    public void BarcodeScanResult_ItemBarcode()
    {
        var itemId = Guid.NewGuid();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.ItemBarcode,
            Barcode = "8901234567890",
            ItemId = itemId,
            ItemCode = "ITEM-001",
            ItemName = "Test Widget",
            HasSerialNo = false,
            HasBatchNo = true,
            Uom = "Unit"
        };

        Assert.True(result.Success);
        Assert.Equal(BarcodeScanType.ItemBarcode, result.ScanType);
        Assert.Equal(itemId, result.ItemId);
        Assert.Equal("ITEM-001", result.ItemCode);
        Assert.True(result.HasBatchNo);
        Assert.False(result.HasSerialNo);
    }

    [Fact]
    public void BarcodeScanResult_SerialNo_IncludesWarehouse()
    {
        var warehouseId = Guid.NewGuid();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.SerialNo,
            Barcode = "SN-12345",
            SerialNoId = Guid.NewGuid(),
            SerialNumber = "SN-12345",
            WarehouseId = warehouseId,
            HasSerialNo = true
        };

        Assert.Equal(BarcodeScanType.SerialNo, result.ScanType);
        Assert.Equal(warehouseId, result.WarehouseId);
        Assert.True(result.HasSerialNo);
    }

    [Fact]
    public void BarcodeScanResult_Warehouse()
    {
        var warehouseId = Guid.NewGuid();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.Warehouse,
            Barcode = "WH-MAIN",
            WarehouseId = warehouseId,
            WarehouseName = "Main Warehouse"
        };

        Assert.Equal(BarcodeScanType.Warehouse, result.ScanType);
        Assert.Equal("Main Warehouse", result.WarehouseName);
        Assert.Null(result.ItemId);
    }

    [Fact]
    public void BarcodeScanResult_Failure()
    {
        var result = new BarcodeScanResult
        {
            Success = false,
            Barcode = "UNKNOWN-CODE",
            Message = "No match found"
        };

        Assert.False(result.Success);
        Assert.Equal("No match found", result.Message);
    }

    // --- DetermineScanAction tests ---

    [Fact]
    public void DetermineScanAction_NoMatch_ReturnsNoMatch()
    {
        var service = CreateService();
        var result = new BarcodeScanResult { Success = false };

        var action = service.DetermineScanAction(result, hasSerialNo: false);
        Assert.Equal(ScanAction.NoMatch, action);
    }

    [Fact]
    public void DetermineScanAction_Warehouse_SetsContext()
    {
        var service = CreateService();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.Warehouse
        };

        var action = service.DetermineScanAction(result, hasSerialNo: false);
        Assert.Equal(ScanAction.SetWarehouseContext, action);
    }

    [Fact]
    public void DetermineScanAction_SerialItem_AlwaysNewRow()
    {
        var service = CreateService();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.ItemBarcode,
            HasSerialNo = true
        };

        // Per gotcha #127: serial items always get new rows
        var action = service.DetermineScanAction(result, hasSerialNo: true);
        Assert.Equal(ScanAction.AddNewRow, action);
    }

    [Fact]
    public void DetermineScanAction_SerialNoResult_AlwaysNewRow()
    {
        var service = CreateService();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.SerialNo,
            SerialNoId = Guid.NewGuid()
        };

        var action = service.DetermineScanAction(result, hasSerialNo: false);
        Assert.Equal(ScanAction.AddNewRow, action);
    }

    [Fact]
    public void DetermineScanAction_NonSerialItem_IncrementsQty()
    {
        var service = CreateService();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.ItemBarcode,
            HasSerialNo = false
        };

        // Per gotcha #127: repeat scan increments qty on existing row
        var action = service.DetermineScanAction(result, hasSerialNo: false);
        Assert.Equal(ScanAction.IncrementExistingRow, action);
    }

    [Fact]
    public void DetermineScanAction_BatchItem_IncrementsQty()
    {
        var service = CreateService();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.BatchNo,
            HasBatchNo = true,
            HasSerialNo = false
        };

        // Batch items without serial → increment existing row
        var action = service.DetermineScanAction(result, hasSerialNo: false);
        Assert.Equal(ScanAction.IncrementExistingRow, action);
    }

    // --- Enum value tests ---

    [Fact]
    public void BarcodeScanType_HasAllValues()
    {
        Assert.Equal(0, (int)BarcodeScanType.None);
        Assert.Equal(1, (int)BarcodeScanType.ItemBarcode);
        Assert.Equal(2, (int)BarcodeScanType.SerialNo);
        Assert.Equal(3, (int)BarcodeScanType.BatchNo);
        Assert.Equal(4, (int)BarcodeScanType.Warehouse);
    }

    [Fact]
    public void ScanAction_HasAllValues()
    {
        Assert.Equal(0, (int)ScanAction.NoMatch);
        Assert.Equal(1, (int)ScanAction.IncrementExistingRow);
        Assert.Equal(2, (int)ScanAction.AddNewRow);
        Assert.Equal(3, (int)ScanAction.SetWarehouseContext);
    }

    [Fact]
    public void BarcodeScanResult_BatchNo_WithItemInfo()
    {
        var itemId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var result = new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.BatchNo,
            Barcode = "BATCH-2026-001",
            ItemId = itemId,
            ItemCode = "RM-STEEL",
            BatchId = batchId,
            BatchNo = "BATCH-2026-001",
            HasBatchNo = true
        };

        Assert.Equal(BarcodeScanType.BatchNo, result.ScanType);
        Assert.Equal(itemId, result.ItemId);
        Assert.Equal(batchId, result.BatchId);
        Assert.Equal("BATCH-2026-001", result.BatchNo);
    }

    [Fact]
    public void BarcodeScanResult_Message_OnFailure()
    {
        var result = new BarcodeScanResult
        {
            Success = false,
            Barcode = "SERIAL-FOR-BATCH-ITEM",
            Message = "Item has serial numbers. Please scan the serial number instead of batch."
        };

        Assert.False(result.Success);
        Assert.Contains("serial number", result.Message);
    }

    // Helper to create service instance (DetermineScanAction is a pure method)
    private static BarcodeScannerService CreateService()
    {
        // DetermineScanAction doesn't use repositories, so we can pass nulls for pure logic testing
        return new BarcodeScannerService(null!, null!, null!, null!);
    }
}
