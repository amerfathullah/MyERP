using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Serial No Ledger report application service.
/// Tracks chronological lifecycle movements, voucher links, and status of serial numbers.
/// Maps to ERPNext stock/report/serial_no_ledger/serial_no_ledger.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialNoLedgerAppService : ApplicationService, ISerialNoLedgerAppService
{
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<SerialAndBatchBundle, Guid> _bundleRepository;
    private readonly IRepository<SerialNo, Guid> _serialNoRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Item, Guid> _itemRepository;

    public SerialNoLedgerAppService(
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<SerialAndBatchBundle, Guid> bundleRepository,
        IRepository<SerialNo, Guid> serialNoRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Item, Guid> itemRepository)
    {
        _sleRepository = sleRepository;
        _bundleRepository = bundleRepository;
        _serialNoRepository = serialNoRepository;
        _warehouseRepository = warehouseRepository;
        _itemRepository = itemRepository;
    }

    public async Task<SerialNoLedgerReportDto> GetReportAsync(SerialNoLedgerFilterDto input)
    {
        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;
        var toDateCutoff = toDate.AddDays(1).AddTicks(-1);

        // 1. Warehouses
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        whQuery = whQuery.Where(w => !w.IsGroup && w.IsActive);
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.CompanyId == input.CompanyId.Value);
        }
        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            whQuery = whQuery.Where(w => w.Id == input.WarehouseId.Value);
        }
        var warehouses = whQuery.ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);
        var allowedWhIds = whMap.Keys.ToHashSet();

        // 2. Items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            itemQuery = itemQuery.Where(i => i.Id == input.ItemId.Value);
        }
        var items = itemQuery.ToList();
        var itemMap = items.ToDictionary(i => i.Id);

        // 3. Serial Nos
        var snQuery = await _serialNoRepository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.SerialNumber))
        {
            snQuery = snQuery.Where(s => s.SerialNumber == input.SerialNumber.Trim());
        }
        var serialList = snQuery.ToList();
        var serialIdMap = serialList.ToDictionary(s => s.Id);

        // 4. Stock Ledger Entries
        var sleQuery = await _sleRepository.GetQueryableAsync();
        sleQuery = sleQuery.Where(s => !s.IsCancelled && s.PostingDateTime <= toDateCutoff);
        if (input.FromDate.HasValue)
        {
            sleQuery = sleQuery.Where(s => s.PostingDateTime >= input.FromDate.Value.Date);
        }
        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.CompanyId == input.CompanyId.Value);
        }
        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);
        }
        if (allowedWhIds.Count > 0)
        {
            sleQuery = sleQuery.Where(s => allowedWhIds.Contains(s.WarehouseId));
        }

        var sles = sleQuery
            .Where(s => s.SerialNoId.HasValue || (s.SerialAndBatchBundleId.HasValue && s.SerialAndBatchBundleId.Value != Guid.Empty))
            .OrderByDescending(s => s.PostingDateTime)
            .ThenByDescending(s => s.CreationTime)
            .ToList();

        // 5. Bundles
        var bundleIds = sles
            .Where(s => s.SerialAndBatchBundleId.HasValue && s.SerialAndBatchBundleId.Value != Guid.Empty)
            .Select(s => s.SerialAndBatchBundleId!.Value)
            .Distinct()
            .ToList();

        var bundleMap = new Dictionary<Guid, SerialAndBatchBundle>();
        if (bundleIds.Count > 0)
        {
            var bundleQuery = await _bundleRepository.WithDetailsAsync(b => b.Entries);
            var bundles = bundleQuery
                .Where(b => bundleIds.Contains(b.Id) && !b.IsCancelled)
                .ToList();
            bundleMap = bundles.ToDictionary(b => b.Id);
        }

        var rows = new List<SerialNoLedgerRowDto>();
        int inwardCount = 0;
        int outwardCount = 0;

        foreach (var s in sles)
        {
            var item = itemMap.GetValueOrDefault(s.ItemId);
            var whName = whMap.GetValueOrDefault(s.WarehouseId) ?? "Unknown";

            // Case A: Direct SerialNoId
            if (s.SerialNoId.HasValue && serialIdMap.TryGetValue(s.SerialNoId.Value, out var snObj))
            {
                if (string.IsNullOrWhiteSpace(input.SerialNumber) ||
                    string.Equals(snObj.SerialNumber, input.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    decimal qty = s.QuantityChange >= 0 ? 1m : -1m;
                    if (qty > 0) inwardCount++; else outwardCount++;

                    rows.Add(BuildRow(s, snObj.SerialNumber, item, whName, qty, Math.Abs(s.ValuationRate)));
                }
            }

            // Case B: Bundle entries
            if (s.SerialAndBatchBundleId.HasValue && bundleMap.TryGetValue(s.SerialAndBatchBundleId.Value, out var bundle))
            {
                foreach (var entry in bundle.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(entry.SerialNo))
                    {
                        if (string.IsNullOrWhiteSpace(input.SerialNumber) ||
                            string.Equals(entry.SerialNo, input.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            decimal qty = bundle.TypeOfTransaction == BundleTransactionType.Outward ? -1m : 1m;
                            if (qty > 0) inwardCount++; else outwardCount++;

                            decimal rate = entry.IncomingRate != 0 ? entry.IncomingRate : Math.Abs(s.ValuationRate);
                            rows.Add(BuildRow(s, entry.SerialNo, item, whName, qty, rate));
                        }
                    }
                }
            }
        }

        // Apply search term filter if provided
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.SerialNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.VoucherType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.PartyName != null && r.PartyName.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        return new SerialNoLedgerReportDto
        {
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Rows = rows,
            InwardCount = inwardCount,
            OutwardCount = outwardCount
        };
    }

    private static SerialNoLedgerRowDto BuildRow(
        StockLedgerEntry s,
        string serialNumber,
        Item? item,
        string warehouseName,
        decimal qty,
        decimal valuationRate)
    {
        string status = qty > 0 ? "Active" :
            (s.VoucherType == "Delivery Note" || s.VoucherType == "Sales Invoice") ? "Delivered" :
            (s.VoucherType == "Stock Entry") ? "Consumed" : "Inactive";

        string? partyType = null;
        if (s.VoucherType == "Purchase Receipt" || s.VoucherType == "Purchase Invoice")
        {
            partyType = "Supplier";
        }
        else if (s.VoucherType == "Delivery Note" || s.VoucherType == "Sales Invoice")
        {
            partyType = "Customer";
        }

        return new SerialNoLedgerRowDto
        {
            PostingDateTime = s.PostingDateTime,
            SerialNumber = serialNumber,
            ItemId = s.ItemId,
            ItemCode = item?.ItemCode ?? string.Empty,
            ItemName = item?.ItemName ?? string.Empty,
            WarehouseId = s.WarehouseId,
            WarehouseName = warehouseName,
            Qty = qty,
            ValuationRate = valuationRate,
            VoucherType = s.VoucherType ?? string.Empty,
            VoucherId = s.VoucherId,
            VoucherNo = s.VoucherId.HasValue ? s.VoucherId.Value.ToString().Substring(0, 8).ToUpper() : string.Empty,
            Status = status,
            PartyType = partyType
        };
    }
}
