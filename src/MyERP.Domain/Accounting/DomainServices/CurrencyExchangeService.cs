using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Resolves exchange rates for multi-currency transactions.
/// Resolution chain per ERPNext:
/// 1. Direct stored rate (from → to, most recent ≤ transactionDate)
/// 2. Reverse stored rate (to → from, inverted)
/// 3. External API fetch (frankfurter.dev v2 — auto-stores result for future lookups)
/// 4. Fallback: 1.0 (same currency or API unavailable)
/// </summary>
public class CurrencyExchangeService : DomainService
{
    private readonly IRepository<CurrencyExchange, Guid> _repository;
    private readonly ICurrencyExchangeProvider _provider;
    private readonly ILogger<CurrencyExchangeService> _logger;

    public CurrencyExchangeService(
        IRepository<CurrencyExchange, Guid> repository,
        ICurrencyExchangeProvider provider,
        ILogger<CurrencyExchangeService> logger)
    {
        _repository = repository;
        _provider = provider;
        _logger = logger;
    }

    /// <summary>
    /// Get the exchange rate for a currency pair on a given date.
    /// Checks stored rates first, then fetches from external provider if not found.
    /// Successfully fetched rates are auto-stored for future use.
    /// </summary>
    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, DateTime transactionDate)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var query = await _repository.GetQueryableAsync();

        // Step 1: Direct match (from → to)
        var direct = query
            .Where(e => e.FromCurrency == fromCurrency && e.ToCurrency == toCurrency && e.Date <= transactionDate)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (direct != null)
            return direct.ExchangeRate;

        // Step 2: Reverse match (to → from), invert
        var reverse = query
            .Where(e => e.FromCurrency == toCurrency && e.ToCurrency == fromCurrency && e.Date <= transactionDate)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (reverse != null && reverse.ExchangeRate != 0)
            return 1m / reverse.ExchangeRate;

        // Step 3: Fetch from external API provider
        var fetchedRate = await _provider.FetchRateAsync(fromCurrency, toCurrency, transactionDate);
        if (fetchedRate.HasValue && fetchedRate.Value > 0)
        {
            // Auto-store for future lookups
            await SetExchangeRateAsync(fromCurrency, toCurrency, fetchedRate.Value, transactionDate);
            _logger.LogInformation(
                "Auto-fetched exchange rate {From}→{To} = {Rate} for {Date}",
                fromCurrency, toCurrency, fetchedRate.Value, transactionDate.ToString("yyyy-MM-dd"));
            return fetchedRate.Value;
        }

        // Step 4: No rate found anywhere — return 1.0
        _logger.LogWarning(
            "No exchange rate found for {From}→{To} on {Date}. Using 1.0 fallback.",
            fromCurrency, toCurrency, transactionDate.ToString("yyyy-MM-dd"));
        return 1m;
    }

    /// <summary>
    /// Store a new exchange rate.
    /// </summary>
    public async Task<CurrencyExchange> SetExchangeRateAsync(string fromCurrency, string toCurrency, decimal rate, DateTime date, Guid? tenantId = null)
    {
        var entry = new CurrencyExchange(
            GuidGenerator.Create(), fromCurrency, toCurrency, rate, date, tenantId);
        await _repository.InsertAsync(entry);
        return entry;
    }

    /// <summary>
    /// Checks if the most recent stored exchange rate for a currency pair is stale.
    /// Per ERPNext Accounts Settings: allow_stale=false means stale_days must be >= 1.
    /// Returns (isStale, rateDate, daysSinceRate).
    /// </summary>
    public async Task<(bool IsStale, DateTime? RateDate, int DaysSinceRate)> CheckStaleRateAsync(
        string fromCurrency, string toCurrency, int maxStaleDays = 1)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return (false, null, 0);

        var query = await _repository.GetQueryableAsync();

        var latestRate = query
            .Where(e => (e.FromCurrency == fromCurrency && e.ToCurrency == toCurrency)
                || (e.FromCurrency == toCurrency && e.ToCurrency == fromCurrency))
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (latestRate == null)
            return (true, null, int.MaxValue); // No rate at all = stale

        int daysSince = (DateTime.UtcNow.Date - latestRate.Date).Days;
        bool isStale = daysSince > maxStaleDays;

        return (isStale, latestRate.Date, daysSince);
    }

    /// <summary>
    /// Gets all currency pairs that have stale exchange rates.
    /// Useful for dashboard/notification: "These currencies need rate updates."
    /// </summary>
    public async Task<List<StaleCurrencyPairInfo>> GetStalePairsAsync(int maxStaleDays = 1)
    {
        var query = await _repository.GetQueryableAsync();
        var today = DateTime.UtcNow.Date;

        // Get the latest rate per unique currency pair
        var allRates = query
            .OrderByDescending(e => e.Date)
            .ToList();

        var latestByPair = allRates
            .GroupBy(e => $"{e.FromCurrency}→{e.ToCurrency}")
            .Select(g => g.First())
            .ToList();

        return latestByPair
            .Where(r => (today - r.Date).Days > maxStaleDays)
            .Select(r => new StaleCurrencyPairInfo
            {
                FromCurrency = r.FromCurrency,
                ToCurrency = r.ToCurrency,
                LastRateDate = r.Date,
                LastRate = r.ExchangeRate,
                DaysSinceUpdate = (today - r.Date).Days
            })
            .OrderByDescending(s => s.DaysSinceUpdate)
            .ToList();
    }
}

public class StaleCurrencyPairInfo
{
    public string FromCurrency { get; set; } = null!;
    public string ToCurrency { get; set; } = null!;
    public DateTime LastRateDate { get; set; }
    public decimal LastRate { get; set; }
    public int DaysSinceUpdate { get; set; }
}
