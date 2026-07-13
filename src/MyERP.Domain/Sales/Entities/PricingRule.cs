using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Pricing Rule — configurable discount/rate/free-item rules applied to transactions.
/// Supports item/group/brand/transaction-level matching with priority and date ranges.
/// Maps to ERPNext accounts/doctype/pricing_rule.
/// </summary>
public class PricingRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Selling, Buying, or Both.</summary>
    public string ApplicableFor { get; set; } = "Selling";

    /// <summary>What the rule matches: ItemCode, ItemGroup, Brand, TransactionTotal.</summary>
    public PricingRuleApplyOn ApplyOn { get; set; } = PricingRuleApplyOn.ItemCode;

    /// <summary>The item/group/brand this rule applies to.</summary>
    public Guid? ApplyOnId { get; set; }
    public string? ApplyOnName { get; set; }

    /// <summary>Type of discount: Discount, Rate, FreeItem.</summary>
    public PricingRuleType RuleType { get; set; } = PricingRuleType.Discount;

    /// <summary>Discount percentage (for Discount type).</summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>Discount amount (flat, for Discount type).</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Fixed rate (for Rate type).</summary>
    public decimal Rate { get; set; }

    /// <summary>Free item ID (for FreeItem type).</summary>
    public Guid? FreeItemId { get; set; }
    public decimal FreeItemQty { get; set; }

    /// <summary>Minimum qty to trigger this rule.</summary>
    public decimal MinQty { get; set; }

    /// <summary>Maximum qty (0 = no limit).</summary>
    public decimal MaxQty { get; set; }

    /// <summary>Minimum transaction amount to trigger.</summary>
    public decimal MinAmount { get; set; }

    /// <summary>Maximum amount (0 = no limit).</summary>
    public decimal MaxAmount { get; set; }

    /// <summary>Rule priority (higher = evaluated first). Multiple matching rules at same priority = error.</summary>
    public int Priority { get; set; } = 1;

    /// <summary>Valid date range.</summary>
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }

    /// <summary>Restrict to specific customer/supplier.</summary>
    public Guid? PartyId { get; set; }
    public string? PartyType { get; set; }

    public bool IsDisabled { get; set; }

    /// <summary>Apply rule on the other item (not the matched item).</summary>
    public bool ApplyOnOtherItem { get; set; }
    public Guid? OtherItemId { get; set; }

    protected PricingRule() { }

    public PricingRule(Guid id, string title, PricingRuleApplyOn applyOn,
        PricingRuleType ruleType, Guid? tenantId = null) : base(id)
    {
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 200);
        ApplyOn = applyOn;
        RuleType = ruleType;
        TenantId = tenantId;
    }

    /// <summary>Check if this rule matches the given context.</summary>
    public bool Matches(Guid? itemId, Guid? itemGroupId, decimal qty, decimal amount, DateTime transactionDate)
    {
        if (IsDisabled) return false;
        if (ValidFrom.HasValue && transactionDate < ValidFrom.Value) return false;
        if (ValidUpto.HasValue && transactionDate > ValidUpto.Value) return false;
        if (MinQty > 0 && qty < MinQty) return false;
        if (MaxQty > 0 && qty > MaxQty) return false;
        if (MinAmount > 0 && amount < MinAmount) return false;
        if (MaxAmount > 0 && amount > MaxAmount) return false;

        return ApplyOn switch
        {
            PricingRuleApplyOn.ItemCode => ApplyOnId == itemId,
            PricingRuleApplyOn.ItemGroup => ApplyOnId == itemGroupId,
            PricingRuleApplyOn.TransactionTotal => true,
            _ => false,
        };
    }
}
