using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
using MyERP.Manufacturing.DomainServices;
using MyERP.Manufacturing.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace MyERP.Manufacturing.BackgroundJobs;

/// <summary>
/// Background job that synchronizes Job Card completions and operational progress back to active Work Orders.
/// Recalculates produced quantities, elapsed operational time, and auto-completes finished Work Orders.
/// Per ERPNext: work_order.update_operation_status (daily scheduler).
/// </summary>
public class WorkOrderOperationSyncJob : AsyncBackgroundJob<WorkOrderOperationSyncJobArgs>, ITransientDependency
{
    private readonly IRepository<WorkOrder, Guid> _workOrderRepository;
    private readonly IRepository<JobCard, Guid> _jobCardRepository;
    private readonly IRepository<ManufacturingSettings, Guid> _settingsRepository;
    private readonly IRepository<Inventory.Entities.StockEntry, Guid> _stockEntryRepository;
    private readonly IRepository<BillOfMaterials, Guid> _bomRepository;
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly StockPostingService _stockPostingService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ILogger<WorkOrderOperationSyncJob> _logger;
    private readonly WorkOrderManager _woManager;
    private readonly JobCardManager _jcManager;

    public WorkOrderOperationSyncJob(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<ManufacturingSettings, Guid> settingsRepository,
        IRepository<Inventory.Entities.StockEntry, Guid> stockEntryRepository,
        IRepository<BillOfMaterials, Guid> bomRepository,
        StockValuationService valuationService,
        BinService binService,
        StockPostingService stockPostingService,
        DocumentPostingOrchestrator postingOrchestrator,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager uowManager,
        ILogger<WorkOrderOperationSyncJob> logger,
        WorkOrderManager woManager,
        JobCardManager jcManager)
    {
        _workOrderRepository = workOrderRepository;
        _jobCardRepository = jobCardRepository;
        _settingsRepository = settingsRepository;
        _stockEntryRepository = stockEntryRepository;
        _bomRepository = bomRepository;
        _valuationService = valuationService;
        _binService = binService;
        _stockPostingService = stockPostingService;
        _postingOrchestrator = postingOrchestrator;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _uowManager = uowManager;
        _logger = logger;
        _woManager = woManager;
        _jcManager = jcManager;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(WorkOrderOperationSyncJobArgs args)
    {
        _logger.LogInformation("WorkOrderOperationSyncJob: Synchronizing work orders and job cards for company {CompanyId}",
            args.CompanyId);

        var woQuery = await _workOrderRepository.GetQueryableAsync();
        var activeWorkOrders = woQuery
            .Where(w => w.CompanyId == args.CompanyId &&
                        (w.Status == WorkOrderStatus.InProcess || w.Status == WorkOrderStatus.NotStarted))
            .ToList();

        if (!activeWorkOrders.Any())
            return;

        var jcQuery = await _jobCardRepository.GetQueryableAsync();
        var completedWoCount = 0;

        foreach (var wo in activeWorkOrders)
        {
            var jobCards = jcQuery.Where(j => j.WorkOrderId == wo.Id).ToList();
            if (!jobCards.Any())
                continue;

            // Check if all job cards are completed
            var allCompleted = jobCards.All(j => j.Status == JobCardStatus.Completed);
            if (allCompleted && jobCards.Count > 0)
            {
                var minCompletedQty = await _jcManager.GetWorkOrderCompletedQtyAsync(wo.Id, wo.Quantity);
                if (minCompletedQty > wo.ProducedQuantity)
                {
                    var delta = minCompletedQty - wo.ProducedQuantity;
                    await RecordProductionAsync(wo, delta, args.TenantId);
                    completedWoCount++;
                }
            }
        }

        _logger.LogInformation("WorkOrderOperationSyncJob: Processed {Total} active work orders (updated/completed {Updated}) for company {CompanyId}",
            activeWorkOrders.Count, completedWoCount, args.CompanyId);
    }

    /// <summary>
    /// Records production for a Work Order lagging behind its Job Cards' bottleneck completion —
    /// builds a real Manufacture Stock Entry (SLE/Bin + GL posting) instead of just bumping
    /// ProducedQuantity, mirroring the round-78 fix already applied to
    /// JobCardAppService.CompleteAsync (this job previously only incremented the counter with
    /// zero physical stock movement, so a Work Order this job "completed" would show as fully
    /// produced without any of the RM consumption / FG receipt / GL impact ever having happened).
    /// </summary>
    private async Task RecordProductionAsync(WorkOrder woSummary, decimal delta, Guid? tenantId)
    {
        var wo = await _workOrderRepository.GetAsync(woSummary.Id, includeDetails: true);
        if (wo.Status == WorkOrderStatus.NotStarted)
        {
            wo.Start();
        }

        await _postingOrchestrator.ValidatePostingPeriodAsync(wo.CompanyId, DateTime.UtcNow, "WorkOrder");

        var settingsQ = await _settingsRepository.GetQueryableAsync();
        var settings = settingsQ.FirstOrDefault(s => s.CompanyId == wo.CompanyId);
        var overproductionPct = settings?.OverproductionPercentage ?? 5m;

        wo.RecordProduction(delta, overproductionPercentage: overproductionPct);

        string entryNumber;
        try
        {
            entryNumber = await _numberGenerator.GenerateAsync("SE", wo.CompanyId);
        }
        catch (Volo.Abp.BusinessException ex) when (ex.Code == MyERPDomainErrorCodes.DocumentSeriesNotConfigured)
        {
            entryNumber = await _numberGenerator.GenerateAsync("StockEntry", wo.CompanyId);
        }

        var entry = new Inventory.Entities.StockEntry(
            _guidGenerator.Create(), wo.CompanyId, StockEntryType.Manufacture,
            DateTime.UtcNow.Date, tenantId ?? _currentTenant.Id)
        {
            WorkOrderId = wo.Id,
            EntryNumber = entryNumber,
            FgCompletedQty = delta,
            Notes = $"Production recorded — WO {wo.WorkOrderNumber} (WorkOrderOperationSyncJob catch-up)",
        };

        decimal totalRmCost = 0;
        var productionRatio = wo.Quantity > 0 ? delta / wo.Quantity : 0m;

        foreach (var item in wo.RequiredItems)
        {
            var issueQty = Math.Round(item.RequiredQuantity * productionRatio, 4);
            var warehouseId = item.SourceWarehouseId ?? wo.SourceWarehouseId;
            if (issueQty > 0 && warehouseId.HasValue)
            {
                var rmBalance = await _valuationService.GetCurrentBalanceAsync(item.ItemId, warehouseId.Value);
                var rmRate = rmBalance.ValuationRate;
                totalRmCost += issueQty * rmRate;

                entry.AddItem(
                    itemId: item.ItemId, quantity: issueQty,
                    sourceWarehouseId: warehouseId.Value, targetWarehouseId: null,
                    valuationRate: rmRate);

                await _binService.UpdateReservedQtyForProductionAsync(
                    item.ItemId, warehouseId.Value, -issueQty, wo.TenantId);
            }
        }

        var bom = await _bomRepository.FindAsync(wo.BomId);
        var fgCostAllocationPct = bom?.CostAllocationPercentage ?? 100m;
        var fgAllocatedCost = totalRmCost * (fgCostAllocationPct / 100m);

        // Operating cost from BOM operations (ERPNext PR #59804 / PR #60595)
        var unitOperatingCost = (bom?.Quantity > 0 && bom.OperatingCost > 0)
            ? bom.OperatingCost / bom.Quantity
            : 0m;
        var opCost = Math.Round(unitOperatingCost * delta, 4);
        if (opCost > 0)
        {
            entry.TotalAdditionalCosts += opCost;
        }

        // Additional operating cost from Work Order (ERPNext PR #62257 / commit 454fbb3182)
        if (wo.AdditionalOperatingCost > 0 && wo.Quantity > 0)
        {
            var addlCost = Math.Round((wo.AdditionalOperatingCost / wo.Quantity) * delta, 4);
            if (addlCost > 0)
            {
                entry.TotalAdditionalCosts += addlCost;
            }
        }

        if (wo.FgWarehouseId.HasValue && delta > 0)
        {
            var fgRate = fgAllocatedCost / delta;

            entry.AddItem(
                itemId: wo.ItemId, quantity: delta,
                sourceWarehouseId: null, targetWarehouseId: wo.FgWarehouseId.Value,
                valuationRate: fgRate, isFinishedItem: true);

            await _binService.UpdatePlannedQtyAsync(
                wo.ItemId, wo.FgWarehouseId.Value, -delta, wo.TenantId);
        }

        // Produce secondary items (co-products, by-products, scrap) when BOM defines them
        if (bom != null && bom.SecondaryItems.Any())
        {
            foreach (var secItem in bom.SecondaryItems)
            {
                var secQty = secItem.EffectiveQuantity * (delta / bom.Quantity);
                if (secQty <= 0) continue;

                var secCost = totalRmCost * (secItem.CostAllocationPercentage / 100m);
                var secRate = secCost / secQty;

                var secWarehouseId = secItem.SecondaryItemType == SecondaryItemType.Scrap
                    ? (wo.ScrapWarehouseId ?? bom.ScrapWarehouseId ?? wo.FgWarehouseId)
                    : wo.FgWarehouseId;

                if (secWarehouseId.HasValue)
                {
                    entry.AddItem(
                        itemId: secItem.ItemId, quantity: secQty,
                        sourceWarehouseId: null, targetWarehouseId: secWarehouseId.Value,
                        valuationRate: secRate,
                        secondaryItemType: secItem.SecondaryItemType.ToString(),
                        processLossPercentage: secItem.ProcessLossPercentage);
                }
            }
        }

        // Distribute operating / additional costs to finished goods item
        if (entry.TotalAdditionalCosts > 0)
        {
            Inventory.DomainServices.StockEntryManager.DistributeAdditionalCosts(entry);
        }

        entry.Submit();
        entry.Post();
        await _stockPostingService.PostStockEntryAsync(entry);

        // Flush before the GL step — DocumentPostingOrchestrator.PostStockEntryAsync queries the
        // just-inserted SLEs itself; without a save here that query can see zero rows and silently
        // skip building the Journal Entry (same reasoning as ManufacturingAppService's own helper).
        if (_uowManager.Current != null)
        {
            await _uowManager.Current.SaveChangesAsync();
        }

        await _postingOrchestrator.PostStockEntryAsync(entry);

        await _stockEntryRepository.InsertAsync(entry, autoSave: true);
        await _woManager.UpdateActualDatesAsync(wo, _stockEntryRepository, _jobCardRepository, currentStockEntry: entry);
        await _workOrderRepository.UpdateAsync(wo, autoSave: true);
    }
}

public class WorkOrderOperationSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
