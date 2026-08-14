using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

/// <summary>
/// Read-only listing of Serial and Batch Bundles for traceability.
/// Bundles are auto-created by stock movement services (SE, DN, PR) — not user-created.
/// Per ERPNext: SABB is the v16 replacement for legacy serial_no/batch_no fields.
/// </summary>
[Authorize(MyERPPermissions.StockEntries.Default)]
public class SerialAndBatchBundleAppService : ApplicationService, ISerialAndBatchBundleAppService
{
    private readonly IRepository<SerialAndBatchBundle, Guid> _repository;

    public SerialAndBatchBundleAppService(IRepository<SerialAndBatchBundle, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<SerialAndBatchBundleDto>> GetListAsync(GetBundleListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(b => b.CompanyId == input.CompanyId.Value);

        if (input.ItemId.HasValue)
            query = query.Where(b => b.ItemId == input.ItemId.Value);

        if (input.WarehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == input.WarehouseId.Value);

        if (!string.IsNullOrWhiteSpace(input.VoucherType))
            query = query.Where(b => b.VoucherType == input.VoucherType);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(b => b.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        // Resolve item names
        var itemIds = items.Select(b => b.ItemId).Distinct().ToList();
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemNames = itemQuery
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName })
            .ToDictionary(i => i.Id, i => i.ItemName);

        return new PagedResultDto<SerialAndBatchBundleDto>(
            totalCount,
            items.Select(b => new SerialAndBatchBundleDto
            {
                Id = b.Id,
                ItemId = b.ItemId,
                ItemName = itemNames.ContainsKey(b.ItemId) ? itemNames[b.ItemId] : "",
                WarehouseId = b.WarehouseId,
                BundleType = b.TypeOfTransaction.ToString(),
                VoucherType = b.VoucherType,
                VoucherId = b.VoucherId,
                PostingDate = b.PostingDate,
                TotalQty = b.TotalQty,
                TotalAmount = b.TotalAmount,
                EntryCount = b.Entries.Count,
                IsCancelled = b.IsCancelled,
            }).ToList());
    }

    public async Task<SerialAndBatchBundleDto> GetAsync(Guid id)
    {
        var bundle = await _repository.GetAsync(id);

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var item = await itemRepo.FindAsync(bundle.ItemId);

        return new SerialAndBatchBundleDto
        {
            Id = bundle.Id,
            ItemId = bundle.ItemId,
            ItemName = item?.ItemName ?? "",
            WarehouseId = bundle.WarehouseId,
            BundleType = bundle.TypeOfTransaction.ToString(),
            VoucherType = bundle.VoucherType,
            VoucherId = bundle.VoucherId,
            PostingDate = bundle.PostingDate,
            TotalQty = bundle.TotalQty,
            TotalAmount = bundle.TotalAmount,
            EntryCount = bundle.Entries.Count,
            IsCancelled = bundle.IsCancelled,
            Entries = bundle.Entries.Select(e => new BundleEntryDto
            {
                SerialNo = e.SerialNo,
                BatchNo = e.BatchId?.ToString(),
                Qty = e.Qty,
                Rate = e.IncomingRate,
            }).ToList(),
        };
    }
}
