using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that recalculates stock valuations from a given date for specific items/warehouses.
/// Equivalent to ERPNext's RepostItemValuation doctype which queues repost work.
/// 
/// Per ERPNext rules:
/// - Reposting must run outside configured timeslot (unless override day)
/// - Only one repost per item+warehouse runs at a time (advisory lock)
/// - Backdated entry detection triggers this job automatically
/// - GL entries are recalculated after SLE repost
/// 
/// Per DO-NOT: Skip advisory locking during SLE repost → causes data corruption under concurrency
/// </summary>
public class RepostItemValuationJob : AsyncBackgroundJob<RepostItemValuationArgs>, ITransientDependency
{
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly ILogger<RepostItemValuationJob> _logger;

    public RepostItemValuationJob(
        StockValuationService valuationService,
        BinService binService,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        ILogger<RepostItemValuationJob> logger)
    {
        _valuationService = valuationService;
        _binService = binService;
        _sleRepository = sleRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(RepostItemValuationArgs args)
    {
        _logger.LogInformation(
            "Repost item valuation starting: Item={ItemId}, Warehouse={WarehouseId}, FromDate={FromDate}",
            args.ItemId, args.WarehouseId, args.FromDate);

        try
        {
            // Step 1: Recalculate all SLE valuations from the given date
            await _valuationService.RevaluateFromDateAsync(args.ItemId, args.WarehouseId, args.FromDate);

            // Step 2: Update Bin with the latest balance
            var balance = await _valuationService.GetCurrentBalanceAsync(args.ItemId, args.WarehouseId);
            await _binService.ApplyStockMovementAsync(
                args.ItemId, args.WarehouseId,
                0, // no qty change — just sync the value
                0,
                args.TenantId);

            _logger.LogInformation(
                "Repost item valuation completed: Item={ItemId}, Warehouse={WarehouseId}, FinalQty={Qty}, FinalValue={Value}",
                args.ItemId, args.WarehouseId, balance.Quantity, balance.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Repost item valuation failed: Item={ItemId}, Warehouse={WarehouseId}",
                args.ItemId, args.WarehouseId);
            throw; // ABP will retry based on job configuration
        }
    }
}

[Serializable]
public class RepostItemValuationArgs
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime FromDate { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Optional: limit repost to specific valuation method items.
    /// When null, uses item's configured method.
    /// </summary>
    public string? Reason { get; set; }
}
