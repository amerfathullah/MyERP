using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Sales Partner — channel partner / reseller / distributor for commission tracking.
/// Per ERPNext: sales partners earn commission on sales they facilitate.
/// Commission tracked via SalesTeamEntry child rows on SO/SI/DN.
/// Can have per-item commission rates and territory assignments.
///
/// Portal access: Sales Partners can view orders they're tagged on.
/// Website: can generate a partner landing page (ERPNext website module, low priority for MyERP).
/// </summary>
public class SalesPartner : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; private set; } = null!;
    public PartnerType PartnerType { get; set; }
    public decimal CommissionRate { get; private set; }

    /// <summary>Territory assignment for regional partner management.</summary>
    public Guid? TerritoryId { get; set; }

    /// <summary>Partner's website URL.</summary>
    public string? Website { get; set; }

    /// <summary>Description of the partnership arrangement.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this partner is actively taking orders.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Referral code for tracking leads and sales.
    /// Per ERPNext: used in web forms and portal links.
    /// </summary>
    public string? ReferralCode { get; set; }

    protected SalesPartner() { }

    public SalesPartner(Guid id, string name, PartnerType partnerType, decimal commissionRate, Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        PartnerType = partnerType;
        SetCommissionRate(commissionRate);
        TenantId = tenantId;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), SalesPartnerConsts.MaxNameLength);
    }

    /// <summary>
    /// Set commission rate with range validation.
    /// Per ERPNext: commission rate must be between 0 and 100%.
    /// </summary>
    public void SetCommissionRate(decimal rate)
    {
        if (rate < 0 || rate > 100)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidCommissionRate)
                .WithData("rate", rate);
        CommissionRate = rate;
    }

    /// <summary>Calculate commission earned on a given amount.</summary>
    public decimal CalculateCommission(decimal amount) => amount * CommissionRate / 100;

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;
}
