using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.DomainServices;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that creates monthly Stock Closing Entry snapshots.
/// Freezes historical inventory balances and enables fast incremental stock balance reporting.
/// Per ERPNext: stock_closing_entry.process (monthly scheduler).
/// </summary>
public class StockClosingJob : AsyncBackgroundJob<StockClosingJobArgs>, ITransientDependency
{
    private readonly StockClosingService _closingService;
    private readonly IRepository<StockClosingEntry, Guid> _closingRepository;
    private readonly ILogger<StockClosingJob> _logger;

    public StockClosingJob(
        StockClosingService closingService,
        IRepository<StockClosingEntry, Guid> closingRepository,
        ILogger<StockClosingJob> logger)
    {
        _closingService = closingService;
        _closingRepository = closingRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(StockClosingJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;

        // Auto-close previous month's balance during first week of month
        var prevMonthEnd = new DateTime(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1);

        _logger.LogInformation("StockClosingJob: Checking monthly stock closing for company {CompanyId} up to {Date}",
            args.CompanyId, prevMonthEnd.ToString("yyyy-MM-dd"));

        var isAlreadyCovered = await _closingService.IsDateCoveredByClosingAsync(args.CompanyId, prevMonthEnd);
        if (isAlreadyCovered)
        {
            _logger.LogDebug("StockClosingJob: Stock closing already exists for {Date}. Skipping.", prevMonthEnd.ToString("yyyy-MM-dd"));
            return;
        }

        try
        {
            var closing = await _closingService.GenerateClosingAsync(args.CompanyId, prevMonthEnd, args.TenantId);
            closing.Submit();
            await _closingRepository.InsertAsync(closing);

            _logger.LogInformation("StockClosingJob: Generated and submitted monthly stock closing {ClosingId} with {Entries} item balances (Total value: MYR {Value:N2}) for company {CompanyId}",
                closing.Id, closing.TotalEntries, closing.TotalStockValue, args.CompanyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StockClosingJob: Failed to generate monthly stock closing for company {CompanyId}", args.CompanyId);
        }
    }
}

public class StockClosingJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
