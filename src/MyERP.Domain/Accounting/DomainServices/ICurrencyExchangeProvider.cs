using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Interface for external currency exchange rate providers.
/// Implementations fetch live rates from APIs (frankfurter.dev, exchangerate.host, etc.)
/// </summary>
public interface ICurrencyExchangeProvider : ITransientDependency
{
    /// <summary>
    /// Fetches the exchange rate from an external API.
    /// Returns null if the rate cannot be fetched (API down, unsupported pair, etc.)
    /// </summary>
    Task<decimal?> FetchRateAsync(string fromCurrency, string toCurrency, DateTime date);
}

/// <summary>
/// Frankfurter.dev v2 provider (free, no API key required).
/// Default provider per ERPNext install_fixtures.
/// Endpoint: GET /v2/rate/{from}/{to}?date={YYYY-MM-DD}
/// Response: { "rate": 4.72 }
/// </summary>
public class FrankfurterExchangeProvider : ICurrencyExchangeProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FrankfurterExchangeProvider> _logger;

    public FrankfurterExchangeProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<FrankfurterExchangeProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<decimal?> FetchRateAsync(string fromCurrency, string toCurrency, DateTime date)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("CurrencyExchange");
            var dateStr = date.ToString("yyyy-MM-dd");
            var url = $"https://api.frankfurter.dev/v2/rate/{fromCurrency.ToUpper()}/{toCurrency.ToUpper()}?date={dateStr}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Currency exchange API returned {StatusCode} for {From}→{To} on {Date}",
                    response.StatusCode, fromCurrency, toCurrency, dateStr);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("rate", out var rateElement))
            {
                return rateElement.GetDecimal();
            }

            _logger.LogWarning("Currency exchange API response missing 'rate' property for {From}→{To}", fromCurrency, toCurrency);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch exchange rate for {From}→{To} on {Date}", fromCurrency, toCurrency, date);
            return null;
        }
    }
}
