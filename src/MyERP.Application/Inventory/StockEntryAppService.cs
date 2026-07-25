using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockEntryAppService : ApplicationService, IStockEntryAppService
{
    private readonly IRepository<StockEntry, Guid> _repository;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockPostingService _stockPostingService;
    private readonly AccountingRuleEngine _ruleEngine;

    public StockEntryAppService(
        IRepository<StockEntry, Guid> repository,
        IRepository<DocumentActivityLog, Guid> activityLogRepository,
        IDocumentNumberGenerator numberGenerator,
        StockPostingService stockPostingService,
        AccountingRuleEngine ruleEngine)
    {
        _repository = repository;
        _activityLogRepository = activityLogRepository;
        _numberGenerator = numberGenerator;
        _stockPostingService = stockPostingService;
        _ruleEngine = ruleEngine;
    }

    public async Task<StockEntryDto> GetAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<StockEntry, StockEntryDto>(entry);

        // Resolve item and warehouse names
        var itemRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Item, Guid>>();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();

        var itemIds = dto.Items.Select(i => i.ItemId).Distinct().ToList();
        var whIds = dto.Items.SelectMany(i => new[] { i.SourceWarehouseId, i.TargetWarehouseId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var itemQuery = await itemRepo.GetQueryableAsync();
        var itemNames = itemQuery.Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.ItemCode, i.ItemName }).ToList()
            .ToDictionary(i => i.Id, i => $"{i.ItemCode} — {i.ItemName}");

        var whQuery = await warehouseRepo.GetQueryableAsync();
        var whNames = whQuery.Where(w => whIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name }).ToList()
            .ToDictionary(w => w.Id, w => w.Name);

        foreach (var item in dto.Items)
        {
            item.ItemName = itemNames.GetValueOrDefault(item.ItemId, item.ItemId.ToString()[..8]);
            if (item.SourceWarehouseId.HasValue)
                item.SourceWarehouseName = whNames.GetValueOrDefault(item.SourceWarehouseId.Value);
            if (item.TargetWarehouseId.HasValue)
                item.TargetWarehouseName = whNames.GetValueOrDefault(item.TargetWarehouseId.Value);
        }

        return dto;
    }

    public async Task<PagedResultDto<StockEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter;
            query = query.Where(x => x.EntryNumber != null && x.EntryNumber.Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        if (input.FromDate.HasValue)
            query = query.Where(x => x.PostingDate >= input.FromDate.Value);

        if (input.ToDate.HasValue)
            query = query.Where(x => x.PostingDate <= input.ToDate.Value);

        var totalCount = query.Count();
        var sorted = SortingHelper.ApplySorting(query, input.Sorting,
            q => q.OrderByDescending(x => x.PostingDate),
            ("entryNumber", x => (object)(x.EntryNumber ?? string.Empty)),
            ("postingDate", x => x.PostingDate),
            ("entryType", x => x.EntryType),
            ("status", x => x.Status));
        var entries = sorted
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<StockEntryDto>(
            totalCount,
            entries.Select(ObjectMapper.Map<StockEntry, StockEntryDto>).ToList());
    }

    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockEntryDto> CreateAsync(CreateStockEntryDto input)
    {
        // Validate items are not empty
        if (input.Items == null || input.Items.Count == 0)
            throw new Volo.Abp.BusinessException("MyERP:01007")
                .WithData("documentType", "Stock Entry");

        // Validate all items are active
        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        // Validate company restriction — items must allow this company
        var restrictionService = LazyServiceProvider.LazyGetRequiredService<Core.DomainServices.CompanyRestrictionValidationService>();
        await restrictionService.ValidateTransactionCompanyAsync(
            "StockEntry", input.CompanyId,
            itemIds: itemIds);

        // Create entry first so we can validate warehouses via domain manager
        var entryNumber = await _numberGenerator.GenerateAsync("StockEntry", input.CompanyId);

        var entry = new StockEntry(
            GuidGenerator.Create(),
            input.CompanyId,
            input.EntryType,
            input.PostingDate);

        entry.EntryNumber = entryNumber;
        entry.ReferenceType = input.ReferenceType;
        entry.ReferenceId = input.ReferenceId;
        entry.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            entry.AddItem(item.ItemId, item.Quantity, item.SourceWarehouseId, item.TargetWarehouseId, item.ValuationRate);
        }

        // Delegate warehouse validation to StockEntryManager (DDD pattern)
        // Per DO-NOT: same-warehouse transfers blocked, group warehouses blocked
        var seManager = LazyServiceProvider.LazyGetRequiredService<StockEntryManager>();
        await seManager.ValidateWarehousesAsync(entry);

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<StockEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Post)]
    public async Task<StockEntryDto> PostAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Post();

        // Create SLE entries + update Bin balances
        await _stockPostingService.PostStockEntryAsync(entry);

        // GL posting for perpetual inventory (stock movement accounting)
        // Uses configured accounting rules: Material Receipt → DR Stock CR Adj,
        // Material Issue → DR Expense CR Stock, Transfer → no P&L impact
        await _ruleEngine.PostDocumentAsync(entry);

        // Update Work Order material transferred qty for manufacturing transfers
        if (entry.EntryType == StockEntryType.MaterialTransferForManufacture && entry.WorkOrderId.HasValue)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var wo = await woRepo.GetAsync(entry.WorkOrderId.Value);
            var totalTransferredQty = entry.Items.Sum(i => i.Quantity);
            wo.RecordMaterialTransfer(totalTransferredQty);
            await woRepo.UpdateAsync(wo, autoSave: true);
        }

        // Update Work Order produced qty for manufacture entries
        if (entry.EntryType == StockEntryType.Manufacture && entry.WorkOrderId.HasValue)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var wo = await woRepo.GetAsync(entry.WorkOrderId.Value);
            // FG qty = sum of items going to target warehouse (finished goods)
            var fgQty = entry.Items
                .Where(i => i.TargetWarehouseId.HasValue && !i.SourceWarehouseId.HasValue)
                .Sum(i => i.Quantity);
            if (fgQty > 0)
            {
                wo.RecordProduction(fgQty);
                await woRepo.UpdateAsync(wo, autoSave: true);
            }
        }

        // Auto-reorder check for stock-out entries (Issue, Transfer source)
        if (entry.EntryType == StockEntryType.MaterialIssue
            || entry.EntryType == StockEntryType.MaterialTransfer
            || entry.EntryType == StockEntryType.MaterialTransferForManufacture)
        {
            var autoReorder = LazyServiceProvider.LazyGetRequiredService<DomainServices.AutoReorderService>();
            foreach (var item in entry.Items.Where(i => i.SourceWarehouseId.HasValue))
            {
                await autoReorder.CheckSingleItemAsync(
                    item.ItemId, item.SourceWarehouseId!.Value, entry.CompanyId, entry.TenantId);
            }
        }

        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "StockEntry", entry.Id, "Posted",
            entry.CompanyId, entry.EntryNumber, "Submitted", "Posted",
            CurrentUser.Id, tenantId: entry.TenantId));

        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<StockEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Validate posting period is not frozen/closed before reversing
        var postingOrchestrator = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestrator.ValidatePostingPeriodAsync(entry.CompanyId, entry.PostingDate, "StockEntry");

        entry.Cancel();

        // Reverse SLE entries + Bin balances
        await _stockPostingService.ReverseStockEntryAsync(entry);

        // Reverse GL entries (per ERPNext: stock entries have perpetual inventory GL)
        await postingOrchestrator.ReversePleForDocumentAsync("StockEntry", entry.Id);

        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "StockEntry", entry.Id, "Cancelled",
            entry.CompanyId, entry.EntryNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: entry.TenantId));

        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    /// <summary>
    /// Returns pre-populated items for a Manufacture stock entry based on a Work Order's BOM.
    /// Calculates proportional material quantities for the desired production qty.
    /// RM items get SourceWarehouseId, FG item gets TargetWarehouseId.
    /// </summary>
    public async Task<ManufactureItemsDto> GetManufactureItemsAsync(Guid workOrderId, decimal produceQty)
    {
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BillOfMaterials, Guid>>();

        var wo = await woRepo.GetAsync(workOrderId, includeDetails: true);
        if (wo.BomId == Guid.Empty)
            throw new Volo.Abp.BusinessException("MyERP:10003")
                .WithData("reason", "Work Order has no linked BOM");

        var bom = await bomRepo.GetAsync(wo.BomId, includeDetails: true);
        var multiplier = produceQty / (bom.Quantity > 0 ? bom.Quantity : 1m);

        var result = new ManufactureItemsDto
        {
            WorkOrderId = wo.Id,
            BomId = bom.Id,
            ProduceQty = produceQty,
            FgItemId = bom.ItemId,
            FgWarehouseId = wo.FgWarehouseId ?? bom.TargetWarehouseId,
            SourceWarehouseId = wo.SourceWarehouseId ?? bom.SourceWarehouseId,
        };

        // Raw material consumption items
        foreach (var bomItem in bom.Items.Where(i => !i.IsPhantom))
        {
            result.Items.Add(new ManufactureItemLineDto
            {
                ItemId = bomItem.ItemId,
                ItemName = bomItem.ItemName,
                RequiredQty = Math.Round(bomItem.Quantity * multiplier, 4),
                Rate = bomItem.Rate,
                SourceWarehouseId = bomItem.SourceWarehouseId ?? result.SourceWarehouseId,
                IsRawMaterial = true,
            });
        }

        // Finished good item
        result.Items.Add(new ManufactureItemLineDto
        {
            ItemId = bom.ItemId,
            ItemName = $"FG: {bom.BomNumber}",
            RequiredQty = produceQty,
            Rate = bom.TotalCost / (bom.Quantity > 0 ? bom.Quantity : 1m),
            TargetWarehouseId = result.FgWarehouseId,
            IsRawMaterial = false,
        });

        return result;
    }

    /// <summary>
    /// Auto-creates a MaterialTransferForManufacture Stock Entry from a Work Order.
    /// Transfers raw materials from source warehouse to WIP warehouse.
    /// Per ERPNext WO→SE "Material Transfer" button behavior.
    /// Only transfers materials not yet transferred (requiredQty - transferredQty per item).
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockEntryDto> CreateMaterialTransferForManufactureAsync(Guid workOrderId)
    {
        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var bomRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<BillOfMaterials, Guid>>();
        var numberGenerator = LazyServiceProvider.LazyGetRequiredService<IDocumentNumberGenerator>();

        var wo = await woRepo.GetAsync(workOrderId, includeDetails: true);

        if (wo.Status == Manufacturing.WorkOrderStatus.Draft || wo.Status == Manufacturing.WorkOrderStatus.Cancelled)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Work Order must be Submitted or In Process to transfer materials");

        var bom = await bomRepo.GetAsync(wo.BomId, includeDetails: true);
        var multiplier = wo.Quantity / (bom.Quantity > 0 ? bom.Quantity : 1m);

        var sourceWarehouseId = wo.SourceWarehouseId ?? bom.SourceWarehouseId;
        var wipWarehouseId = wo.WipWarehouseId;

        if (!sourceWarehouseId.HasValue)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("field", "SourceWarehouse");

        if (!wipWarehouseId.HasValue)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.MissingWarehouse)
                .WithData("field", "WIPWarehouse");

        // Create the transfer Stock Entry
        var entry = new StockEntry(
            Guid.NewGuid(), wo.CompanyId,
            StockEntryType.MaterialTransferForManufacture,
            DateTime.UtcNow.Date, CurrentTenant.Id);

        entry.WorkOrderId = wo.Id;
        entry.EntryNumber = await numberGenerator.GenerateAsync("SE", wo.CompanyId);
        entry.Notes = $"Material Transfer for Work Order {wo.WorkOrderNumber ?? wo.Id.ToString()}";

        // Add raw materials (only pending qty not yet transferred)
        foreach (var woItem in wo.RequiredItems)
        {
            var pendingQty = woItem.RequiredQuantity - woItem.TransferredQuantity;
            if (pendingQty <= 0) continue;

            // Find BOM item for valuation rate
            var bomItem = bom.Items.FirstOrDefault(b => b.ItemId == woItem.ItemId);
            var rate = bomItem?.Rate ?? 0m;

            // Per ERPNext: source from WO item-specific warehouse → BOM item → WO default → BOM default
            var itemSourceWarehouse = woItem.SourceWarehouseId ?? sourceWarehouseId.Value;

            entry.AddItem(
                itemId: woItem.ItemId,
                quantity: pendingQty,
                sourceWarehouseId: itemSourceWarehouse,
                targetWarehouseId: wipWarehouseId.Value,
                valuationRate: rate);
        }

        if (!entry.Items.Any())
            throw new Volo.Abp.BusinessException("MyERP:10013")
                .WithData("reason", "All materials have already been transferred for this Work Order");

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Edit)]
    public async Task<StockEntryDto> UpdateAsync(Guid id, CreateStockEntryDto input)
    {
        var entry = await _repository.GetAsync(id);
        if (entry.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft stock entries can be edited");

        entry.EntryType = input.EntryType;
        entry.PostingDate = input.PostingDate;
        entry.Notes = input.Notes;
        entry.ReferenceType = input.ReferenceType;
        entry.ReferenceId = input.ReferenceId;

        // Replace items
        entry.ClearItems();
        foreach (var item in input.Items)
            entry.AddItem(item.ItemId, item.Quantity, item.SourceWarehouseId, item.TargetWarehouseId, item.ValuationRate);

        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        if (entry.Status != Core.DocumentStatus.Draft)
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("detail", "Only Draft stock entries can be deleted");
        await _repository.DeleteAsync(id);
    }

    // ─── Transit Transfer Operations ────────────────────────────────────────

    /// <summary>
    /// Creates a transit transfer (2-step): Source → Transit warehouse.
    /// The receiving leg is created separately after this entry is posted.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockEntryDto> CreateTransitTransferAsync(CreateTransitTransferDto input)
    {
        var transitService = LazyServiceProvider.LazyGetRequiredService<TransitTransferService>();

        var items = input.Items.Select(i => new TransitTransferItem(
            i.ItemId, i.Quantity, i.ValuationRate)).ToArray();

        var entry = await transitService.CreateSendToWarehouseAsync(
            input.CompanyId,
            input.SourceWarehouseId,
            input.DestinationWarehouseId,
            items,
            input.PostingDate,
            input.Notes,
            CurrentTenant.Id);

        entry.EntryNumber = await _numberGenerator.GenerateAsync("StockEntry", entry.CompanyId);

        await _repository.InsertAsync(entry);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    /// <summary>
    /// Creates the receiving leg of a transit transfer: Transit → Destination warehouse.
    /// Requires the outgoing entry to be Posted first.
    /// </summary>
    [Authorize(MyERPPermissions.StockEntries.Create)]
    public async Task<StockEntryDto> CreateReceiveAtWarehouseAsync(
        Guid outgoingStockEntryId, Guid destinationWarehouseId, DateTime postingDate)
    {
        var transitService = LazyServiceProvider.LazyGetRequiredService<TransitTransferService>();

        var entry = await transitService.CreateReceiveAtWarehouseAsync(
            outgoingStockEntryId, destinationWarehouseId, postingDate);

        entry.EntryNumber = await _numberGenerator.GenerateAsync("StockEntry", entry.CompanyId);

        await _repository.InsertAsync(entry);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    /// <summary>
    /// Gets pending transit transfers (sent but not yet received) for a company.
    /// </summary>
    public async Task<PendingTransitTransferDto[]> GetPendingTransitTransfersAsync(Guid companyId)
    {
        var transitService = LazyServiceProvider.LazyGetRequiredService<TransitTransferService>();
        var warehouseRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Warehouse, Guid>>();

        var pending = await transitService.GetPendingTransfersAsync(companyId);

        // Resolve warehouse names
        var warehouseIds = pending.Select(p => p.SourceWarehouseId).Distinct().ToList();
        var wQuery = await warehouseRepo.GetQueryableAsync();
        var warehouseNames = wQuery
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionary(w => w.Id, w => w.Name);

        return pending.Select(p => new PendingTransitTransferDto
        {
            StockEntryId = p.StockEntryId,
            EntryNumber = p.EntryNumber,
            PostingDate = p.PostingDate,
            SourceWarehouseId = p.SourceWarehouseId,
            SourceWarehouseName = warehouseNames.GetValueOrDefault(p.SourceWarehouseId),
            TotalQuantity = p.TotalQuantity,
            ItemCount = p.ItemCount
        }).ToArray();
    }

}

