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
    private readonly IRepository<SerialAndBatchBundle, Guid>? _bundleRepository;

    public SerialNoValidationService(
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<SerialAndBatchBundle, Guid>? bundleRepository = null)
    {
        _serialNoRepository = serialNoRepository;
        _bundleRepository = bundleRepository;
    }

    /// <summary>
    /// Validates serial numbers for outward stock movements.
    /// </summary>
    public async Task ValidateForStockOutAsync(IEnumerable<SerialNoValidationItem> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return;

        // Check for duplicate serial numbers for the same item within the transaction
        // Per ERPNext PR #59049: allow shared serial numbers across different items
        var itemSerialPairs = itemList
            .Where(i => !string.IsNullOrWhiteSpace(i.SerialNumber))
            .Select(i => (i.ItemId, Serial: i.SerialNumber!.Trim()))
            .ToList();

        if (itemSerialPairs.Count != itemSerialPairs.DistinctBy(p => (p.ItemId, Serial: p.Serial.ToUpperInvariant())).Count())
        {
            var duplicate = itemSerialPairs
                .GroupBy(p => (p.ItemId, Serial: p.Serial.ToUpperInvariant()))
                .First(g => g.Count() > 1).Key;
            throw new BusinessException(MyERPDomainErrorCodes.SerialNoDuplicate)
                .WithData("serialNo", duplicate.Serial);
        }

        var serialStrings = itemSerialPairs.Select(p => p.Serial).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var itemIds = itemSerialPairs.Select(p => p.ItemId).Distinct().ToList();

        var serialNos = await _serialNoRepository.GetListAsync(s => itemIds.Contains(s.ItemId) && serialStrings.Contains(s.SerialNumber));
        var serialMap = serialNos.ToDictionary(s => (s.ItemId, s.SerialNumber.Trim().ToUpperInvariant()));

        foreach (var item in itemList)
        {
            if (string.IsNullOrWhiteSpace(item.SerialNumber))
                continue;

            if (!serialMap.TryGetValue((item.ItemId, item.SerialNumber.Trim().ToUpperInvariant()), out var serial))
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

    /// <summary>
    /// Validates customer ownership for standalone returns (no ReturnAgainstId).
    /// Per ERPNext selling_controller.py validate_standalone_serial_nos_customer:
    /// If a returned serial is assigned to a customer, it must match the document customer.
    /// Prevents returning serials that belong to a different customer.
    /// </summary>
    public async Task ValidateStandaloneReturnCustomerAsync(
        Guid customerId,
        IEnumerable<Guid>? serialNoIds = null,
        IEnumerable<string>? serialNumbers = null,
        IEnumerable<Guid>? bundleIds = null)
    {
        var serialStrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (serialNumbers != null)
        {
            foreach (var s in serialNumbers.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                serialStrings.Add(s.Trim());
            }
        }

        var serialIds = serialNoIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();

        // If bundles provided, pull serial numbers from bundle entries
        if (bundleIds != null)
        {
            var validBundleIds = bundleIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (validBundleIds.Any())
            {
                var bundleRepo = _bundleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<SerialAndBatchBundle, Guid>>();
                if (bundleRepo != null)
                {
                    var bundleQuery = await bundleRepo.GetQueryableAsync();
                    var bundles = bundleQuery.Where(b => validBundleIds.Contains(b.Id)).ToList();
                    foreach (var bundle in bundles)
                    {
                        foreach (var entry in bundle.Entries.Where(e => !string.IsNullOrWhiteSpace(e.SerialNo)))
                        {
                            serialStrings.Add(entry.SerialNo!.Trim());
                        }
                    }
                }
            }
        }

        if (!serialIds.Any() && !serialStrings.Any())
            return;

        var serialQuery = await _serialNoRepository.GetQueryableAsync();
        var matchingSerials = serialQuery
            .Where(s => s.CustomerId.HasValue && (serialIds.Contains(s.Id) || (s.SerialNumber != null && serialStrings.Contains(s.SerialNumber))))
            .ToList();

        foreach (var sn in matchingSerials)
        {
            if (sn.CustomerId.HasValue && sn.CustomerId.Value != customerId)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ReturnSerialCustomerMismatch)
                    .WithData("serialNo", sn.SerialNumber)
                    .WithData("assignedCustomer", sn.CustomerId.Value)
                    .WithData("returnCustomer", customerId);
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
