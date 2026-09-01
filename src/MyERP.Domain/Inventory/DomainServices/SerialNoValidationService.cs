using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Inventory.DomainServices;

/// <summary>
/// Domain service for validating Serial Numbers on outward stock transactions (Delivery Note, Sales Invoice with stock update, Stock Entry outward).
/// Per ERPNext: serial_no.py and stock_ledger_entry.py (PR #58394):
/// - Serial numbers must be active and not consumed/delivered.
/// - Serial numbers must reside in the specified source warehouse.
/// - Duplicate serial numbers within a transaction are rejected.
/// - Serial numbers must match the transacted item.
/// </summary>
public class SerialNoValidationService : DomainService
{
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;

    public SerialNoValidationService(IRepository<SerialNo, Guid> serialNoRepository)
    {
        _serialNoRepository = serialNoRepository;
    }

    /// <summary>
    /// Validates serial numbers for outward stock movements.
    /// </summary>
    public async Task ValidateForStockOutAsync(IEnumerable<SerialNoValidationItem> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return;

        // Check for duplicate serial numbers within the transaction
        var serialStrings = itemList
            .Where(i => !string.IsNullOrWhiteSpace(i.SerialNumber))
            .Select(i => i.SerialNumber!.Trim())
            .ToList();

        if (serialStrings.Count != serialStrings.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            var duplicate = serialStrings.GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .First(g => g.Count() > 1).Key;
            throw new BusinessException(MyERPDomainErrorCodes.SerialNoDuplicate)
                .WithData("serialNo", duplicate);
        }

        var serialNos = await _serialNoRepository.GetListAsync(s => serialStrings.Contains(s.SerialNumber));
        var serialMap = serialNos.ToDictionary(s => s.SerialNumber, StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemList)
        {
            if (string.IsNullOrWhiteSpace(item.SerialNumber))
                continue;

            if (!serialMap.TryGetValue(item.SerialNumber.Trim(), out var serial))
            {
                throw new BusinessException(MyERPDomainErrorCodes.SerialNoNotFound)
                    .WithData("serialNo", item.SerialNumber)
                    .WithData("item", item.ItemName ?? item.ItemId.ToString());
            }

            if (serial.ItemId != item.ItemId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Serial No {serial.SerialNumber} belongs to a different item.");
            }

            if (serial.Status != SerialNoStatus.Active)
            {
                throw new BusinessException(MyERPDomainErrorCodes.SerialNoNotActive)
                    .WithData("serialNo", serial.SerialNumber)
                    .WithData("status", serial.Status.ToString());
            }

            if (item.WarehouseId.HasValue && serial.WarehouseId != item.WarehouseId.Value)
            {
                throw new BusinessException(MyERPDomainErrorCodes.SerialNoWarehouseMismatch)
                    .WithData("serialNo", serial.SerialNumber)
                    .WithData("expectedWarehouse", item.WarehouseId.Value)
                    .WithData("actualWarehouse", serial.WarehouseId?.ToString() ?? "None");
            }
        }
    }
}

/// <summary>
/// Data container for serial number validation input.
/// </summary>
public class SerialNoValidationItem
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? WarehouseId { get; set; }

    public SerialNoValidationItem(Guid itemId, string? serialNumber, Guid? warehouseId = null, string? itemName = null)
    {
        ItemId = itemId;
        SerialNumber = serialNumber;
        WarehouseId = warehouseId;
        ItemName = itemName;
    }
}
