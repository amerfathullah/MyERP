using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Inventory.Entities;

/// <summary>
/// Price List — defines a set of item prices (e.g., "Standard Selling", "Wholesale", "VIP").
/// Each price list can be Selling, Buying, or both.
/// </summary>
public class PriceList : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public string CurrencyCode { get; set; } = "MYR";

    /// <summary>If true, this price list is used for selling transactions.</summary>
    public bool IsSelling { get; set; }

    /// <summary>If true, this price list is used for buying transactions.</summary>
    public bool IsBuying { get; set; }

    /// <summary>If true, this is the system default for its type (selling/buying).</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Company-scoped price list (null = cross-company).</summary>
    public Guid? CompanyId { get; set; }

    protected PriceList() { }

    public PriceList(Guid id, string name, string currencyCode, bool isSelling, bool isBuying, Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        CurrencyCode = currencyCode;
        IsSelling = isSelling;
        IsBuying = isBuying;
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), PriceListConsts.MaxNameLength);
    }
}
