using System;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using Volo.Abp.DependencyInjection;

namespace MyERP;

/// <summary>
/// Test stub for ICurrencyExchangeProvider that returns a fixed rate.
/// Used in integration tests to avoid external HTTP calls.
/// </summary>
public class TestCurrencyExchangeProvider : ICurrencyExchangeProvider, ITransientDependency
{
    public Task<decimal?> FetchRateAsync(string fromCurrency, string toCurrency, DateTime date)
    {
        // Return a fixed rate for testing (4.72 MYR per USD)
        if (fromCurrency == toCurrency) return Task.FromResult<decimal?>(1.0m);
        return Task.FromResult<decimal?>(4.72m);
    }
}
