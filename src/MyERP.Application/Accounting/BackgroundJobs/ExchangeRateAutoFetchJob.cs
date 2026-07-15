using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.DomainServices;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that auto-fetches exchange rates for stale currency pairs.
/// Per ERPNext: scheduled daily (or weekly/monthly depending on Accounts Settings).
/// 
/// Algorithm:
/// 1. Get all stale currency pairs (last rate > maxStaleDays old)
/// 2. For each pair, call CurrencyExchangeService.GetExchangeRateAsync
///    (which internally fetches from external API if no local rate exists)
/// 3. Store the fetched rates for future use
/// 
/// Per ERPNext hooks.py:
/// - Daily: auto_create_exchange_rate_revaluation_daily
/// - Weekly: auto_create_exchange_rate_revaluation_weekly  
/// - Monthly: auto_create_exchange_rate_revaluation_monthly
/// 
/// This job handles the rate FETCHING part. Revaluation is separate.
/// </summary>
public class ExchangeRateAutoFetchJob : AsyncBackgroundJob<ExchangeRateAutoFetchJobArgs>, ITransientDependency
{
    private readonly CurrencyExchangeService _exchangeService;
    private readonly ILogger<ExchangeRateAutoFetchJob> _logger;

    public ExchangeRateAutoFetchJob(
        CurrencyExchangeService exchangeService,
        ILogger<ExchangeRateAutoFetchJob> logger)
    {
        _exchangeService = exchangeService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ExchangeRateAutoFetchJobArgs args)
    {
        _logger.LogInformation(
            "ExchangeRateAutoFetchJob: Fetching stale exchange rates for company {CompanyId}, maxStaleDays={MaxStaleDays}",
            args.CompanyId, args.MaxStaleDays);

        var stalePairs = await _exchangeService.GetStalePairsAsync(args.MaxStaleDays);

        if (stalePairs.Count == 0)
        {
            _logger.LogInformation("ExchangeRateAutoFetchJob: No stale currency pairs found.");
            return;
        }

        int fetchedCount = 0;
        int failedCount = 0;

        foreach (var pair in stalePairs)
        {
            try
            {
                // GetExchangeRateAsync will auto-fetch from external API if no local rate exists
                var rate = await _exchangeService.GetExchangeRateAsync(
                    pair.FromCurrency, pair.ToCurrency, DateTime.UtcNow.Date);

                if (rate > 0 && rate != 1m)
                {
                    fetchedCount++;
                    _logger.LogDebug(
                        "ExchangeRateAutoFetchJob: Fetched {From}/{To} = {Rate}",
                        pair.FromCurrency, pair.ToCurrency, rate);
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex,
                    "ExchangeRateAutoFetchJob: Failed to fetch rate for {From}/{To}",
                    pair.FromCurrency, pair.ToCurrency);
            }
        }

        _logger.LogInformation(
            "ExchangeRateAutoFetchJob: Completed. Fetched={Fetched}, Failed={Failed}, Total={Total}",
            fetchedCount, failedCount, stalePairs.Count);
    }
}

public class ExchangeRateAutoFetchJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Maximum days since last rate update before considering it stale.
    /// Default 1 (daily updates). Per ERPNext: controlled by allow_stale + stale_days setting.
    /// </summary>
    public int MaxStaleDays { get; set; } = 1;
}
