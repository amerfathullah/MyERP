using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
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
/// - GL entries are recalculated after SLE repost (via GlRepostService)
/// 
/// Per DO-NOT: Skip advisory locking during SLE repost → causes data corruption under concurrency
/// Per gotcha #403: 3-path GL dispatch — item-based reposts may create child RIV docs for chained async processing
/// </summary>
public class RepostItemValuationJob : AsyncBackgroundJob<RepostItemValuationArgs>, ITransientDependency
{
    private readonly StockValuationService _valuationService;
    private readonly BinService _binService;
    private readonly GlRepostService _glRepostService;
    private readonly IRepository<StockLedgerEntry, Guid> _sleRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RepostItemValuationJob> _logger;

    public RepostItemValuationJob(
        StockValuationService valuationService,
        BinService binService,
        GlRepostService glRepostService,
        IRepository<StockLedgerEntry, Guid> sleRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IServiceProvider serviceProvider,
        ILogger<RepostItemValuationJob> logger)
    {
        _valuationService = valuationService;
        _binService = binService;
        _glRepostService = glRepostService;
        _sleRepository = sleRepository;
        _journalRepository = journalRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task ExecuteAsync(RepostItemValuationArgs args)
    {
        _logger.LogInformation(
            "Repost item valuation starting: Item={ItemId}, Warehouse={WarehouseId}, FromDate={FromDate}",
            args.ItemId, args.WarehouseId, args.FromDate);

        // Update RepostItemValuation entity status if tracking ID provided
        RepostItemValuation? repostEntry = null;
        if (args.RepostId.HasValue)
        {
            var repostRepo = (IRepository<RepostItemValuation, Guid>)
                _serviceProvider.GetService(typeof(IRepository<RepostItemValuation, Guid>))!;
            repostEntry = await repostRepo.FindAsync(args.RepostId.Value);
            if (repostEntry != null)
            {
                repostEntry.StartProcessing();
                await repostRepo.UpdateAsync(repostEntry);
            }
        }

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

            // Step 3: Rebuild GL entries for affected stock vouchers
            // Per gotcha #122: GL repost only compares 4 fields (account, cost_center, debit, credit)
            // Per gotcha #402: MAX_WRITES multiplied by 4 during repost
            await RepostAffectedGlEntriesAsync(args);

            _logger.LogInformation(
                "Repost item valuation completed: Item={ItemId}, Warehouse={WarehouseId}, FinalQty={Qty}, FinalValue={Value}",
                args.ItemId, args.WarehouseId, balance.Quantity, balance.Value);

            // Mark repost entry as completed
            if (repostEntry != null)
            {
                repostEntry.Complete(0);
                var repostRepo2 = (IRepository<RepostItemValuation, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<RepostItemValuation, Guid>))!;
                await repostRepo2.UpdateAsync(repostEntry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Repost item valuation failed: Item={ItemId}, Warehouse={WarehouseId}",
                args.ItemId, args.WarehouseId);

            // Mark repost entry as failed with error details
            if (repostEntry != null)
            {
                repostEntry.Fail(ex.Message);
                var repostRepo3 = (IRepository<RepostItemValuation, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<RepostItemValuation, Guid>))!;
                await repostRepo3.UpdateAsync(repostEntry);
            }

            // Don't re-throw — prevents infinite retry on persistent data issues.
            // Admin can trigger manually via API after investigating the error.
        }
    }

    /// <summary>
    /// Identifies and rebuilds GL entries for all stock vouchers affected by the revaluation.
    /// Per ERPNext: finds all SLEs for this item+warehouse from the repost date,
    /// groups by voucher, and reposts each voucher's GL entries.
    /// Per gotcha #122: GL repost compares only 4 fields (account, cost_center, debit, credit).
    /// Per gotcha #403: 3-path GL dispatch based on settings+type.
    /// </summary>
    private async Task RepostAffectedGlEntriesAsync(RepostItemValuationArgs args)
    {
        // Find all SLE entries affected by the repost (from date onwards for this item+warehouse)
        var affectedSles = await _sleRepository.GetListAsync(sle =>
            sle.ItemId == args.ItemId
            && sle.WarehouseId == args.WarehouseId
            && sle.PostingDate >= args.FromDate);

        if (!affectedSles.Any())
            return;

        // Group by voucher to find unique affected documents
        var affectedVouchers = affectedSles
            .Where(sle => !string.IsNullOrEmpty(sle.VoucherType) && sle.VoucherId.HasValue)
            .GroupBy(sle => new { sle.VoucherType, VoucherId = sle.VoucherId!.Value })
            .Select(g => new { g.Key.VoucherType, g.Key.VoucherId })
            .ToList();

        int repostedCount = 0;
        int skippedCount = 0;

        foreach (var voucher in affectedVouchers)
        {
            if (voucher.VoucherType == null || !GlRepostService.IsRepostAllowed(voucher.VoucherType))
            {
                skippedCount++;
                continue;
            }

            try
            {
                // Load the source document as IAccountableDocument and repost GL
                var document = await LoadAccountableDocumentAsync(voucher.VoucherType, voucher.VoucherId);
                if (document == null)
                {
                    skippedCount++;
                    continue;
                }

                var result = await _glRepostService.RepostForVoucherAsync(
                    args.CompanyId, voucher.VoucherType, voucher.VoucherId, document);

                if (result != null)
                {
                    repostedCount++;
                    _logger.LogDebug("GL reposted for {VoucherType}/{VoucherId}",
                        voucher.VoucherType, voucher.VoucherId);
                }
                else
                {
                    skippedCount++;
                }
            }
            catch (Exception ex)
            {
                // Per-voucher error isolation: one failure doesn't block others
                _logger.LogWarning(ex, "GL repost failed for {VoucherType}/{VoucherId}",
                    voucher.VoucherType, voucher.VoucherId);
                skippedCount++;
            }
        }

        if (repostedCount > 0 || skippedCount > 0)
        {
            _logger.LogInformation(
                "GL repost complete: {Reposted} reposted, {Skipped} skipped for Item={ItemId}, Warehouse={WarehouseId}",
                repostedCount, skippedCount, args.ItemId, args.WarehouseId);
        }
    }

    /// <summary>
    /// Loads a stock voucher document as IAccountableDocument for GL repost.
    /// Only stock-affecting documents (SE, PR, DN) need GL repost after valuation change.
    /// SI/PI only need repost if they have UpdateStock=true.
    /// </summary>
    private async Task<IAccountableDocument?> LoadAccountableDocumentAsync(string voucherType, Guid voucherId)
    {
        switch (voucherType)
        {
            case "StockEntry":
                var seRepo = (IRepository<MyERP.Inventory.Entities.StockEntry, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Inventory.Entities.StockEntry, Guid>))!;
                var se = await seRepo.FindAsync(voucherId);
                return se; // StockEntry implements IAccountableDocument

            case "PurchaseReceipt":
                var prRepo = (IRepository<MyERP.Purchasing.Entities.PurchaseReceipt, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Purchasing.Entities.PurchaseReceipt, Guid>))!;
                var pr = await prRepo.FindAsync(voucherId);
                return pr; // PurchaseReceipt implements IAccountableDocument

            case "DeliveryNote":
                var dnRepo = (IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Sales.Entities.DeliveryNote, Guid>))!;
                var dn = await dnRepo.FindAsync(voucherId);
                return dn; // DeliveryNote implements IAccountableDocument

            case "SalesInvoice":
                var siRepo = (IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Sales.Entities.SalesInvoice, Guid>))!;
                var si = await siRepo.FindAsync(voucherId);
                if (si?.UpdateStock == true) return si;
                return null; // SI without UpdateStock doesn't need GL repost from stock valuation change

            case "PurchaseInvoice":
                var piRepo = (IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>)
                    _serviceProvider.GetService(typeof(IRepository<MyERP.Purchasing.Entities.PurchaseInvoice, Guid>))!;
                var pi = await piRepo.FindAsync(voucherId);
                if (pi?.UpdateStock == true) return pi;
                return null; // PI without UpdateStock doesn't need GL repost from stock valuation change

            default:
                return null;
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
    /// Optional: tracking ID for the RepostItemValuation entity (status updates on start/complete/fail).
    /// </summary>
    public Guid? RepostId { get; set; }

    /// <summary>
    /// Optional: limit repost to specific valuation method items.
    /// When null, uses item's configured method.
    /// </summary>
    public string? Reason { get; set; }
}
