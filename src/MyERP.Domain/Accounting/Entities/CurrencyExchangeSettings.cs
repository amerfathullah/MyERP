using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Currency Exchange Settings — configuration for live exchange rate API providers.
/// Maps to ERPNext accounts/doctype/currency_exchange_settings.
/// </summary>
public class CurrencyExchangeSettings : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string ServiceProvider { get; set; } = "frankfurter.dev";
    public string ApiEndpoint { get; set; } = "https://api.frankfurter.dev/v1/{transaction_date}";
    public string? AccessKey { get; set; }
    public string? Url { get; set; }
    public bool UseHttp { get; set; }
    public bool Disabled { get; set; }

    public virtual ICollection<CurrencyExchangeSettingsDetail> ReqParams { get; protected set; } = new Collection<CurrencyExchangeSettingsDetail>();
    public virtual ICollection<CurrencyExchangeSettingsResult> ResultKeys { get; protected set; } = new Collection<CurrencyExchangeSettingsResult>();

    protected CurrencyExchangeSettings() { }

    public CurrencyExchangeSettings(
        Guid id,
        string serviceProvider = "frankfurter.dev",
        string apiEndpoint = "https://api.frankfurter.dev/v1/{transaction_date}",
        string? accessKey = null,
        string? url = null,
        bool useHttp = false,
        bool disabled = false,
        Guid? tenantId = null)
        : base(id)
    {
        ServiceProvider = Check.NotNullOrWhiteSpace(serviceProvider, nameof(serviceProvider), CurrencyExchangeSettingsConsts.MaxServiceProviderLength);
        ApiEndpoint = Check.NotNullOrWhiteSpace(apiEndpoint, nameof(apiEndpoint), CurrencyExchangeSettingsConsts.MaxApiEndpointLength);
        AccessKey = accessKey;
        Url = url;
        UseHttp = useHttp;
        Disabled = disabled;
        TenantId = tenantId;
    }

    public void AddParam(Guid id, string key, string value)
    {
        ReqParams.Add(new CurrencyExchangeSettingsDetail(id, Id, key, value));
    }

    public void AddResultKey(Guid id, string key)
    {
        ResultKeys.Add(new CurrencyExchangeSettingsResult(id, Id, key));
    }

    public void ClearParamsAndResults()
    {
        ReqParams.Clear();
        ResultKeys.Clear();
    }
}
