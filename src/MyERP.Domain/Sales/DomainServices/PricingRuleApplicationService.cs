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
    private readonly DiscountCeilingValidationService _discountCeilingValidationService;

    public PricingRuleApplicationService(
        IRepository<PricingRule, Guid> pricingRuleRepository,
        DiscountCeilingValidationService discountCeilingValidationService)
    {
        _pricingRuleRepository = pricingRuleRepository;
        _discountCeilingValidationService = discountCeilingValidationService;
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
        var rulesQuery = queryable.Where(r => !r.IsDisabled);

        // Filter by company if specified
        if (companyId.HasValue)
            rulesQuery = rulesQuery.Where(r => !r.CompanyId.HasValue || r.CompanyId == companyId);

        // Filter by party if specified
        if (partyId.HasValue)
            rulesQuery = rulesQuery.Where(r => !r.PartyId.HasValue || r.PartyId == partyId);

        var allRules = rulesQuery.ToList();
        if (!allRules.Any()) return new List<AppliedPricingRule>();

        var applied = new List<AppliedPricingRule>();

        foreach (var item in items)
        {
            // Per ERPNext PR #59406 (set correct transaction type on args copy while evaluating item-wise pricing rule):
            // Resolve transaction type per item row (item override > item doctype > document applicableFor)
            var itemApplicableFor = ResolveTransactionType(item.TransactionType, item.Doctype, applicableFor);

            var matching = allRules
                .Where(r => string.Equals(r.ApplicableFor, itemApplicableFor, StringComparison.OrdinalIgnoreCase)
                         && r.Matches(item.ItemId, item.ItemGroupId, item.Qty, item.Amount, transactionDate,
                             item.Campaign, item.UtmCampaign, item.ConversionFactor, item.SameTransactionUom))
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

        // Per DiscountCeilingValidationService (Item.MaxDiscount, Gotcha #3222): a Pricing Rule is
        // configured independently of any specific item's discount ceiling, so it can easily exceed
        // one without the rule author noticing — validate every rule-computed percentage discount
        // against its item's ceiling before returning, so a misconfigured rule blocks the whole
        // document rather than silently overselling. Rate/DiscountAmount/FreeItem rule types are
        // exempt — MaxDiscount is defined as a percentage ceiling only, same as ERPNext. Selling-only:
        // MaxDiscount caps what a customer can be discounted, it isn't a ceiling on a bigger discount
        // a supplier grants us on the buying side.
        var percentageDiscounts = items
            .Where(i => i.DiscountPercentage > 0 && ResolveTransactionType(i.TransactionType, i.Doctype, applicableFor) == "Selling")
            .Select(i => (i.ItemId, i.DiscountPercentage));
        await _discountCeilingValidationService.ValidateDiscountsAsync(percentageDiscounts);

        return applied;
    }

    /// <summary>
    /// Sets and resolves correct transaction type (Selling vs Buying) for pricing rule evaluation context (ERPNext PR #59406).
    /// </summary>
    public static string ResolveTransactionType(string? transactionType, string? doctype, string fallback = "Selling")
    {
        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            if (string.Equals(transactionType, "selling", StringComparison.OrdinalIgnoreCase)) return "Selling";
            if (string.Equals(transactionType, "buying", StringComparison.OrdinalIgnoreCase)) return "Buying";
        }

        if (!string.IsNullOrWhiteSpace(doctype))
        {
            switch (doctype.Trim().ToLowerInvariant())
            {
                case "opportunity":
                case "quotation":
                case "salesorder":
                case "sales order":
                case "deliverynote":
                case "delivery note":
                case "salesinvoice":
                case "sales invoice":
                    return "Selling";

                case "materialrequest":
                case "material request":
                case "supplierquotation":
                case "supplier quotation":
                case "purchaseorder":
                case "purchase order":
                case "purchasereceipt":
                case "purchase receipt":
                case "purchaseinvoice":
                case "purchase invoice":
                    return "Buying";
            }
        }

        return fallback;
    }

    /// <summary>
    /// Allow-list of transaction document types permitted to invoke the pricing engine
    /// (ERPNext PR #69273: <c>PRICING_TRANSACTION_DOCTYPES</c>).
    /// Accepts both ERPNext space-separated names and MyERP PascalCase names.
    /// </summary>
    public static readonly IReadOnlySet<string> PricingTransactionDoctypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Quotation",
        "Sales Order", "SalesOrder",
        "Delivery Note", "DeliveryNote",
        "Sales Invoice", "SalesInvoice",
        "POS Invoice", "PosInvoice",
        "Supplier Quotation", "SupplierQuotation",
        "Purchase Order", "PurchaseOrder",
        "Purchase Receipt", "PurchaseReceipt",
        "Purchase Invoice", "PurchaseInvoice",
        "Material Request", "MaterialRequest",
        "BOM",
        "Request for Quotation", "RequestForQuotation",
        "Opportunity"
    };

    /// <summary>
    /// Authorizes a client-supplied pricing context before running the pricing engine
    /// (ERPNext PR #69273: <c>validate_pricing_context</c>, <c>validate_pricing_parties</c>,
    /// <c>validate_pricing_price_lists</c>, <c>fits_price_list_side</c>).
    /// Validates transaction doctype allow-list, transaction read permission, company restriction scope,
    /// party select permission, and selling/buying price list side compatibility.
    /// </summary>
    public static void ValidatePricingContext(
        string? doctype,
        bool hasTransactionReadPermission,
        Guid? companyId = null,
        IReadOnlySet<Guid>? allowedCompanyIds = null,
        bool hasParty = false,
        bool hasPartySelectPermission = true,
        string? priceListName = null,
        bool priceListMatchesSide = true,
        bool isInternalParty = false,
        bool isReturnAgainstSamePriceList = false)
    {
        if (string.IsNullOrWhiteSpace(doctype) || !PricingTransactionDoctypes.Contains(doctype.Trim()))
        {
            throw new BusinessException("MyERP:11002")
                .WithData("detail", $"Invalid doctype '{doctype}' for pricing evaluation.");
        }

        if (!hasTransactionReadPermission)
        {
            throw new BusinessException("MyERP:11003")
                .WithData("detail", $"Not permitted to read '{doctype}' for pricing evaluation.");
        }

        if (companyId.HasValue && allowedCompanyIds != null && allowedCompanyIds.Count > 0
            && !allowedCompanyIds.Contains(companyId.Value))
        {
            throw new BusinessException("MyERP:11004")
                .WithData("detail", $"Not permitted for company '{companyId.Value}'.");
        }

        if (hasParty && !hasPartySelectPermission)
        {
            throw new BusinessException("MyERP:11005")
                .WithData("detail", "Not permitted to select the specified party for pricing evaluation.");
        }

        var trimmedDoctype = doctype.Trim();
        if (string.Equals(trimmedDoctype, "BOM", StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrWhiteSpace(priceListName))
        {
            var side = ResolvePriceListSide(trimmedDoctype);
            var fits = side != null && FitsPriceListSide(priceListMatchesSide, isInternalParty, isReturnAgainstSamePriceList);
            if (!fits)
            {
                throw new BusinessException("MyERP:11006")
                    .WithData("detail", $"Price List '{priceListName}' cannot be used in {trimmedDoctype}.");
            }
        }
    }

    /// <summary>
    /// Returns whether a price list fits the transaction side per ERPNext PR #69273 (<c>fits_price_list_side</c>):
    /// matches side flag, or matches the submitted voucher's price list on a return, or party is internal.
    /// </summary>
    public static bool FitsPriceListSide(
        bool priceListMatchesSide,
        bool isInternalParty,
        bool isReturnAgainstSamePriceList)
    {
        return priceListMatchesSide || isReturnAgainstSamePriceList || isInternalParty;
    }

    private static string? ResolvePriceListSide(string doctype)
    {
        switch (doctype.ToLowerInvariant())
        {
            case "quotation":
            case "sales order":
            case "salesorder":
            case "delivery note":
            case "deliverynote":
            case "sales invoice":
            case "salesinvoice":
            case "pos invoice":
            case "posinvoice":
                return "Selling";

            case "supplier quotation":
            case "supplierquotation":
            case "purchase order":
            case "purchaseorder":
            case "purchase receipt":
            case "purchasereceipt":
            case "purchase invoice":
            case "purchaseinvoice":
                return "Buying";

            default:
                return null;
        }
    }

    private static AppliedPricingRule? ApplyRule(PricingRule rule, PricingRuleContext item)
    {
        switch (rule.RuleType)
        {
            case PricingRuleType.Discount:
                // Per ERPNext PR #66398: reset conflicting discount fields when applying a pricing discount rule
                item.DiscountPercentage = 0m;
                item.DiscountAmount = 0m;
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
                item.DiscountPercentage = 0m;
                item.DiscountAmount = 0m;
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
    public string? TransactionType { get; set; }
    public string? Doctype { get; set; }
    public string? Campaign { get; set; }
    public string? UtmCampaign { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
    public bool SameTransactionUom { get; set; }

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
