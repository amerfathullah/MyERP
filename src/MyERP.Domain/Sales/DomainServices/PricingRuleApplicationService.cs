using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Applies pricing rules to transaction items during document creation.
/// Priority-based: higher priority wins, same priority = ambiguity error.
/// Supports discount percentage, discount amount, fixed rate, and free items.
/// </summary>
public class PricingRuleApplicationService : DomainService
{
    private readonly IRepository<PricingRule, Guid> _pricingRuleRepository;

    public PricingRuleApplicationService(IRepository<PricingRule, Guid> pricingRuleRepository)
    {
        _pricingRuleRepository = pricingRuleRepository;
    }

    /// <summary>
    /// Applies matching pricing rules to a list of transaction items.
    /// Modifies item rates/discounts in-place based on matched rules.
    /// Returns list of applied rule results for audit/display.
    /// </summary>
    public async Task<List<AppliedPricingRule>> ApplyToItemsAsync(
        List<PricingRuleContext> items,
        DateTime transactionDate,
        string applicableFor = "Selling",
        Guid? partyId = null,
        Guid? companyId = null)
    {
        var queryable = await _pricingRuleRepository.GetQueryableAsync();
        var rules = queryable
            .Where(r => !r.IsDisabled && r.ApplicableFor == applicableFor)
            .ToList();

        if (!rules.Any()) return new List<AppliedPricingRule>();

        // Filter by company if specified
        if (companyId.HasValue)
            rules = rules.Where(r => !r.CompanyId.HasValue || r.CompanyId == companyId).ToList();

        // Filter by party if specified
        if (partyId.HasValue)
            rules = rules.Where(r => !r.PartyId.HasValue || r.PartyId == partyId).ToList();

        var applied = new List<AppliedPricingRule>();

        foreach (var item in items)
        {
            var matching = rules
                .Where(r => r.Matches(item.ItemId, item.ItemGroupId, item.Qty, item.Amount, transactionDate))
                .OrderByDescending(r => r.Priority)
                .ToList();

            if (!matching.Any()) continue;

            // Ambiguity detection at highest priority
            var topPriority = matching[0].Priority;
            var topRules = matching.Where(r => r.Priority == topPriority).ToList();
            if (topRules.Count > 1)
            {
                throw new BusinessException("MyERP:11001")
                    .WithData("detail", $"Multiple pricing rules at priority {topPriority} match for item '{item.ItemName}'. Resolve ambiguity.");
            }

            var rule = topRules[0];
            var result = ApplyRule(rule, item);
            if (result != null)
                applied.Add(result);
        }

        return applied;
    }

    private static AppliedPricingRule? ApplyRule(PricingRule rule, PricingRuleContext item)
    {
        switch (rule.RuleType)
        {
            case PricingRuleType.Discount:
                if (rule.DiscountPercentage > 0)
                {
                    item.DiscountPercentage = rule.DiscountPercentage;
                    item.DiscountedRate = item.Rate * (1 - rule.DiscountPercentage / 100m);
                }
                else if (rule.DiscountAmount > 0)
                {
                    item.DiscountAmount = rule.DiscountAmount;
                    item.DiscountedRate = Math.Max(0, item.Rate - rule.DiscountAmount);
                }
                break;

            case PricingRuleType.Rate:
                item.DiscountedRate = rule.Rate;
                break;

            case PricingRuleType.FreeItem:
                // Free item rules don't modify rate — they add a separate free item
                return new AppliedPricingRule
                {
                    RuleId = rule.Id,
                    RuleTitle = rule.Title,
                    RuleType = rule.RuleType,
                    FreeItemId = rule.FreeItemId,
                    FreeItemQty = rule.FreeItemQty,
                    AppliedToItemId = item.ItemId,
                };

            default:
                return null;
        }

        return new AppliedPricingRule
        {
            RuleId = rule.Id,
            RuleTitle = rule.Title,
            RuleType = rule.RuleType,
            DiscountPercentage = item.DiscountPercentage,
            DiscountAmount = item.DiscountAmount,
            DiscountedRate = item.DiscountedRate,
            AppliedToItemId = item.ItemId,
        };
    }
}

/// <summary>Input context for pricing rule evaluation per item.</summary>
public class PricingRuleContext
{
    public Guid ItemId { get; set; }
    public Guid? ItemGroupId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;

    // Outputs (set by rule application)
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountedRate { get; set; }
}

/// <summary>Result of a pricing rule application (for audit trail).</summary>
public class AppliedPricingRule
{
    public Guid RuleId { get; set; }
    public string RuleTitle { get; set; } = null!;
    public PricingRuleType RuleType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DiscountedRate { get; set; }
    public Guid? FreeItemId { get; set; }
    public decimal FreeItemQty { get; set; }
    public Guid? AppliedToItemId { get; set; }
}
