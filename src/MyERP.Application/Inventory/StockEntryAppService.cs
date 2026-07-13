using System;
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
        return MapToDto(entry);
    }

    public async Task<PagedResultDto<StockEntryDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x => x.EntryNumber != null && x.EntryNumber.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(input.Status) && Enum.TryParse<Core.DocumentStatus>(input.Status, true, out var status))
            query = query.Where(x => x.Status == status);

        var totalCount = query.Count();
        var entries = query
            .OrderByDescending(x => x.PostingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<StockEntryDto>(
            totalCount,
            entries.Select(MapToDto).ToList());
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

        // Validate warehouse requirements based on entry type
        var requiresSource = input.EntryType == StockEntryType.MaterialIssue
            || input.EntryType == StockEntryType.MaterialTransfer
            || input.EntryType == StockEntryType.MaterialTransferForManufacture
            || input.EntryType == StockEntryType.SendToWarehouse;
        var requiresTarget = input.EntryType == StockEntryType.MaterialReceipt
            || input.EntryType == StockEntryType.MaterialTransfer
            || input.EntryType == StockEntryType.MaterialTransferForManufacture
            || input.EntryType == StockEntryType.ReceiveAtWarehouse
            || input.EntryType == StockEntryType.Manufacture;

        foreach (var item in input.Items)
        {
            if (requiresSource && !item.SourceWarehouseId.HasValue)
                throw new Volo.Abp.BusinessException("MyERP:05015")
                    .WithData("entryType", input.EntryType.ToString())
                    .WithData("field", "SourceWarehouse");
            if (requiresTarget && !item.TargetWarehouseId.HasValue)
                throw new Volo.Abp.BusinessException("MyERP:05015")
                    .WithData("entryType", input.EntryType.ToString())
                    .WithData("field", "TargetWarehouse");
            // Same-warehouse transfer not allowed (per DO-NOT)
            if (item.SourceWarehouseId.HasValue && item.TargetWarehouseId.HasValue
                && item.SourceWarehouseId == item.TargetWarehouseId)
                throw new Volo.Abp.BusinessException("MyERP:05016")
                    .WithData("warehouse", item.SourceWarehouseId.Value.ToString());
        }

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

        await _repository.InsertAsync(entry, autoSave: true);
        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<StockEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Submit();
        await _repository.UpdateAsync(entry, autoSave: true);
        return MapToDto(entry);
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

        return MapToDto(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Cancel)]
    public async Task<StockEntryDto> CancelAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Cancel();

        // Reverse SLE entries + Bin balances
        await _stockPostingService.ReverseStockEntryAsync(entry);

        await _repository.UpdateAsync(entry, autoSave: true);

        // Audit trail
        await _activityLogRepository.InsertAsync(new DocumentActivityLog(
            GuidGenerator.Create(), "StockEntry", entry.Id, "Cancelled",
            entry.CompanyId, entry.EntryNumber, "Posted", "Cancelled",
            CurrentUser.Id, tenantId: entry.TenantId));

        return MapToDto(entry);
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

    private StockEntryDto MapToDto(StockEntry entry)
    {
        return new StockEntryDto
        {
            Id = entry.Id,
            CompanyId = entry.CompanyId,
            EntryNumber = entry.EntryNumber,
            EntryType = entry.EntryType,
            PostingDate = entry.PostingDate,
            ReferenceType = entry.ReferenceType,
            ReferenceId = entry.ReferenceId,
            Notes = entry.Notes,
            Status = entry.Status.ToString(),
            CreationTime = entry.CreationTime,
            LastModificationTime = entry.LastModificationTime,
            Items = entry.Items.Select(i => new StockEntryItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                Quantity = i.Quantity,
                SourceWarehouseId = i.SourceWarehouseId,
                TargetWarehouseId = i.TargetWarehouseId,
                ValuationRate = i.ValuationRate,
            }).ToList(),
        };
    }
}
