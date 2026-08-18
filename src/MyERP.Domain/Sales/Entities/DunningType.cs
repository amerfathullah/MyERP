using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Dunning Type — per-company configuration for a collections level: default fee,
/// yearly interest rate, posting accounts, and language-templated letter text.
/// Maps to ERPNext accounts/doctype/dunning_type. Referenced by <see cref="Dunning"/>
/// (fetch_from: dunning_fee, rate_of_interest, income_account, cost_center all default
/// from the selected Dunning Type but remain editable per document).
/// </summary>
public class DunningType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string DunningTypeName { get; private set; } = null!;
    public bool IsDefault { get; set; }

    public decimal DunningFee { get; set; }

    /// <summary>Yearly interest rate (%) on overdue amounts.</summary>
    public decimal RateOfInterest { get; set; }

    public Guid? IncomeAccountId { get; set; }
    public Guid? CostCenterId { get; set; }

    private readonly List<DunningLetterText> _letterText = new();
    public IReadOnlyList<DunningLetterText> LetterText => _letterText.AsReadOnly();

    protected DunningType() { }

    public DunningType(Guid id, Guid companyId, string dunningTypeName, Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Rename(dunningTypeName);
        TenantId = tenantId;
    }

    public void Rename(string dunningTypeName)
        => DunningTypeName = Check.NotNullOrWhiteSpace(dunningTypeName, nameof(dunningTypeName), DunningTypeConsts.MaxDunningTypeNameLength);

    /// <summary>Mirrors validate_languages + validate_is_default_language: one row per language, at most one default.</summary>
    public void SetLetterText(IEnumerable<(string? Language, bool IsDefaultLanguage, string? BodyText, string? ClosingText)> rows)
    {
        var list = rows.ToList();

        var languages = list.Select(r => r.Language ?? string.Empty).ToList();
        if (languages.Distinct().Count() != languages.Count)
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("reason", "Duplicate languages found on Dunning Letter Text");

        if (list.Count(r => r.IsDefaultLanguage) > 1)
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateRecord)
                .WithData("reason", "Only one Dunning Letter Text row may be marked as default language");

        _letterText.Clear();
        foreach (var row in list)
            _letterText.Add(new DunningLetterText(Guid.NewGuid(), Id, row.Language, row.IsDefaultLanguage, row.BodyText, row.ClosingText));
    }
}

/// <summary>Language-specific dunning letter body/closing text, used for print/email templating.</summary>
public class DunningLetterText : FullAuditedEntity<Guid>
{
    public Guid DunningTypeId { get; set; }
    public string? Language { get; set; }
    public bool IsDefaultLanguage { get; set; }
    public string? BodyText { get; set; }
    public string? ClosingText { get; set; }

    protected DunningLetterText() { }

    public DunningLetterText(Guid id, Guid dunningTypeId, string? language, bool isDefaultLanguage,
        string? bodyText, string? closingText) : base(id)
    {
        DunningTypeId = dunningTypeId;
        Language = language;
        IsDefaultLanguage = isDefaultLanguage;
        BodyText = bodyText;
        ClosingText = closingText;
    }
}
