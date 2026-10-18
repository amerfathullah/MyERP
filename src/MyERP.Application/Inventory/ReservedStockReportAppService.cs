using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Reserved Stock Report application service.
/// Tracks stock reservation entries, fulfilled quantities, and available unreserved stock across warehouses.
/// Maps to ERPNext stock/report/reserved_stock/reserved_stock.py.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class ReservedStockReportAppService : ApplicationService, IReservedStockReportAppService
{
    private readonly IRepository<StockReservationEntry, Guid> _reservationRepository;
    private readonly IRepository<Bin, Guid> _binRepository;
    private readonly IRepository<Item, Guid> _itemRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IRepository<Batch, Guid> _batchRepository;

    public ReservedStockReportAppService(
        IRepository<StockReservationEntry, Guid> reservationRepository,
        IRepository<Bin, Guid> binRepository,
        IRepository<Item, Guid> itemRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IRepository<Batch, Guid> batchRepository)
    {
        _reservationRepository = reservationRepository;
        _binRepository = binRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
    }

    public async Task<ReservedStockReportDto> GetReportAsync(ReservedStockFilterDto input)
    {
        var fromDate = input.FromDate?.Date ?? DateTime.UtcNow.Date.AddMonths(-1);
        var toDate = input.ToDate?.Date ?? DateTime.UtcNow.Date;
        var toDateCutoff = toDate.AddDays(1).AddTicks(-1);

        var query = await _reservationRepository.GetQueryableAsync();
        query = query.Where(s => s.CreationTime >= fromDate && s.CreationTime <= toDateCutoff);

        if (input.CompanyId.HasValue && input.CompanyId.Value != Guid.Empty)
        {
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        }

        if (input.ItemId.HasValue && input.ItemId.Value != Guid.Empty)
        {
            query = query.Where(s => s.ItemId == input.ItemId.Value);
        }

        if (input.WarehouseId.HasValue && input.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(s => s.WarehouseId == input.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.VoucherType))
        {
            query = query.Where(s => s.VoucherType == input.VoucherType);
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<DocumentStatus>(input.Status, true, out var status))
        {
            query = query.Where(s => s.Status == status);
        }
        else
        {
            query = query.Where(s => s.Status == DocumentStatus.Submitted);
        }

        var reservations = query.ToList();
        if (reservations.Count == 0)
        {
            return new ReservedStockReportDto { FromDate = fromDate, ToDate = toDate };
        }

        // 1. Fetch Bins for (ItemId, WarehouseId)
        var itemIds = reservations.Select(r => r.ItemId).Distinct().ToList();
        var warehouseIds = reservations.Select(r => r.WarehouseId).Distinct().ToList();

        var binQuery = await _binRepository.GetQueryableAsync();
        var bins = binQuery
            .Where(b => itemIds.Contains(b.ItemId) && warehouseIds.Contains(b.WarehouseId))
            .ToList();

        var binMap = bins.ToDictionary(
            b => (b.ItemId, b.WarehouseId),
            b => Math.Max(0, b.ActualQty - b.ReservedQty));

        // 2. Fetch Items
        var itemQuery = await _itemRepository.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id)).ToList();
        var itemMap = items.ToDictionary(i => i.Id);

        // 3. Fetch Warehouses
        var whQuery = await _warehouseRepository.GetQueryableAsync();
        var warehouses = whQuery.Where(w => warehouseIds.Contains(w.Id)).ToList();
        var whMap = warehouses.ToDictionary(w => w.Id, w => w.Name);

        // 4. Fetch Batches
        var batchIds = reservations.Where(r => r.BatchId.HasValue).Select(r => r.BatchId!.Value).Distinct().ToList();
        Dictionary<Guid, string> batchMap = new();
        if (batchIds.Count > 0)
        {
            var batchQuery = await _batchRepository.GetQueryableAsync();
            batchMap = batchQuery.Where(b => batchIds.Contains(b.Id)).ToDictionary(b => b.Id, b => b.BatchNo);
        }

        // 5. Build rows
        var rows = new List<ReservedStockRowDto>();
        foreach (var r in reservations)
        {
            var item = itemMap.GetValueOrDefault(r.ItemId);
            var whName = whMap.GetValueOrDefault(r.WarehouseId) ?? string.Empty;
            var batchNo = r.BatchId.HasValue ? batchMap.GetValueOrDefault(r.BatchId.Value) : null;
            var freeQty = binMap.GetValueOrDefault((r.ItemId, r.WarehouseId));

            rows.Add(new ReservedStockRowDto
            {
                StockReservationEntryId = r.Id,
                Date = r.CreationTime,
                WarehouseId = r.WarehouseId,
                WarehouseName = whName,
                ItemId = r.ItemId,
                ItemCode = item?.ItemCode ?? string.Empty,
                ItemName = item?.ItemName ?? string.Empty,
                StockUom = item?.Uom,
                VoucherQty = r.VoucherQty,
                ReservedQty = r.ReservedQty,
                DeliveredQty = r.DeliveredQty,
                TransferredQty = r.TransferredQty,
                ConsumedQty = r.ConsumedQty,
                AvailableQtyToReserve = freeQty,
                VoucherType = r.VoucherType,
                VoucherId = r.VoucherId,
                FromVoucherType = r.FromVoucherType,
                FromVoucherId = r.FromVoucherId,
                Status = r.Status.ToString(),
                BatchId = r.BatchId,
                BatchNo = batchNo
            });
        }

        // 6. Search filter
        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var term = input.SearchTerm.Trim();
            rows = rows.Where(r =>
                r.ItemCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.VoucherType.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.BatchNo != null && r.BatchNo.Contains(term, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // 7. Sort by Date descending
        rows = rows.OrderByDescending(r => r.Date).ToList();

        return new ReservedStockReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Rows = rows,
            TotalEntries = rows.Count,
            TotalReservedQty = rows.Sum(r => r.ReservedQty),
            TotalDeliveredQty = rows.Sum(r => r.DeliveredQty),
            TotalTransferredQty = rows.Sum(r => r.TransferredQty),
            TotalConsumedQty = rows.Sum(r => r.ConsumedQty),
            TotalAvailableQty = rows.Sum(r => r.AvailableQtyToReserve)
        };
    }
}
