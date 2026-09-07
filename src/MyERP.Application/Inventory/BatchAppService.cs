using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using MyERP.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.Items.Default)]
public class BatchAppService : ApplicationService, IBatchAppService
{
    private readonly IRepository<Batch, Guid> _repository;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<Warehouse, Guid> _warehouseRepository;
    private readonly IStockEntryAppService _stockEntryAppService;

    public BatchAppService(
        IRepository<Batch, Guid> repository,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<Warehouse, Guid> warehouseRepository,
        IStockEntryAppService stockEntryAppService)
    {
        _repository = repository;
        _sleRepository = sleRepository;
        _warehouseRepository = warehouseRepository;
        _stockEntryAppService = stockEntryAppService;
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
        var batch = new Batch(Guid.NewGuid(), input.ItemId, input.BatchNo, tenantId: null)
        {
            ManufacturingDate = input.ManufacturingDate,
            ExpiryDate = input.ExpiryDate,
            ShelfLifeInDays = input.ShelfLifeInDays,
            SupplierBatchNo = input.SupplierBatchNo,
            AllowNegativeStock = input.AllowNegativeStock,
            Description = input.Description,
        };

        if (batch.ManufacturingDate.HasValue && batch.ShelfLifeInDays.HasValue && !batch.ExpiryDate.HasValue)
            batch.SetExpiryFromShelfLife();

        // Per ERPNext commits 65ba79bb85 and cc171d9706:
        // Batchwise valuation is ALLOWED for Moving Average items, UNLESS StockSettings.DoNotUseBatchwiseValuation is enabled.
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var item = await itemRepo.FindAsync(input.ItemId);
        if (item != null)
        {
            var doNotUseBatchwiseValuation = await SettingProvider.IsTrueAsync(MyERPSettings.Stock.DoNotUseBatchwiseValuation);
            batch.EvaluateBatchwiseValuation(item.ValuationMethod, doNotUseBatchwiseValuation);
        }

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
    /// <summary>
    /// Traces which customers received a specific batch via delivery notes/sales invoices.
    /// Critical for product recalls and food safety compliance (Malaysia HACCP/GMP).
    /// Per ERPNext serial_batch_traceability report: traces batch → DN → Customer.
    /// </summary>
    public async Task<BatchTraceabilityDto> GetTraceabilityAsync(Guid batchId)
    {
        var batch = await _repository.GetAsync(batchId);

        var sleQuery = await _sleRepository.GetQueryableAsync();

        // Find all outward movements for this batch (deliveries to customers)
        var outwardEntries = sleQuery
            .Where(s => s.BatchId == batchId && !s.IsCancelled && s.QuantityChange < 0)
            .OrderByDescending(s => s.PostingDateTime)
            .ToList();

        var dnRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<DeliveryNote, Guid>>();
        var customerRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.Sales.Entities.Customer, Guid>>();

        // Resolve delivery notes and customers
        var dnIds = outwardEntries
            .Where(e => e.VoucherType == "DeliveryNote" && e.VoucherId.HasValue)
            .Select(e => e.VoucherId!.Value)
            .Distinct().ToList();
        var dns = dnIds.Count > 0
            ? (await dnRepo.GetQueryableAsync())
                .Where(d => dnIds.Contains(d.Id))
                .Select(d => new { d.Id, d.CustomerId, d.DeliveryNumber, d.PostingDate })
                .ToList()
            : new();

        var customerIds = dns.Select(d => d.CustomerId).Distinct().ToList();
        var customerNames = customerIds.Count > 0
            ? (await customerRepo.GetQueryableAsync())
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToList()
                .ToDictionary(c => c.Id, c => c.Name)
            : new Dictionary<Guid, string>();

        var dnMap = dns.ToDictionary(d => d.Id);

        var deliveries = outwardEntries
            .Where(e => e.VoucherType == "DeliveryNote" && e.VoucherId.HasValue && dnMap.ContainsKey(e.VoucherId!.Value))
            .Select(e =>
            {
                var dn = dnMap[e.VoucherId!.Value];
                return new BatchDeliveryTraceDto
                {
                    DeliveryNoteId = dn.Id,
                    DeliveryNumber = dn.DeliveryNumber,
                    DeliveryDate = dn.PostingDate,
                    CustomerId = dn.CustomerId,
                    CustomerName = customerNames.GetValueOrDefault(dn.CustomerId, "Unknown"),
                    QuantityDelivered = Math.Abs(e.QuantityChange),
                    WarehouseId = e.WarehouseId,
                };
            })
            .ToList();

        // Group by customer for summary
        var customerSummary = deliveries
            .GroupBy(d => d.CustomerId)
            .Select(g => new BatchCustomerSummaryDto
            {
                CustomerId = g.Key,
                CustomerName = g.First().CustomerName,
                TotalQuantity = g.Sum(d => d.QuantityDelivered),
                DeliveryCount = g.Count(),
                FirstDeliveryDate = g.Min(d => d.DeliveryDate),
                LastDeliveryDate = g.Max(d => d.DeliveryDate),
            })
            .OrderByDescending(c => c.TotalQuantity)
            .ToList();

        return new BatchTraceabilityDto
        {
            BatchId = batchId,
            BatchNo = batch.BatchNo,
            ItemId = batch.ItemId,
            ManufacturingDate = batch.ManufacturingDate,
            ExpiryDate = batch.ExpiryDate,
            TotalProduced = outwardEntries.Count > 0
                ? sleQuery.Where(s => s.BatchId == batchId && !s.IsCancelled && s.QuantityChange > 0).Sum(s => s.QuantityChange)
                : 0,
            TotalDelivered = deliveries.Sum(d => d.QuantityDelivered),
            CustomerCount = customerSummary.Count,
            Deliveries = deliveries,
            CustomerSummary = customerSummary,
        };
    }

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

    /// <summary>
    /// Splits an existing batch by generating a new Batch entity and a Repack Stock Entry
    /// to transfer the split quantity at the specified warehouse (Gotcha #5992).
    /// </summary>
    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<SplitBatchResultDto> SplitBatchAsync(SplitBatchDto input)
    {
        if (input.SplitQuantity <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Split quantity must be greater than zero.");
        }

        var sourceBatch = await _repository.GetAsync(input.SourceBatchId);
        if (sourceBatch.IsDisabled || sourceBatch.IsCancelled)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Source batch {sourceBatch.BatchNo} is {(sourceBatch.IsCancelled ? "cancelled" : "disabled")}.");
        }

        var warehouse = await _warehouseRepository.GetAsync(input.WarehouseId);

        // Verify available stock in the specified warehouse
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var availableQty = sleQuery
            .Where(s => s.BatchId == input.SourceBatchId && s.WarehouseId == input.WarehouseId && !s.IsCancelled)
            .Sum(s => (decimal?)s.QuantityChange) ?? 0m;

        if (availableQty < input.SplitQuantity)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("available", availableQty)
                .WithData("required", input.SplitQuantity);
        }

        // Check if target batch number already exists for this item
        var batchQuery = await _repository.GetQueryableAsync();
        var batchExists = batchQuery.Any(b => b.ItemId == sourceBatch.ItemId && b.BatchNo == input.NewBatchNo);
        if (batchExists)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("batchNo", input.NewBatchNo);
        }

        // Create new batch inheriting source attributes
        var newBatch = new Batch(Guid.NewGuid(), sourceBatch.ItemId, input.NewBatchNo, tenantId: null)
        {
            ManufacturingDate = sourceBatch.ManufacturingDate,
            ExpiryDate = sourceBatch.ExpiryDate,
            ShelfLifeInDays = sourceBatch.ShelfLifeInDays,
            SupplierBatchNo = sourceBatch.SupplierBatchNo,
            Description = input.Description ?? $"Split from {sourceBatch.BatchNo}",
        };
        await _repository.InsertAsync(newBatch, autoSave: true);

        // Generate Repack Stock Entry
        var createEntryDto = new CreateStockEntryDto
        {
            CompanyId = warehouse.CompanyId,
            EntryType = StockEntryType.Repack,
            PostingDate = DateTime.UtcNow,
            Notes = $"Split batch {sourceBatch.BatchNo} into {newBatch.BatchNo}",
            Items = new List<CreateStockEntryItemDto>
            {
                new()
                {
                    ItemId = sourceBatch.ItemId,
                    SourceWarehouseId = input.WarehouseId,
                    Quantity = input.SplitQuantity,
                    IsFinishedItem = false,
                    BatchId = sourceBatch.Id,
                },
                new()
                {
                    ItemId = sourceBatch.ItemId,
                    TargetWarehouseId = input.WarehouseId,
                    Quantity = input.SplitQuantity,
                    IsFinishedItem = true,
                    BatchId = newBatch.Id,
                }
            }
        };

        var createdEntry = await _stockEntryAppService.CreateAsync(createEntryDto);
        await _stockEntryAppService.SubmitAsync(createdEntry.Id);
        await _stockEntryAppService.PostAsync(createdEntry.Id);

        return new SplitBatchResultDto
        {
            NewBatchId = newBatch.Id,
            NewBatchNo = newBatch.BatchNo,
            StockEntryId = createdEntry.Id,
            StockEntryNumber = createdEntry.EntryNumber,
        };
    }

    /// <summary>
    /// Moves batch stock from source warehouse to target warehouse via Material Transfer Stock Entry (Gotcha #5992).
    /// </summary>
    [Authorize(MyERPPermissions.Items.Edit)]
    public async Task<MoveBatchResultDto> MoveBatchAsync(MoveBatchDto input)
    {
        if (input.Quantity <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Move quantity must be greater than zero.");
        }

        if (input.SourceWarehouseId == input.TargetWarehouseId)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Source and target warehouses cannot be identical.");
        }

        var batch = await _repository.GetAsync(input.BatchId);
        if (batch.IsDisabled || batch.IsCancelled)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Batch {batch.BatchNo} is {(batch.IsCancelled ? "cancelled" : "disabled")}.");
        }

        var sourceWarehouse = await _warehouseRepository.GetAsync(input.SourceWarehouseId);
        var targetWarehouse = await _warehouseRepository.GetAsync(input.TargetWarehouseId);

        if (sourceWarehouse.CompanyId != targetWarehouse.CompanyId)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Source and target warehouses must belong to the same company.");
        }

        // Verify available stock in source warehouse
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var availableQty = sleQuery
            .Where(s => s.BatchId == input.BatchId && s.WarehouseId == input.SourceWarehouseId && !s.IsCancelled)
            .Sum(s => (decimal?)s.QuantityChange) ?? 0m;

        if (availableQty < input.Quantity)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InsufficientStock)
                .WithData("available", availableQty)
                .WithData("required", input.Quantity);
        }

        // Generate Material Transfer Stock Entry
        var createEntryDto = new CreateStockEntryDto
        {
            CompanyId = sourceWarehouse.CompanyId,
            EntryType = StockEntryType.MaterialTransfer,
            PostingDate = DateTime.UtcNow,
            Notes = input.Description ?? $"Move batch {batch.BatchNo} from {sourceWarehouse.Name} to {targetWarehouse.Name}",
            Items = new List<CreateStockEntryItemDto>
            {
                new()
                {
                    ItemId = batch.ItemId,
                    SourceWarehouseId = input.SourceWarehouseId,
                    TargetWarehouseId = input.TargetWarehouseId,
                    Quantity = input.Quantity,
                    BatchId = batch.Id,
                }
            }
        };

        var createdEntry = await _stockEntryAppService.CreateAsync(createEntryDto);
        await _stockEntryAppService.SubmitAsync(createdEntry.Id);
        await _stockEntryAppService.PostAsync(createdEntry.Id);

        return new MoveBatchResultDto
        {
            BatchId = batch.Id,
            StockEntryId = createdEntry.Id,
            StockEntryNumber = createdEntry.EntryNumber,
        };
    }

    /// <summary>
    /// Returns available batch stock filtered by company, item, and warehouse.
    /// Per ERPNext PR #58065 / #57995 (available_batch_report): filters strictly by company to prevent cross-company leakage.
    /// </summary>
    public async Task<List<AvailableBatchItemDto>> GetAvailableBatchesAsync(GetAvailableBatchesDto input)
    {
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var warehouseQuery = await _warehouseRepository.GetQueryableAsync();
        var batchQuery = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
        {
            var companyWarehouseIds = warehouseQuery
                .Where(w => w.CompanyId == input.CompanyId.Value)
                .Select(w => w.Id)
                .ToList();
            sleQuery = sleQuery.Where(s => s.BatchId.HasValue && companyWarehouseIds.Contains(s.WarehouseId) && !s.IsCancelled);
        }
        else
        {
            sleQuery = sleQuery.Where(s => s.BatchId.HasValue && !s.IsCancelled);
        }

        if (input.WarehouseId.HasValue)
            sleQuery = sleQuery.Where(s => s.WarehouseId == input.WarehouseId.Value);

        if (input.ItemId.HasValue)
            sleQuery = sleQuery.Where(s => s.ItemId == input.ItemId.Value);

        var batchBalances = sleQuery
            .GroupBy(s => new { s.BatchId, s.WarehouseId, s.ItemId })
            .Select(g => new { g.Key.BatchId, g.Key.WarehouseId, g.Key.ItemId, Qty = g.Sum(s => s.QuantityChange) })
            .Where(g => g.Qty > 0)
            .ToList();

        if (!batchBalances.Any()) return new List<AvailableBatchItemDto>();

        var batchIds = batchBalances.Select(b => b.BatchId!.Value).Distinct().ToList();
        var batches = batchQuery.Where(b => batchIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BatchNo, b.ExpiryDate, b.IsDisabled, b.IsCancelled })
            .Where(b => !b.IsDisabled && !b.IsCancelled)
            .ToDictionary(b => b.Id);

        var warehouseIds = batchBalances.Select(b => b.WarehouseId).Distinct().ToList();
        var warehouses = warehouseQuery.Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionary(w => w.Id, w => w.Name);

        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var itemIds = batchBalances.Select(b => b.ItemId).Distinct().ToList();
        var itemQuery = await itemRepo.GetQueryableAsync();
        var items = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemName })
            .ToDictionary(i => i.Id, i => i.ItemName);

        var today = DateTime.UtcNow.Date;
        var list = batchBalances
            .Where(b => batches.ContainsKey(b.BatchId!.Value))
            .Select(b =>
            {
                var batch = batches[b.BatchId!.Value];
                return new AvailableBatchItemDto
                {
                    BatchId = b.BatchId!.Value,
                    BatchNo = batch.BatchNo,
                    ItemId = b.ItemId,
                    ItemName = items.GetValueOrDefault(b.ItemId),
                    WarehouseId = b.WarehouseId,
                    WarehouseName = warehouses.GetValueOrDefault(b.WarehouseId, "Unknown"),
                    AvailableQuantity = b.Qty,
                    ExpiryDate = batch.ExpiryDate,
                    IsExpired = batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < today,
                };
            })
            .ToList();

        // Per ERPNext commit 199cae9496:
        // Subtract stock qty of same-document rows from batch availability.
        if (input.SameDocumentBatchQuantities != null && input.SameDocumentBatchQuantities.Count > 0)
        {
            var sameDocGrouped = input.SameDocumentBatchQuantities
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.StockQty));

            foreach (var item in list)
            {
                if (sameDocGrouped.TryGetValue(item.BatchId, out var consumedQty))
                {
                    item.AvailableQuantity -= consumedQty;
                }
            }

            list = list.Where(item => item.AvailableQuantity > 0).ToList();
        }

        return list
            .OrderBy(b => b.ExpiryDate == null)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNo)
            .ToList();
    }

    /// <summary>
    /// Returns the first batch in FIFO/expiry order that can cover the full required quantity.
    /// Per ERPNext PR #58668 / commit 9261c9b47f:
    /// Assign batch_no only when the first batch covers the full qty. If no single batch covers the qty,
    /// return null so the system doesn't bind a partial batch that later causes negative stock errors.
    /// Also per commit 199cae9496: subtract stock qty of same-document rows.
    /// </summary>
    public async Task<AvailableBatchItemDto?> GetBatchCoveringQuantityAsync(AutoPickBatchDto input)
    {
        if (input.RequiredStockQty <= 0)
            return null;

        var availableBatches = await GetAvailableBatchesAsync(new GetAvailableBatchesDto
        {
            CompanyId = input.CompanyId,
            ItemId = input.ItemId,
            WarehouseId = input.WarehouseId,
            SameDocumentBatchQuantities = input.SameDocumentBatchQuantities,
        });

        // Exclude expired batches
        var validBatches = availableBatches.Where(b => !b.IsExpired).ToList();

        var firstBatch = validBatches.FirstOrDefault();
        if (firstBatch != null && firstBatch.AvailableQuantity >= input.RequiredStockQty)
        {
            return firstBatch;
        }

        return null;
    }

    /// <summary>
    /// Returns the hierarchical tree of batches split from parent batches.
    /// Per ERPNext PR #58530 / commit 0223223385 (Batch Split Tree report).
    /// </summary>
    public async Task<List<BatchSplitTreeNodeDto>> GetBatchSplitTreeAsync(GetBatchSplitTreeDto input)
    {
        var batchQuery = (await _repository.GetQueryableAsync()).Where(b => !b.IsCancelled);

        List<Batch> rootBatches;
        if (input.BatchId.HasValue)
        {
            var singleBatch = await _repository.FindAsync(input.BatchId.Value);
            if (singleBatch == null || singleBatch.IsCancelled) return new List<BatchSplitTreeNodeDto>();
            rootBatches = new List<Batch> { singleBatch };
        }
        else
        {
            // Roots are batches with no ParentBatchId that have children
            var parentBatchIdsWithChildren = batchQuery
                .Where(b => b.ParentBatchId.HasValue)
                .Select(b => b.ParentBatchId!.Value)
                .Distinct()
                .ToList();

            var rootsQuery = batchQuery.Where(b => !b.ParentBatchId.HasValue && parentBatchIdsWithChildren.Contains(b.Id));
            if (input.ItemId.HasValue)
            {
                rootsQuery = rootsQuery.Where(b => b.ItemId == input.ItemId.Value);
            }

            rootBatches = rootsQuery.OrderBy(b => b.BatchNo).ToList();
        }

        if (!rootBatches.Any()) return new List<BatchSplitTreeNodeDto>();

        // Load all descendants recursively
        var allBatches = new Dictionary<Guid, Batch>();
        foreach (var root in rootBatches)
        {
            allBatches[root.Id] = root;
        }

        var currentParentIds = rootBatches.Select(b => b.Id).ToList();
        while (currentParentIds.Count > 0)
        {
            var children = batchQuery
                .Where(b => b.ParentBatchId.HasValue && currentParentIds.Contains(b.ParentBatchId.Value))
                .ToList();

            currentParentIds.Clear();
            foreach (var child in children)
            {
                if (allBatches.TryAdd(child.Id, child))
                {
                    currentParentIds.Add(child.Id);
                }
            }
        }

        var batchIds = allBatches.Keys.ToList();

        // Load item details (ItemCode, ItemName, StockUom)
        var itemIds = allBatches.Values.Select(b => b.ItemId).Distinct().ToList();
        var items = new Dictionary<Guid, (string ItemCode, string? ItemName, string? Uom)>();
        var itemRepo = LazyServiceProvider?.LazyGetService<IRepository<Item, Guid>>();
        if (itemRepo != null)
        {
            var itemQuery = await itemRepo.GetQueryableAsync();
            items = itemQuery
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.ItemCode, i.ItemName, i.Uom })
                .ToDictionary(i => i.Id, i => (i.ItemCode, (string?)i.ItemName, (string?)i.Uom));
        }

        // Load SLE stock balances
        var sleQuery = await _sleRepository.GetQueryableAsync();
        var balances = sleQuery
            .Where(s => s.BatchId.HasValue && batchIds.Contains(s.BatchId.Value) && !s.IsCancelled)
            .GroupBy(s => s.BatchId!.Value)
            .Select(g => new { BatchId = g.Key, Qty = g.Sum(s => s.QuantityChange) })
            .ToDictionary(x => x.BatchId, x => x.Qty);

        // Load reference document names (StockEntry, JobCard)
        var stockEntryIds = allBatches.Values
            .Where(b => b.ReferenceDocType == "StockEntry" && b.ReferenceDocId.HasValue)
            .Select(b => b.ReferenceDocId!.Value)
            .Distinct()
            .ToList();

        var stockEntryNames = new Dictionary<Guid, string?>();
        if (stockEntryIds.Count > 0)
        {
            var seRepo = LazyServiceProvider?.LazyGetService<IRepository<StockEntry, Guid>>();
            if (seRepo != null)
            {
                var seQuery = await seRepo.GetQueryableAsync();
                stockEntryNames = seQuery
                    .Where(se => stockEntryIds.Contains(se.Id))
                    .Select(se => new { se.Id, se.EntryNumber })
                    .ToDictionary(se => se.Id, se => (string?)se.EntryNumber);
            }
        }

        var jobCardIds = allBatches.Values
            .Where(b => b.ReferenceDocType == "JobCard" && b.ReferenceDocId.HasValue)
            .Select(b => b.ReferenceDocId!.Value)
            .Distinct()
            .ToList();

        var jobCardNames = new Dictionary<Guid, string?>();
        if (jobCardIds.Count > 0)
        {
            var jcRepo = LazyServiceProvider?.LazyGetService<IRepository<Manufacturing.Entities.JobCard, Guid>>();
            if (jcRepo != null)
            {
                var jcQuery = await jcRepo.GetQueryableAsync();
                jobCardNames = jcQuery
                    .Where(jc => jobCardIds.Contains(jc.Id))
                    .Select(jc => new { jc.Id, CardNumber = jc.Id.ToString() })
                    .ToDictionary(jc => jc.Id, jc => (string?)jc.CardNumber);
            }
        }

        // Group children by ParentBatchId
        var childrenByParent = allBatches.Values
            .Where(b => b.ParentBatchId.HasValue)
            .GroupBy(b => b.ParentBatchId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.CreationTime).ToList());

        BatchSplitTreeNodeDto BuildNode(Batch batch, int indent)
        {
            items.TryGetValue(batch.ItemId, out var itemInfo);
            var refName = batch.ReferenceDocType switch
            {
                "StockEntry" when batch.ReferenceDocId.HasValue => stockEntryNames.GetValueOrDefault(batch.ReferenceDocId.Value),
                "JobCard" when batch.ReferenceDocId.HasValue => jobCardNames.GetValueOrDefault(batch.ReferenceDocId.Value),
                _ => null
            };

            var node = new BatchSplitTreeNodeDto
            {
                BatchId = batch.Id,
                BatchNo = batch.BatchNo,
                ParentBatchId = batch.ParentBatchId,
                ItemId = batch.ItemId,
                ItemCode = !string.IsNullOrEmpty(itemInfo.ItemCode) ? itemInfo.ItemCode : "Unknown",
                ItemName = itemInfo.ItemName,
                StockUom = itemInfo.Uom,
                BatchQty = balances.GetValueOrDefault(batch.Id, 0m),
                ReferenceDocType = batch.ReferenceDocType,
                ReferenceDocId = batch.ReferenceDocId,
                ReferenceName = refName,
                ManufacturingDate = batch.ManufacturingDate,
                Indent = indent,
            };

            if (childrenByParent.TryGetValue(batch.Id, out var childBatches))
            {
                foreach (var child in childBatches)
                {
                    node.Children.Add(BuildNode(child, indent + 1));
                }
            }

            return node;
        }

        var result = new List<BatchSplitTreeNodeDto>();
        foreach (var root in rootBatches)
        {
            result.Add(BuildNode(root, 0));
        }

        return result;
    }
}

