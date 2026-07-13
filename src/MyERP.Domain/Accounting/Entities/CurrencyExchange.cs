using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Currency Exchange — stores historical exchange rates between currency pairs.
/// Used for multi-currency transactions and exchange rate revaluation.
/// </summary>
public class CurrencyExchange : AuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Source currency code (e.g., "USD").</summary>
    public string FromCurrency { get; set; } = null!;

    /// <summary>Target currency code (e.g., "MYR").</summary>
    public string ToCurrency { get; set; } = null!;

    /// <summary>Exchange rate (1 FromCurrency = ExchangeRate ToCurrency).</summary>
    public decimal ExchangeRate { get; set; }

    /// <summary>Date for which this rate applies.</summary>
    public DateTime Date { get; set; }

    /// <summary>Purpose: "Selling" or "Buying" or null (general).</summary>
    public string? ForBuying { get; set; }

    /// <summary>Whether this was auto-fetched from external API.</summary>
    public bool IsAutoFetched { get; set; }

    protected CurrencyExchange() { }

    public CurrencyExchange(Guid id, string fromCurrency, string toCurrency, decimal exchangeRate, DateTime date, Guid? tenantId = null)
        : base(id)
    {
        FromCurrency = fromCurrency;
        ToCurrency = toCurrency;
        ExchangeRate = exchangeRate;
        Date = date;
        TenantId = tenantId;
    }
}
