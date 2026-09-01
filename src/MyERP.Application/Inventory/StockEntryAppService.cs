using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using MyERP.Manufacturing.Entities;
using MyERP.Permissions;
using MyERP.Settings;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Settings;
using MyERP.Manufacturing;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.StockEntries.Default)]
public class StockEntryAppService : ApplicationService, IStockEntryAppService
{
    private readonly IRepository<StockEntry, Guid> _repository;
    private readonly IRepository<DocumentActivityLog, Guid> _activityLogRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly StockPostingService _stockPostingService;
    private readonly ISettingProvider _settingProvider;

    public StockEntryAppService(
        IRepository<StockEntry, Guid> repository,
        IRepository<DocumentActivityLog, Guid> activityLogRepository,
        IDocumentNumberGenerator numberGenerator,
        StockPostingService stockPostingService,
        ISettingProvider settingProvider)
    {
        _repository = repository;
        _activityLogRepository = activityLogRepository;
        _numberGenerator = numberGenerator;
        _stockPostingService = stockPostingService;
        _settingProvider = settingProvider;
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
        entry.WorkOrderId = input.WorkOrderId;
        entry.FgCompletedQty = input.FgCompletedQty;
        entry.ProcessLossQty = input.ProcessLossQty;
        entry.ProcessLossPercentage = input.ProcessLossPercentage;
        entry.SyncProcessLoss();
        entry.IsFgConversion = input.IsFgConversion;
        entry.WeightPerPiece = input.WeightPerPiece;
        entry.Notes = input.Notes;

        foreach (var item in input.Items)
        {
            entry.AddItem(item.ItemId, item.Quantity, item.SourceWarehouseId, item.TargetWarehouseId, item.ValuationRate, item.IsFinishedItem, item.BatchId);
        }

        // Delegate purpose-specific validation to StockEntryManager (DDD pattern)
        // Per DO-NOT: same-warehouse transfers blocked, group warehouses blocked
        var seManager = LazyServiceProvider.LazyGetRequiredService<StockEntryManager>();
        await seManager.ValidateWarehousesAsync(entry);
        // ValidateRepackItems/ValidateManufactureItems were domain-service methods with no
        // caller anywhere — a manually-authored Repack or Manufacture Stock Entry (via this
        // generic create path) skipped their purpose-specific rules entirely (Repack's
        // outgoing/incoming-item and multi-FG-manual-rate checks; Manufacture's one-unique-FG
        // rule and mandatory manufactured qty check per PR #58005).
        seManager.ValidateRepackItems(entry);
        seManager.ValidateManufactureItems(entry);
        seManager.ValidateBatchSplit(entry);

        var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
        var altRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemAlternative, Guid>>();
        await seManager.ValidateFgConversionAsync(entry, woRepo, altRepo, _repository);

        var mfgSettingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.ManufacturingSettings, Guid>>();
        var mfgSettings = await mfgSettingsRepo.FindAsync(s => s.CompanyId == entry.CompanyId);
        var overproductionPct = mfgSettings?.OverproductionPercentage ?? 5m;
        await seManager.ValidateDuplicateManufactureEntryAsync(entry, woRepo, _repository, overproductionPct);

        await _repository.InsertAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Submit)]
    public async Task<StockEntryDto> SubmitAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);

        // Per DO-NOT: "Allow excess material transfer for manufacture beyond required_qty -
        // already_transferred_qty" — applies to manually-authored SendToSubcontractor entries too,
        // not just the auto-generated ones from CreateRmTransferStockEntryAsync (which self-caps).
        if (entry.EntryType == StockEntryType.SendToSubcontractor && entry.SubcontractingOrderId.HasValue)
        {
            var rmService = LazyServiceProvider.LazyGetRequiredService<Purchasing.DomainServices.SubcontractingRmTransferService>();
            var allowancePctString = await _settingProvider.GetOrNullAsync(MyERPSettings.Buying.OverTransferAllowance);
            var allowancePct = decimal.TryParse(allowancePctString, out var pct) ? pct : 0m;

            foreach (var line in entry.Items.GroupBy(i => i.ItemId))
            {
                await rmService.ValidateTransferQuantityAsync(
                    entry.SubcontractingOrderId.Value, line.Key, line.Sum(i => i.Quantity), allowancePct);
            }
        }

        var seManager = LazyServiceProvider.LazyGetRequiredService<StockEntryManager>();
        seManager.ValidateBatchSplit(entry);

        if (entry.IsFgConversion)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var altRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<ItemAlternative, Guid>>();
            await seManager.ValidateFgConversionAsync(entry, woRepo, altRepo, _repository);
        }

        // Operations completion check (per ERPNext PR #58000 / commit 401eb30963)
        if (entry.WorkOrderId.HasValue
            && (entry.EntryType == StockEntryType.Manufacture || entry.EntryType == StockEntryType.MaterialConsumptionForManufacture))
        {
            var jcRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<JobCard, Guid>>();
            var jcQuery = await jcRepo.GetQueryableAsync();
            var jobCards = jcQuery.Where(jc => jc.WorkOrderId == entry.WorkOrderId.Value).ToList();
            if (jobCards.Any())
            {
                var uncompleted = jobCards.Where(jc => jc.Status != JobCardStatus.Completed && jc.Status != JobCardStatus.Cancelled).ToList();
                if (uncompleted.Any())
                {
                    throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"Operations are not completed for Work Order. Please complete active Job Cards before submitting manufacture entry.");
                }
            }
        }

        // Mandatory manufactured qty (PR #58005) & duplicate manufacture check (PR #58004)
        if (entry.WorkOrderId.HasValue && entry.EntryType == StockEntryType.Manufacture)
        {
            var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var wo = await woRepo.FindAsync(entry.WorkOrderId.Value);
            seManager.ValidateManufacturedQty(entry, wo);

            var mfgSettingsRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.ManufacturingSettings, Guid>>();
            var mfgSettings = await mfgSettingsRepo.FindAsync(s => s.CompanyId == entry.CompanyId);
            var overproductionPct = mfgSettings?.OverproductionPercentage ?? 5m;
            await seManager.ValidateDuplicateManufactureEntryAsync(entry, woRepo, _repository, overproductionPct);
        }

        entry.Submit();
        await _repository.UpdateAsync(entry, autoSave: true);
        return ObjectMapper.Map<StockEntry, StockEntryDto>(entry);
    }

    [Authorize(MyERPPermissions.StockEntries.Post)]
    public async Task<StockEntryDto> PostAsync(Guid id)
    {
        var entry = await _repository.GetAsync(id);
        entry.Post();

        // Batch expiry validation for outward stock paths
        // Per DO-NOT: must block expired batch consumption in transactions
        {
            var batchOutItems = entry.Items
                .Where(i => i.SourceWarehouseId.HasValue && i.BatchId.HasValue)
                .Select(i => new DomainServices.BatchValidationItem(i.ItemId, i.BatchId, null))
                .ToList();
            if (batchOutItems.Any())
            {
                var batchValidation = LazyServiceProvider.LazyGetRequiredService<DomainServices.BatchExpiryValidationService>();
                await batchValidation.ValidateForStockOutAsync(batchOutItems, entry.PostingDate);
            }
        }

        // Quality Inspection enforcement for outward stock paths
        // Per DO-NOT: Material Consumption for Manufacture is explicitly excluded
        {
            var itemIds = entry.Items.Select(i => i.ItemId).Distinct().ToArray();
            if (itemIds.Length > 0)
            {
                var qiEnforcement = LazyServiceProvider.LazyGetRequiredService<DomainServices.QualityInspectionEnforcementService>();
                await qiEnforcement.ValidateForStockEntryAsync(
                    entry.Id, itemIds, entry.EntryType.ToString(), entry.TenantId);
            }
        }

        // Create SLE entries + update Bin balances
        await _stockPostingService.PostStockEntryAsync(entry);

        // GL posting for perpetual inventory (stock movement accounting)
        // Uses configured accounting rules: Material Receipt → DR Stock CR Adj,
        // Material Issue → DR Expense CR Stock, Transfer → no P&L impact.
        // Routed through the orchestrator (not _ruleEngine directly) so the Journal Entry
        // is actually validated against period-closure and persisted — calling _ruleEngine
        // alone builds and discards it in memory, it is never written to the ledger.
        var postingOrchestratorForPost = LazyServiceProvider
            .LazyGetRequiredService<Accounting.DomainServices.DocumentPostingOrchestrator>();
        await postingOrchestratorForPost.PostStockEntryAsync(entry);

        // Update Work Order material transferred qty for manufacturing transfers
        // Per ERPNext PR #58091 / #58080: exclude corrective job card transfers from Work Order transferred qty
        if (entry.EntryType == StockEntryType.MaterialTransferForManufacture && entry.WorkOrderId.HasValue)
        {
            bool isCorrective = false;
            if (entry.JobCardId.HasValue)
            {
                var jcRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Manufacturing.Entities.JobCard, Guid>>();
                var jc = await jcRepo.FindAsync(entry.JobCardId.Value);
                isCorrective = jc?.IsCorrective ?? false;
            }

            if (!isCorrective)
            {
                var woRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
                var wo = await woRepo.GetAsync(entry.WorkOrderId.Value);
                var totalTransferredQty = entry.Items.Sum(i => i.Quantity);
                wo.RecordMaterialTransfer(totalTransferredQty);
                await woRepo.UpdateAsync(wo, autoSave: true);
            }
        }

        // Update Subcontracting Order's supplied-item TransferredQty for RM transfers.
        // Without this, SubcontractingRmTransferService.CalculateRmRequirementsAsync would keep
        // reporting the full required qty as pending forever, and ValidateTransferQuantityAsync's
        // "already transferred" figure would never move.
        if (entry.EntryType == StockEntryType.SendToSubcontractor && entry.SubcontractingOrderId.HasValue)
        {
            var rmService = LazyServiceProvider.LazyGetRequiredService<Purchasing.DomainServices.SubcontractingRmTransferService>();
            var transferLines = entry.Items
                .GroupBy(i => i.ItemId)
                .Select(g => new Purchasing.DomainServices.RmTransferLine { ItemId = g.Key, Qty = g.Sum(i => i.Quantity) });
            await rmService.RecordRmTransferAsync(entry.SubcontractingOrderId.Value, transferLines);
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
            var processLoss = entry.FgCompletedQty > fgQty ? entry.FgCompletedQty - fgQty : 0m;
            if (fgQty > 0 || processLoss > 0)
            {
                wo.RecordProduction(fgQty, processLoss: processLoss);
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

            // Low-stock alert for procurement staff, distinct from AutoReorderService above
            // (which auto-creates a Material Request) — StockAlertNotificationService had zero
            // callers anywhere despite its own doc comment saying it should fire here.
            var stockAlert = LazyServiceProvider.LazyGetRequiredService<DomainServices.StockAlertNotificationService>();
            var alertWarehouseGroups = entry.Items
                .Where(i => i.SourceWarehouseId.HasValue)
                .GroupBy(i => i.SourceWarehouseId!.Value);
            foreach (var group in alertWarehouseGroups)
            {
                await stockAlert.CheckMultipleAndNotifyAsync(
                    group.Select(i => i.ItemId), group.Key, entry.CompanyId, entry.TenantId);
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

        // Repost Guard: cannot cancel while an active valuation repost is in progress (gotcha #6183)
        var repostGuard = LazyServiceProvider.LazyGetRequiredService<Inventory.DomainServices.StockRepostGuardService>();
        await repostGuard.ValidateCanCancelVoucherAsync("StockEntry", entry.Id);

        // Guard: cannot cancel a Stock Entry linked to a Completed Work Order. Per ERPNext
        // validate_work_order_status(): the WO is done (FG received, downstream DN/SI may
        // already reference the produced items), so reversing material movements now would
        // corrupt a finished production run. This is the reverse of the "can't cancel a WO
        // with submitted Stock Entries" guard — this one gates the Stock Entry side.
        if (entry.WorkOrderId.HasValue)
        {
            var workOrderRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var workOrder = await workOrderRepo.FindAsync(entry.WorkOrderId.Value);
            if (workOrder != null && workOrder.Status == Manufacturing.WorkOrderStatus.Completed)
            {
                throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                    .WithData("detail", "Cannot cancel this transaction — the linked Work Order is Completed.");
            }
        }

        entry.Cancel();

        // Keep WorkOrder.ProducedQuantity in sync when a Manufacture entry is cancelled — it's
        // the only signal CancelWorkOrderAsync's own stock-reversal block has for "how much of
        // this WO's production is still unreversed." Without this, cancelling this Stock Entry
        // (which already reverses SLE/Bin/GL below) then cancelling the WO would reverse the
        // same units a second time via that block (round-76 fix).
        if (entry.WorkOrderId.HasValue && entry.EntryType == StockEntryType.Manufacture)
        {
            var workOrderRepoForProduction = LazyServiceProvider.LazyGetRequiredService<IRepository<WorkOrder, Guid>>();
            var producingWorkOrder = await workOrderRepoForProduction.FindAsync(entry.WorkOrderId.Value);
            if (producingWorkOrder != null)
            {
                producingWorkOrder.ProducedQuantity = Math.Max(0, producingWorkOrder.ProducedQuantity - entry.FgCompletedQty);
                await workOrderRepoForProduction.UpdateAsync(producingWorkOrder);
            }
        }

        // Reverse the Subcontracting Order's supplied-item TransferredQty (mirrors PostAsync).
        if (entry.EntryType == StockEntryType.SendToSubcontractor && entry.SubcontractingOrderId.HasValue)
        {
            var rmService = LazyServiceProvider.LazyGetRequiredService<Purchasing.DomainServices.SubcontractingRmTransferService>();
            var transferLines = entry.Items
                .GroupBy(i => i.ItemId)
                .Select(g => new Purchasing.DomainServices.RmTransferLine { ItemId = g.Key, Qty = g.Sum(i => i.Quantity) });
            await rmService.RecordRmTransferAsync(entry.SubcontractingOrderId.Value, transferLines, reverse: true);
        }

        // Reverse SLE entries + Bin balances
        await _stockPostingService.ReverseStockEntryAsync(entry);

        // Reverse PLE entries (StockEntry has no party, so this is normally a no-op)
        // and reverse the posted GL Journal Entry (per ERPNext: stock entries have perpetual inventory GL)
        await postingOrchestrator.ReversePleForDocumentAsync("StockEntry", entry.Id);
        await postingOrchestrator.ReverseGlForDocumentAsync("StockEntry", entry.Id);

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
        entry.WorkOrderId = input.WorkOrderId;
        entry.FgCompletedQty = input.FgCompletedQty;
        entry.ProcessLossQty = input.ProcessLossQty;
        entry.ProcessLossPercentage = input.ProcessLossPercentage;
        entry.SyncProcessLoss();
        entry.IsFgConversion = input.IsFgConversion;
        entry.WeightPerPiece = input.WeightPerPiece;

        // Replace items
        entry.ClearItems();
        foreach (var item in input.Items)
            entry.AddItem(item.ItemId, item.Quantity, item.SourceWarehouseId, item.TargetWarehouseId, item.ValuationRate, item.IsFinishedItem, item.BatchId);

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

    /// <summary>
    /// Gets items from a Material Request to pre-populate a Stock Entry form.
    /// Per ERPNext MR→SE mapper: creates SE with purpose based on MR type
    /// (Purchase→MaterialReceipt, Transfer→MaterialTransfer, Issue→MaterialIssue).
    /// Returns pending items (requested - already ordered/transferred).
    /// </summary>
    public async Task<MaterialRequestItemsForSeDto> GetItemsFromMaterialRequestAsync(
        Guid materialRequestId)
    {
        var mrRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Purchasing.Entities.MaterialRequest, Guid>>();
        var mr = (await mrRepo.WithDetailsAsync()).First(m => m.Id == materialRequestId);

        // Map MR type → SE purpose
        var purpose = mr.RequestType switch
        {
            Purchasing.MaterialRequestType.MaterialTransfer => StockEntryType.MaterialTransfer,
            Purchasing.MaterialRequestType.MaterialIssue => StockEntryType.MaterialIssue,
            _ => StockEntryType.MaterialTransfer,
        };

        var result = new MaterialRequestItemsForSeDto
        {
            MaterialRequestId = mr.Id,
            MaterialRequestNumber = mr.RequestNumber,
            SuggestedPurpose = purpose.ToString(),
            SourceWarehouseId = mr.SourceWarehouseId,
            TargetWarehouseId = mr.TargetWarehouseId,
        };

        foreach (var item in mr.Items)
        {
            var pendingQty = item.Quantity - item.OrderedQuantity;
            if (pendingQty <= 0) continue;

            result.Items.Add(new MaterialRequestItemLineDto
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Quantity = pendingQty,
                Uom = item.Uom,
                WarehouseId = item.WarehouseId,
                MaterialRequestItemId = item.Id,
            });
        }

        return result;
    }

}

/// <summary>DTO for pre-populating Stock Entry from Material Request.</summary>
public class MaterialRequestItemsForSeDto
{
    public Guid MaterialRequestId { get; set; }
    public string? MaterialRequestNumber { get; set; }
    public string SuggestedPurpose { get; set; } = null!;
    public Guid? SourceWarehouseId { get; set; }
    public Guid? TargetWarehouseId { get; set; }
    public List<MaterialRequestItemLineDto> Items { get; set; } = new();
}

public class MaterialRequestItemLineDto
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; }
    public string? Uom { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid MaterialRequestItemId { get; set; }
}

