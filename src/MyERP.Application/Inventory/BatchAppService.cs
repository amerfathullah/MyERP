using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class BatchAppService : ApplicationService
{
    private readonly IRepository<Batch, Guid> _repository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;

    public BatchAppService(
        IRepository<Batch, Guid> repository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Warehouse, Guid> warehouseRepository)
    {
        _repository = repository;
        _sleRepository = sleRepository;
        _warehouseRepository = warehouseRepository;
    }

    public async Task<BatchDto> GetAsync(Guid id)
    {
        var batch = await _repository.GetAsync(id);
        return ObjectMapper.Map<Batch, BatchDto>(batch);
    }

    public async Task<PagedResultDto<BatchDto>> GetListAsync(GetBatchListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ItemId.HasValue)
            query = query.Where(b => b.ItemId == input.ItemId.Value);
        if (input.IsDisabled.HasValue)
            query = query.Where(b => b.IsDisabled == input.IsDisabled.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(b => b.BatchNo.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(b => b.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<BatchDto>(totalCount, items.Select(ObjectMapper.Map<Batch, BatchDto>).ToList());
    }

    [Authorize(MyERPPermissions.Items.Create)]
    public async Task<BatchDto> CreateAsync(CreateBatchDto input)
    {
        var batch = new Batch(GuidGenerator.Create(), input.ItemId, input.BatchNo, CurrentTenant.Id)
        {
            ManufacturingDate = input.ManufacturingDate,
            ExpiryDate = input.ExpiryDate,
            ShelfLifeInDays = input.ShelfLifeInDays,
            SupplierBatchNo = input.SupplierBatchNo,
            Description = input.Description,
        };

        if (batch.ManufacturingDate.HasValue && batch.ShelfLifeInDays.HasValue && !batch.ExpiryDate.HasValue)
            batch.SetExpiryFromShelfLife();

        await _repository.InsertAsync(batch);
        return ObjectMapper.Map<Batch, BatchDto>(batch);
    }

    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var batch = await _repository.GetAsync(id);
        batch.IsDisabled = true;
        await _repository.UpdateAsync(batch);
    }

    /// <summary>
    /// Per-warehouse stock balance for a batch. Derived from SLE aggregation.
    /// ERPNext equivalent: Batch stock dashboard showing qty per warehouse with Move/Split actions.
    /// </summary>
    public async Task<BatchStockBalanceDto> GetStockBalanceAsync(Guid batchId)
    {
        var batch = await _repository.GetAsync(batchId);

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();

        var warehouseBalances = sleQuery
            .Where(s => s.BatchId == batchId && !s.IsCancelled)
            .GroupBy(s => s.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, Qty = g.Sum(s => s.QuantityChange), Value = g.Sum(s => s.StockValue) })
            .Where(g => g.Qty != 0)
            .ToList();

        var warehouseIds = warehouseBalances.Select(w => w.WarehouseId).ToList();
        var warehouseNames = warehouseQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        var entries = warehouseBalances.Select(w => new BatchWarehouseBalanceDto
        {
            WarehouseId = w.WarehouseId,
            WarehouseName = warehouseNames.GetValueOrDefault(w.WarehouseId, "Unknown"),
            Quantity = w.Qty,
            StockValue = w.Value,
            ValuationRate = w.Qty != 0 ? w.Value / w.Qty : 0,
        }).OrderByDescending(e => e.Quantity).ToList();

        return new BatchStockBalanceDto
        {
            BatchId = batchId,
            BatchNo = batch.BatchNo,
            ItemId = batch.ItemId,
            TotalQuantity = entries.Sum(e => e.Quantity),
            TotalValue = entries.Sum(e => e.StockValue),
            WarehouseBalances = entries,
        };
    }

    /// <summary>
    /// Stock ledger entries for a specific batch (movement history).
    /// ERPNext equivalent: Stock Ledger filtered by batch_no showing all movements.
    /// </summary>
    public async Task<BatchMovementHistoryDto> GetMovementHistoryAsync(Guid batchId, int maxEntries = 50)
    {
        var batch = await _repository.GetAsync(batchId);

        var sleQuery = await _sleRepository.GetQueryableAsync();
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();

        var entries = sleQuery
            .Where(s => s.BatchId == batchId && !s.IsCancelled)
            .OrderByDescending(s => s.PostingDateTime)
            .ThenByDescending(s => s.CreationTime)
            .Take(maxEntries)
            .ToList();

        var warehouseIds = entries.Select(e => e.WarehouseId).Distinct().ToList();
        var warehouseNames = warehouseQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        return new BatchMovementHistoryDto
        {
            BatchId = batchId,
            BatchNo = batch.BatchNo,
            Entries = entries.Select(e => new BatchMovementEntryDto
            {
                Id = e.Id,
                PostingDate = e.PostingDate,
                WarehouseId = e.WarehouseId,
                WarehouseName = warehouseNames.GetValueOrDefault(e.WarehouseId, "Unknown"),
                QuantityChange = e.QuantityChange,
                ValuationRate = e.ValuationRate,
                VoucherType = e.VoucherType,
                VoucherId = e.VoucherId,
                IsInward = e.QuantityChange > 0,
            }).ToList(),
        };
    }
}

public class BatchDto : AuditedEntityDto<Guid>
{
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    public string? SupplierBatchNo { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsExpired { get; set; }
    public string? Description { get; set; }
}

public class CreateBatchDto
{
    [Required] public Guid ItemId { get; set; }
    [Required][StringLength(100)] public string BatchNo { get; set; } = null!;
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? ShelfLifeInDays { get; set; }
    [StringLength(100)] public string? SupplierBatchNo { get; set; }
    [StringLength(500)] public string? Description { get; set; }
}

public class GetBatchListDto : PagedAndSortedResultRequestDto
{
    public Guid? ItemId { get; set; }
    public bool? IsDisabled { get; set; }
    public string? Filter { get; set; }
}

public class BatchStockBalanceDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public List<BatchWarehouseBalanceDto> WarehouseBalances { get; set; } = new();
}

public class BatchWarehouseBalanceDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal StockValue { get; set; }
    public decimal ValuationRate { get; set; }
}

public class BatchMovementHistoryDto
{
    public Guid BatchId { get; set; }
    public string BatchNo { get; set; } = null!;
    public List<BatchMovementEntryDto> Entries { get; set; } = new();
}

public class BatchMovementEntryDto
{
    public Guid Id { get; set; }
    public DateTime PostingDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal QuantityChange { get; set; }
    public decimal ValuationRate { get; set; }
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public bool IsInward { get; set; }
}
