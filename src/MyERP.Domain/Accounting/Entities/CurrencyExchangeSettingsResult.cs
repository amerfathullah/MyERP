using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

public class CurrencyExchangeSettingsResult : CreationAuditedEntity<Guid>
{
    public Guid SettingsId { get; set; }
    public string Key { get; set; } = null!;

    protected CurrencyExchangeSettingsResult() { }

    public CurrencyExchangeSettingsResult(Guid id, Guid settingsId, string key)
        : base(id)
    {
        SettingsId = settingsId;
        Key = Check.NotNullOrWhiteSpace(key, nameof(key), CurrencyExchangeSettingsConsts.MaxKeyLength);
    }
}
