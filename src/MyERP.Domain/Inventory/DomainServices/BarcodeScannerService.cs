using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using MyERP.Inventory.Entities;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Barcode scanning service implementing ERPNext's 4-stage resolution chain.
/// Per gotcha #155: Item Barcode → Serial No → Batch No → Warehouse.
/// Per gotcha #475: row matching uses 6 conditions for transaction line allocation.
/// </summary>
public class BarcodeScannerService : DomainService
{
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public BarcodeScannerService(
        IRepository<Item, Guid> itemRepository,
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<Batch, Guid> batchRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _itemRepository = itemRepository;
        _serialNoRepository = serialNoRepository;
        _batchRepository = batchRepository;
        _warehouseRepository = warehouseRepository;
    }

    /// <summary>
    /// Resolves a barcode scan to an item, serial, batch, or warehouse.
    /// 4-stage resolution chain (per ERPNext scan_barcode API):
    /// 1. Item Barcode (stored barcode field on Item)
    /// 2. Serial No (serial number itself as barcode)
    /// 3. Batch No (batch number itself as barcode)
    /// 4. Warehouse (warehouse name/code as barcode)
    /// </summary>
    public async Task<BarcodeScanResult> ScanAsync(string barcode, Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            throw new BusinessException(MyERPDomainErrorCodes.BarcodeRequired)
                .WithData("field", "barcode");

        var trimmedBarcode = barcode.Trim();

        // Stage 1: Item Barcode lookup
        var itemResult = await ResolveFromItemBarcodeAsync(trimmedBarcode);
        if (itemResult != null) return itemResult;

        // Stage 2: Serial No lookup
        var serialResult = await ResolveFromSerialNoAsync(trimmedBarcode);
        if (serialResult != null) return serialResult;

        // Stage 3: Batch No lookup
        var batchResult = await ResolveFromBatchNoAsync(trimmedBarcode);
        if (batchResult != null) return batchResult;

        // Stage 4: Warehouse lookup
        var warehouseResult = await ResolveFromWarehouseAsync(trimmedBarcode);
        if (warehouseResult != null) return warehouseResult;

        // Nothing found
        return new BarcodeScanResult
        {
            Success = false,
            Barcode = trimmedBarcode,
            Message = $"No item, serial number, batch, or warehouse found for barcode: {trimmedBarcode}"
        };
    }

    /// <summary>
    /// Determines whether a repeated scan should increment qty on existing row or add new row.
    /// Per gotcha #127: repeat barcode scan increments qty (exception: serial items always new row).
    /// </summary>
    public ScanAction DetermineScanAction(BarcodeScanResult scanResult, bool hasSerialNo)
    {
        if (!scanResult.Success)
            return ScanAction.NoMatch;

        if (scanResult.ScanType == BarcodeScanType.Warehouse)
            return ScanAction.SetWarehouseContext;

        if (hasSerialNo || scanResult.SerialNoId.HasValue)
            return ScanAction.AddNewRow;

        return ScanAction.IncrementExistingRow;
    }

    private async Task<BarcodeScanResult?> ResolveFromItemBarcodeAsync(string barcode)
    {
        var queryable = await _itemRepository.GetQueryableAsync();
        var item = await Task.Run(() =>
            queryable.FirstOrDefault(i =>
                i.Barcode != null && i.Barcode == barcode && i.IsActive));

        if (item == null) return null;

        return new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.ItemBarcode,
            Barcode = barcode,
            ItemId = item.Id,
            ItemCode = item.ItemCode,
            ItemName = item.ItemName,
            HasSerialNo = item.HasSerialNo,
            HasBatchNo = item.HasBatchNo,
            Uom = item.Uom,
            MaintainStock = item.MaintainStock
        };
    }

    private async Task<BarcodeScanResult?> ResolveFromSerialNoAsync(string barcode)
    {
        var queryable = await _serialNoRepository.GetQueryableAsync();
        var serial = await Task.Run(() =>
            queryable.FirstOrDefault(s => s.SerialNumber == barcode));

        if (serial == null) return null;

        // Per gotcha #155: batch scan for serialized items throws (must scan serial instead)
        // But here we're scanning the serial itself — resolve item from it
        var item = await _itemRepository.FindAsync(serial.ItemId);

        return new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.SerialNo,
            Barcode = barcode,
            ItemId = serial.ItemId,
            ItemCode = item?.ItemCode,
            ItemName = item?.ItemName,
            SerialNoId = serial.Id,
            SerialNumber = serial.SerialNumber,
            HasSerialNo = true,
            HasBatchNo = item?.HasBatchNo ?? false,
            Uom = item?.Uom,
            WarehouseId = serial.WarehouseId,
            MaintainStock = item?.MaintainStock ?? true
        };
    }

    private async Task<BarcodeScanResult?> ResolveFromBatchNoAsync(string barcode)
    {
        var queryable = await _batchRepository.GetQueryableAsync();
        var batch = await Task.Run(() =>
            queryable.FirstOrDefault(b => b.BatchNo == barcode));

        if (batch == null) return null;

        var item = await _itemRepository.FindAsync(batch.ItemId);

        // Per gotcha #155: batch scan for serialized items throws (must scan serial instead)
        if (item?.HasSerialNo == true)
        {
            return new BarcodeScanResult
            {
                Success = false,
                Barcode = barcode,
                Message = "Item has serial numbers. Please scan the serial number instead of batch."
            };
        }

        return new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.BatchNo,
            Barcode = barcode,
            ItemId = batch.ItemId,
            ItemCode = item?.ItemCode,
            ItemName = item?.ItemName,
            BatchId = batch.Id,
            BatchNo = batch.BatchNo,
            HasSerialNo = false,
            HasBatchNo = true,
            Uom = item?.Uom,
            MaintainStock = item?.MaintainStock ?? true
        };
    }

    private async Task<BarcodeScanResult?> ResolveFromWarehouseAsync(string barcode)
    {
        var queryable = await _warehouseRepository.GetQueryableAsync();
        var warehouse = await Task.Run(() =>
            queryable.FirstOrDefault(w =>
                (w.WarehouseCode == barcode || w.Name == barcode) &&
                w.IsActive && !w.IsGroup));

        if (warehouse == null) return null;

        return new BarcodeScanResult
        {
            Success = true,
            ScanType = BarcodeScanType.Warehouse,
            Barcode = barcode,
            WarehouseId = warehouse.Id,
            WarehouseName = warehouse.Name
        };
    }
}

/// <summary>
/// Result of a barcode scan resolution.
/// Contains all possible resolved data depending on scan type.
/// </summary>
public class BarcodeScanResult
{
    public bool Success { get; set; }
    public BarcodeScanType ScanType { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? Message { get; set; }

    // Item data (stages 1-3)
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public bool HasSerialNo { get; set; }
    public bool HasBatchNo { get; set; }
    public string? Uom { get; set; }
    public bool MaintainStock { get; set; } = true;

    // Serial data (stage 2)
    public Guid? SerialNoId { get; set; }
    public string? SerialNumber { get; set; }

    // Batch data (stage 2-3)
    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }

    // Warehouse data (stage 2, 4)
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
}

/// <summary>
/// Type of barcode scan resolution.
/// </summary>
public enum BarcodeScanType
{
    None = 0,
    ItemBarcode = 1,
    SerialNo = 2,
    BatchNo = 3,
    Warehouse = 4
}

/// <summary>
/// Action to take after a successful barcode scan.
/// </summary>
public enum ScanAction
{
    NoMatch = 0,
    IncrementExistingRow = 1,
    AddNewRow = 2,
    SetWarehouseContext = 3
}
