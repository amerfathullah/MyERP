using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

public class CurrencyExchangeSettingsDetail : CreationAuditedEntity<Guid>
{
    public Guid SettingsId { get; set; }
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;

    protected CurrencyExchangeSettingsDetail() { }

    public CurrencyExchangeSettingsDetail(Guid id, Guid settingsId, string key, string value)
        : base(id)
    {
        SettingsId = settingsId;
        Key = Check.NotNullOrWhiteSpace(key, nameof(key), CurrencyExchangeSettingsConsts.MaxKeyLength);
        Value = Check.NotNullOrWhiteSpace(value, nameof(value), CurrencyExchangeSettingsConsts.MaxValueLength);
    }
}
