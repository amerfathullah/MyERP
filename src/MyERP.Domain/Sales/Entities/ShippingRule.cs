using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Shipping Rule — calculates freight/shipping charges for transactions.
/// Supports fixed rate, net-total-based tiers, and weight-based tiers.
/// Applied as an "Actual" tax row on the transaction.
/// 
/// Per ERPNext:
/// - Selling rules can only be applied to selling docs, buying to buying docs
/// - Country-based filtering restricts rules to specific shipping destinations
/// - Currency conversion applies when doc currency != company currency
/// - Replaces existing shipping row if same account+cost center already exists
/// 
/// Source: erpnext/accounts/doctype/shipping_rule/shipping_rule.py
/// </summary>
public class ShippingRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    /// <summary>Display name for the rule.</summary>
    public string Label { get; set; } = null!;

    /// <summary>Whether this is a selling or buying shipping rule.</summary>
    public ShippingRuleType RuleType { get; set; }

    /// <summary>How the shipping amount is determined.</summary>
    public ShippingCalculationMode CalculationMode { get; set; }

    /// <summary>Fixed shipping amount (used when CalculationMode = Fixed).</summary>
    public decimal FixedAmount { get; set; }

    /// <summary>GL account to post shipping charges to.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Cost center for the shipping charge.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Whether this rule is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Conditions (amount/weight tiers) for range-based calculation.</summary>
    public ICollection<ShippingRuleCondition> Conditions { get; private set; }
        = new List<ShippingRuleCondition>();

    /// <summary>Country restrictions. If empty, rule applies globally.</summary>
    public ICollection<ShippingRuleCountry> Countries { get; private set; }
        = new List<ShippingRuleCountry>();

    protected ShippingRule() { }

    public ShippingRule(
        Guid id,
        string label,
        ShippingRuleType ruleType,
        ShippingCalculationMode calculationMode,
        Guid accountId,
        Guid? companyId = null,
        Guid? tenantId = null) : base(id)
    {
        Label = Check.NotNullOrWhiteSpace(label, nameof(label));
        RuleType = ruleType;
        CalculationMode = calculationMode;
        AccountId = accountId;
        CompanyId = companyId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a range-based condition (for NetTotal or NetWeight modes).
    /// </summary>
    public void AddCondition(decimal fromValue, decimal toValue, decimal shippingAmount)
    {
        Conditions.Add(new ShippingRuleCondition(
            Guid.NewGuid(), Id, fromValue, toValue, shippingAmount, Conditions.Count));
    }

    /// <summary>
    /// Add a country restriction.
    /// </summary>
    public void AddCountry(string countryCode)
    {
        Countries.Add(new ShippingRuleCountry(Guid.NewGuid(), Id, countryCode));
    }

    /// <summary>
    /// Validate rule configuration.
    /// </summary>
    public void Validate()
    {
        if (CalculationMode == ShippingCalculationMode.Fixed)
        {
            // Fixed mode clears conditions (they're irrelevant)
            return;
        }

        if (!Conditions.Any())
        {
            throw new BusinessException("MyERP:03004")
                .WithData("rule", Label);
        }

        // Only ONE condition can have to_value = 0 (catch-all "and above")
        var blankToValues = Conditions.Count(c => c.ToValue == 0);
        if (blankToValues > 1)
        {
            throw new BusinessException("MyERP:03005")
                .WithData("rule", Label);
        }

        // Check for overlapping ranges
        var rangedConditions = Conditions
            .Where(c => c.ToValue > 0)
            .OrderBy(c => c.FromValue)
            .ToList();

        for (int i = 0; i < rangedConditions.Count - 1; i++)
        {
            if (rangedConditions[i].ToValue > rangedConditions[i + 1].FromValue)
            {
                throw new BusinessException("MyERP:03006")
                    .WithData("rule", Label)
                    .WithData("range1To", rangedConditions[i].ToValue)
                    .WithData("range2From", rangedConditions[i + 1].FromValue);
            }
        }
    }

    /// <summary>
    /// Calculate the shipping amount for a given value (net total or weight).
    /// </summary>
    public decimal Calculate(decimal value)
    {
        if (CalculationMode == ShippingCalculationMode.Fixed)
            return FixedAmount;

        // Find matching condition (ordered by FromValue ASC, first match wins)
        var orderedConditions = Conditions.OrderBy(c => c.FromValue).ToList();

        foreach (var condition in orderedConditions)
        {
            if (condition.ToValue == 0 || condition.ToValue == 0m)
            {
                // Catch-all: matches anything >= FromValue
                if (value >= condition.FromValue)
                    return condition.ShippingAmount;
            }
            else if (value >= condition.FromValue && value <= condition.ToValue)
            {
                return condition.ShippingAmount;
            }
        }

        // No condition matched
        throw new BusinessException("MyERP:03007")
            .WithData("rule", Label)
            .WithData("value", value);
    }

    /// <summary>
    /// Check if this rule applies to a given shipping country.
    /// Returns true if: no country restrictions, OR country matches.
    /// </summary>
    public bool AppliesToCountry(string? countryCode)
    {
        if (!Countries.Any()) return true; // No restrictions = global
        if (string.IsNullOrEmpty(countryCode)) return true;
        return Countries.Any(c =>
            string.Equals(c.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// A condition/tier within a shipping rule (from_value → to_value → amount).
/// </summary>
public class ShippingRuleCondition : Entity<Guid>
{
    public Guid ShippingRuleId { get; set; }
    public decimal FromValue { get; set; }

    /// <summary>To value. 0 = catch-all ("and above").</summary>
    public decimal ToValue { get; set; }

    public decimal ShippingAmount { get; set; }
    public int SortOrder { get; set; }

    protected ShippingRuleCondition() { }

    public ShippingRuleCondition(Guid id, Guid ruleId, decimal fromValue, decimal toValue,
        decimal shippingAmount, int sortOrder) : base(id)
    {
        ShippingRuleId = ruleId;
        FromValue = fromValue;
        ToValue = toValue;
        ShippingAmount = shippingAmount;
        SortOrder = sortOrder;
    }
}

/// <summary>
/// Country restriction for a shipping rule.
/// </summary>
public class ShippingRuleCountry : Entity<Guid>
{
    public Guid ShippingRuleId { get; set; }
    public string CountryCode { get; set; } = null!;

    protected ShippingRuleCountry() { }

    public ShippingRuleCountry(Guid id, Guid ruleId, string countryCode) : base(id)
    {
        ShippingRuleId = ruleId;
        CountryCode = countryCode;
    }
}
