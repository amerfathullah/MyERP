using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using MyERP.Inventory.DomainServices;
using MyERP.Permissions;

namespace MyERP.Inventory;

public interface IBarcodeScanAppService : IApplicationService
{
    Task<BarcodeScanResultDto> ScanAsync(string barcode);
}

[Authorize(MyERPPermissions.StockEntries.Default)]
public class BarcodeScanAppService : ApplicationService, IBarcodeScanAppService
{
    private readonly BarcodeScannerService _scannerService;

    public BarcodeScanAppService(BarcodeScannerService scannerService)
    {
        _scannerService = scannerService;
    }

    public async Task<BarcodeScanResultDto> ScanAsync(string barcode)
    {
        var result = await _scannerService.ScanAsync(barcode);

        return new BarcodeScanResultDto
        {
            Success = result.Success,
            ScanType = (int)result.ScanType,
            ScanTypeName = result.ScanType.ToString(),
            Barcode = result.Barcode,
            Message = result.Message,
            ItemId = result.ItemId,
            ItemCode = result.ItemCode,
            ItemName = result.ItemName,
            HasSerialNo = result.HasSerialNo,
            HasBatchNo = result.HasBatchNo,
            Uom = result.Uom,
            MaintainStock = result.MaintainStock,
            SerialNoId = result.SerialNoId,
            SerialNumber = result.SerialNumber,
            BatchId = result.BatchId,
            BatchNo = result.BatchNo,
            WarehouseId = result.WarehouseId,
            WarehouseName = result.WarehouseName,
            Action = (int)_scannerService.DetermineScanAction(result, result.HasSerialNo),
            ActionName = _scannerService.DetermineScanAction(result, result.HasSerialNo).ToString()
        };
    }
}

public class BarcodeScanResultDto
{
    public bool Success { get; set; }
    public int ScanType { get; set; }
    public string ScanTypeName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Message { get; set; }

    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public bool HasSerialNo { get; set; }
    public bool HasBatchNo { get; set; }
    public string? Uom { get; set; }
    public bool MaintainStock { get; set; }

    public Guid? SerialNoId { get; set; }
    public string? SerialNumber { get; set; }

    public Guid? BatchId { get; set; }
    public string? BatchNo { get; set; }

    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }

    /// <summary>
    /// Recommended action: 0=NoMatch, 1=IncrementExistingRow, 2=AddNewRow, 3=SetWarehouseContext
    /// </summary>
    public int Action { get; set; }
    public string ActionName { get; set; } = string.Empty;
}
