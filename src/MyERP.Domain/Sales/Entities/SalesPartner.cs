using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Sales targets for this partner (PR #62324).</summary>
    public ICollection<SalesPartnerTarget> Targets { get; private set; }
        = new List<SalesPartnerTarget>();

    /// <summary>
    /// Add a sales target.
    /// Per ERPNext PR #62324: targets cannot be negative, cannot duplicate (fiscalYear, itemGroup), and must not both be zero.
    /// </summary>
    public void AddTarget(Guid? fiscalYearId, decimal targetQty, decimal targetAmount, Guid? itemGroupId = null, Guid? distributionId = null)
    {
        if (targetQty < 0 || targetAmount < 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Target Qty and Target Amount cannot be negative.");

        if (targetQty == 0 && targetAmount == 0)
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Target Qty or Target Amount must be greater than zero.");

        if (Targets.Any(t => t.FiscalYearId == fiscalYearId && t.ItemGroupId == itemGroupId))
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "A target already exists for this fiscal year and item group.");

        Targets.Add(new SalesPartnerTarget(
            Guid.NewGuid(), Id, fiscalYearId, targetQty, targetAmount, itemGroupId, distributionId));
    }

    public void RemoveTarget(Guid targetId)
    {
        var target = Targets.FirstOrDefault(t => t.Id == targetId);
        if (target != null)
        {
            Targets.Remove(target);
        }
    }
}

/// <summary>
/// Sales target for a sales partner (qty or amount target per fiscal year).
/// Maps to ERPNext target_detail for Sales Partner (PR #62324).
/// </summary>
public class SalesPartnerTarget : Volo.Abp.Domain.Entities.Entity<Guid>
{
    public Guid SalesPartnerId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public decimal TargetQty { get; set; }
    public decimal TargetAmount { get; set; }
    public Guid? ItemGroupId { get; set; }
    public Guid? DistributionId { get; set; }

    protected SalesPartnerTarget() { }

    public SalesPartnerTarget(Guid id, Guid salesPartnerId, Guid? fiscalYearId,
        decimal targetQty, decimal targetAmount, Guid? itemGroupId, Guid? distributionId = null) : base(id)
    {
        SalesPartnerId = salesPartnerId;
        FiscalYearId = fiscalYearId;
        TargetQty = targetQty;
        TargetAmount = targetAmount;
        ItemGroupId = itemGroupId;
        DistributionId = distributionId;
    }
}
