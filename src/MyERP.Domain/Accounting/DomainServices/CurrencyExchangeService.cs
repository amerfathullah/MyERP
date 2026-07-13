using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Resolves exchange rates for multi-currency transactions.
/// Resolution chain: direct match → reverse match → fallback to 1.0.
/// </summary>
public class CurrencyExchangeService : DomainService
{
    private readonly IRepository<CurrencyExchange, Guid> _repository;

    public CurrencyExchangeService(IRepository<CurrencyExchange, Guid> repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get the exchange rate for a currency pair on a given date.
    /// Returns the most recent rate on or before the transaction date.
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

        // Step 3: No rate found — return 1.0 (same currency assumption or needs manual entry)
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
}
