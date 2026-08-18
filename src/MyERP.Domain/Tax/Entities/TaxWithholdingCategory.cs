using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Tax Withholding Category — rate table + posting accounts for withholding tax (e.g. Malaysia
/// Section 107A). Referenced by <see cref="TaxWithholdingEntry"/> via its category code.
/// Maps to ERPNext accounts/doctype/tax_withholding_category, with child tables
/// Tax Withholding Rate and Tax Withholding Account.
/// </summary>
public class TaxWithholdingCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>Identifying code, e.g. "Section 107A - Royalty". Referenced by TaxWithholdingEntry.TaxCategory.</summary>
    public string CategoryName { get; private set; } = null!;

    public TaxDeductionBasis TaxDeductionBasis { get; set; } = TaxDeductionBasis.NetTotal;
    public bool RoundOffTaxAmount { get; set; }
    public bool TaxOnExcessAmount { get; set; }
    public bool DisableCumulativeThreshold { get; set; }
    public bool DisableTransactionThreshold { get; set; }

    private readonly List<TaxWithholdingRate> _rates = new();
    public IReadOnlyList<TaxWithholdingRate> Rates => _rates.AsReadOnly();

    private readonly List<TaxWithholdingAccount> _accounts = new();
    public IReadOnlyList<TaxWithholdingAccount> Accounts => _accounts.AsReadOnly();

    protected TaxWithholdingCategory() { }

    public TaxWithholdingCategory(Guid id, string categoryName, Guid? tenantId = null) : base(id)
    {
        Rename(categoryName);
        TenantId = tenantId;
    }

    public void Rename(string categoryName)
        => CategoryName = Check.NotNullOrWhiteSpace(categoryName, nameof(categoryName), TaxWithholdingCategoryConsts.MaxCategoryNameLength);

    /// <summary>Add a rate slab. Mirrors validate_dates: from &lt; to, no overlap within the same group.</summary>
    public void AddRate(DateTime fromDate, DateTime toDate, decimal rate,
        decimal? singleThreshold = null, decimal? cumulativeThreshold = null, string? group = null)
    {
        if (fromDate >= toDate)
            throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingRateDateRangeInvalid);

        if (cumulativeThreshold.HasValue && singleThreshold.HasValue && cumulativeThreshold < singleThreshold)
            throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingRateDateRangeInvalid)
                .WithData("reason", "Cumulative threshold cannot be less than transaction threshold");

        var overlaps = _rates.Any(r => string.Equals(r.Group, group, StringComparison.OrdinalIgnoreCase)
            && fromDate <= r.ToDate && toDate >= r.FromDate);
        if (overlaps)
            throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingRateOverlap);

        _rates.Add(new TaxWithholdingRate(Guid.NewGuid(), Id, fromDate, toDate, rate, singleThreshold, cumulativeThreshold, group));
    }

    public void ClearRates() => _rates.Clear();

    /// <summary>Add the posting account for a company. One account per company.</summary>
    public void AddAccount(Guid companyId, Guid accountId)
    {
        if (_accounts.Any(a => a.CompanyId == companyId))
            throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingDuplicateCompanyAccount);

        _accounts.Add(new TaxWithholdingAccount(Guid.NewGuid(), Id, companyId, accountId));
    }

    public void ClearAccounts() => _accounts.Clear();

    /// <summary>Mirrors get_applicable_tax_row — the rate slab covering the posting date and group.</summary>
    public TaxWithholdingRate GetApplicableRate(DateTime postingDate, string? group = null)
    {
        var row = _rates.FirstOrDefault(r => r.FromDate <= postingDate && postingDate <= r.ToDate
            && string.Equals(r.Group, group, StringComparison.OrdinalIgnoreCase));
        return row ?? throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingNoApplicableRate);
    }

    /// <summary>Mirrors get_company_account — the withholding posting account for a company.</summary>
    public Guid GetCompanyAccount(Guid companyId)
    {
        var account = _accounts.FirstOrDefault(a => a.CompanyId == companyId);
        return account?.AccountId ?? throw new BusinessException(MyERPDomainErrorCodes.TaxWithholdingNoAccountForCompany);
    }
}

public class TaxWithholdingRate : FullAuditedEntity<Guid>
{
    public Guid TaxWithholdingCategoryId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal Rate { get; set; }
    public decimal? SingleThreshold { get; set; }
    public decimal? CumulativeThreshold { get; set; }

    /// <summary>Free-text grouping (ERPNext Tax Withholding Group), simplified to a plain label.</summary>
    public string? Group { get; set; }

    protected TaxWithholdingRate() { }

    public TaxWithholdingRate(Guid id, Guid categoryId, DateTime fromDate, DateTime toDate, decimal rate,
        decimal? singleThreshold, decimal? cumulativeThreshold, string? group) : base(id)
    {
        TaxWithholdingCategoryId = categoryId;
        FromDate = fromDate;
        ToDate = toDate;
        Rate = rate;
        SingleThreshold = singleThreshold;
        CumulativeThreshold = cumulativeThreshold;
        Group = group;
    }
}

public class TaxWithholdingAccount : FullAuditedEntity<Guid>
{
    public Guid TaxWithholdingCategoryId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }

    protected TaxWithholdingAccount() { }

    public TaxWithholdingAccount(Guid id, Guid categoryId, Guid companyId, Guid accountId) : base(id)
    {
        TaxWithholdingCategoryId = categoryId;
        CompanyId = companyId;
        AccountId = accountId;
    }
}
