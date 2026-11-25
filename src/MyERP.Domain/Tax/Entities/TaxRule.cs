using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Tax.Entities;

/// <summary>
/// Tax rule defines the rate and conditions for a tax category.
/// Supports date-range effectivity so rates can change without code changes.
/// Maps to ERPNext accounts/doctype/tax_rule.
/// </summary>
public class TaxRule : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid TaxCategoryId { get; set; }

    /// <summary>Tax rate as percentage (e.g., 6 = 6%, 8 = 8%).</summary>
    public decimal Rate { get; set; }

    /// <summary>Date this rule becomes effective.</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Date this rule expires. Null = no expiry.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Optional filter: applies only to items in this group.</summary>
    public string? ItemGroupFilter { get; set; }

    /// <summary>Optional filter: applies only to this region/state.</summary>
    public string? RegionFilter { get; set; }

    /// <summary>Priority when multiple rules match. Higher = evaluated first.</summary>
    public int Priority { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    protected TaxRule() { }

    public TaxRule(Guid id, Guid taxCategoryId, decimal rate, DateTime effectiveFrom, Guid? tenantId = null)
        : base(id)
    {
        TaxCategoryId = taxCategoryId;
        Rate = rate;
        EffectiveFrom = effectiveFrom;
        TenantId = tenantId;
    }

    /// <summary>Check if this rule is applicable for a given date.</summary>
    public bool IsApplicableOn(DateTime date)
    {
        return date >= EffectiveFrom && (EffectiveTo == null || date <= EffectiveTo);
    }

    /// <summary>
    /// Builds the document-header Tax Rule matching filter dictionary by excluding item-level filter keys
    /// (<c>item</c> and <c>item_group</c>) so document-level Tax Rules match properly
    /// per ERPNext commit 657b6398c9 (<c>tax_rule.py</c>).
    /// </summary>
    public static Dictionary<string, string?> BuildDocumentLevelTaxRuleArgs(IDictionary<string, string?>? rawArgs)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (rawArgs == null)
        {
            return result;
        }

        foreach (var (key, value) in rawArgs)
        {
            if (string.Equals(key, "item", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "item_group", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Resolves the billing address sent to Tax Rule matching: uses <paramref name="supplierAddress"/>
    /// ONLY when <paramref name="partyType"/> is <c>Supplier</c>, otherwise uses <paramref name="customerAddress"/>
    /// (covering Quotations whose <c>party_name</c> field does not populate <c>customer</c>/<c>lead</c>).
    /// Per ERPNext PR #69278 (<c>party.js: set_taxes</c>).
    /// </summary>
    public static string? ResolveTaxRuleBillingAddress(
        string? partyType,
        string? customerAddress,
        string? supplierAddress)
    {
        return string.Equals(partyType, "Supplier", StringComparison.OrdinalIgnoreCase)
            ? supplierAddress
            : customerAddress;
    }
}
