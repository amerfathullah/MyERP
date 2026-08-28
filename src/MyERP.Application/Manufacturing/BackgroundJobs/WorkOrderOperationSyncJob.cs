using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.DomainServices;
using MyERP.Core.DomainServices;
using MyERP.Inventory;
using MyERP.Inventory.DomainServices;
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
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly StockPostingService _stockPostingService;
    private readonly DocumentPostingOrchestrator _postingOrchestrator;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uowManager;
    private readonly ILogger<WorkOrderOperationSyncJob> _logger;

    public WorkOrderOperationSyncJob(
        IRepository<WorkOrder, Guid> workOrderRepository,
        IRepository<JobCard, Guid> jobCardRepository,
        IRepository<ManufacturingSettings, Guid> settingsRepository,
        IRepository<Inventory.Entities.StockEntry, Guid> stockEntryRepository,
        StockValuationService valuationService,
        BinService binService,
        StockPostingService stockPostingService,
        DocumentPostingOrchestrator postingOrchestrator,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager uowManager,
        ILogger<WorkOrderOperationSyncJob> logger)
    {
        _workOrderRepository = workOrderRepository;
        _jobCardRepository = jobCardRepository;
        _settingsRepository = settingsRepository;
        _stockEntryRepository = stockEntryRepository;
        _valuationService = valuationService;
        _binService = binService;
        _stockPostingService = stockPostingService;
        _postingOrchestrator = postingOrchestrator;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _uowManager = uowManager;
        _logger = logger;
    }

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
                var minCompletedQty = jobCards.Min(j => j.CompletedQty);
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

        var entry = new Inventory.Entities.StockEntry(
            _guidGenerator.Create(), wo.CompanyId, StockEntryType.Manufacture,
            DateTime.UtcNow.Date, tenantId ?? _currentTenant.Id)
        {
            WorkOrderId = wo.Id,
            EntryNumber = await _numberGenerator.GenerateAsync("SE", wo.CompanyId),
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

        if (wo.FgWarehouseId.HasValue && delta > 0)
        {
            var fgRate = totalRmCost / delta;

            entry.AddItem(
                itemId: wo.ItemId, quantity: delta,
                sourceWarehouseId: null, targetWarehouseId: wo.FgWarehouseId.Value,
                valuationRate: fgRate);

            await _binService.UpdatePlannedQtyAsync(
                wo.ItemId, wo.FgWarehouseId.Value, -delta, wo.TenantId);
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
        await _workOrderRepository.UpdateAsync(wo, autoSave: true);
    }
}

public class WorkOrderOperationSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
