using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Promotional Scheme — a template that generates a batch of Pricing Rules from
/// price-discount and product-discount (free item) slabs, cross joined against the
/// scheme's apply-on targets (items/groups/brands) and applicable-for parties.
/// Maps to ERPNext accounts/doctype/promotional_scheme.
/// </summary>
public class PromotionalScheme : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Title { get; set; } = null!;

    public bool IsDisabled { get; set; }

    /// <summary>What the generated rules match: ItemCode, ItemGroup, Brand, TransactionTotal.</summary>
    public PricingRuleApplyOn ApplyOn { get; set; } = PricingRuleApplyOn.ItemCode;

    /// <summary>When true, all conditions across items/groups must match together rather than any one.</summary>
    public bool MixedConditions { get; set; }

    /// <summary>When true, discounts across matching rules stack instead of only the best one applying.</summary>
    public bool IsCumulative { get; set; }

    /// <summary>Apply the discount to a different item/group/brand than the one that triggered it.</summary>
    public bool ApplyRuleOnOtherItem { get; set; }
    public PricingRuleApplyOn? OtherApplyOn { get; set; }
    public Guid? OtherTargetId { get; set; }

    public bool Selling { get; set; }
    public bool Buying { get; set; }

    public PromotionalSchemeApplicableFor ApplicableFor { get; set; } = PromotionalSchemeApplicableFor.None;

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUpto { get; set; }
    public Guid? CurrencyId { get; set; }

    private readonly List<PromotionalSchemeTarget> _targets = new();
    /// <summary>Items/groups/brands the scheme applies on (semantics driven by <see cref="ApplyOn"/>). Empty = all.</summary>
    public IReadOnlyList<PromotionalSchemeTarget> Targets => _targets.AsReadOnly();

    private readonly List<PromotionalSchemeParty> _parties = new();
    /// <summary>Party restriction values (semantics driven by <see cref="ApplicableFor"/>). Empty = no restriction.</summary>
    public IReadOnlyList<PromotionalSchemeParty> Parties => _parties.AsReadOnly();

    private readonly List<PromotionalSchemePriceDiscountSlab> _priceDiscountSlabs = new();
    public IReadOnlyList<PromotionalSchemePriceDiscountSlab> PriceDiscountSlabs => _priceDiscountSlabs.AsReadOnly();

    private readonly List<PromotionalSchemeProductDiscountSlab> _productDiscountSlabs = new();
    public IReadOnlyList<PromotionalSchemeProductDiscountSlab> ProductDiscountSlabs => _productDiscountSlabs.AsReadOnly();

    protected PromotionalScheme() { }

    public PromotionalScheme(Guid id, Guid companyId, string title, PricingRuleApplyOn applyOn, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), 140);
        ApplyOn = applyOn;
        TenantId = tenantId;
    }

    public void Validate()
    {
        if (!Selling && !Buying)
            throw new BusinessException(MyERPDomainErrorCodes.PromotionalSchemeRequiresSellingOrBuying);

        if (!_priceDiscountSlabs.Any() && !_productDiscountSlabs.Any())
            throw new BusinessException(MyERPDomainErrorCodes.PromotionalSchemeRequiresSlabs);

        if (ApplicableFor != PromotionalSchemeApplicableFor.None && !_parties.Any())
            throw new BusinessException(MyERPDomainErrorCodes.PromotionalSchemeApplicableForRequiresParty)
                .WithData("applicableFor", ApplicableFor.ToString());

        if (MixedConditions && _productDiscountSlabs.Any(s => s.IsRecursive))
            throw new BusinessException(MyERPDomainErrorCodes.PromotionalSchemeRecursiveWithMixedConditions);
    }

    public void AddTarget(Guid targetId, string? targetName = null)
    {
        if (_targets.Any(t => t.TargetId == targetId)) return;
        _targets.Add(new PromotionalSchemeTarget(Guid.NewGuid(), Id, targetId, targetName));
    }

    public void ClearTargets() => _targets.Clear();

    public void AddParty(Guid partyId, string? partyName = null)
    {
        if (_parties.Any(p => p.PartyId == partyId)) return;
        _parties.Add(new PromotionalSchemeParty(Guid.NewGuid(), Id, partyId, partyName));
    }

    public void ClearParties() => _parties.Clear();

    public PromotionalSchemePriceDiscountSlab AddPriceDiscountSlab(
        PricingRuleType rateOrDiscount, decimal discountPercentage, decimal discountAmount, decimal rate,
        decimal minQty = 0, decimal maxQty = 0, decimal minAmount = 0, decimal maxAmount = 0,
        int priority = 1, Guid? warehouseId = null, string? description = null)
    {
        var slab = new PromotionalSchemePriceDiscountSlab(Guid.NewGuid(), Id, rateOrDiscount,
            discountPercentage, discountAmount, rate, minQty, maxQty, minAmount, maxAmount,
            priority, warehouseId, description);
        _priceDiscountSlabs.Add(slab);
        return slab;
    }

    public void ClearPriceDiscountSlabs() => _priceDiscountSlabs.Clear();

    public PromotionalSchemeProductDiscountSlab AddProductDiscountSlab(
        Guid freeItemId, decimal freeQty, decimal freeItemRate, bool sameItem,
        decimal minQty = 0, decimal maxQty = 0, decimal minAmount = 0, decimal maxAmount = 0,
        int priority = 1, Guid? warehouseId = null, string? description = null,
        bool isRecursive = false, decimal recurseFor = 0, bool roundFreeQty = false)
    {
        var slab = new PromotionalSchemeProductDiscountSlab(Guid.NewGuid(), Id, freeItemId, freeQty,
            freeItemRate, sameItem, minQty, maxQty, minAmount, maxAmount, priority, warehouseId,
            description, isRecursive, recurseFor, roundFreeQty);
        _productDiscountSlabs.Add(slab);
        return slab;
    }

    public void ClearProductDiscountSlabs() => _productDiscountSlabs.Clear();
}

/// <summary>Apply-on target (item/group/brand id) for a Promotional Scheme, matched per the scheme's ApplyOn.</summary>
public class PromotionalSchemeTarget : Entity<Guid>
{
    public Guid PromotionalSchemeId { get; set; }
    public Guid TargetId { get; set; }
    public string? TargetName { get; set; }

    protected PromotionalSchemeTarget() { }

    public PromotionalSchemeTarget(Guid id, Guid promotionalSchemeId, Guid targetId, string? targetName = null) : base(id)
    {
        PromotionalSchemeId = promotionalSchemeId;
        TargetId = targetId;
        TargetName = targetName;
    }

    public override object[] GetKeys() => new object[] { Id };
}

/// <summary>Applicable-for party restriction value for a Promotional Scheme.</summary>
public class PromotionalSchemeParty : Entity<Guid>
{
    public Guid PromotionalSchemeId { get; set; }
    public Guid PartyId { get; set; }
    public string? PartyName { get; set; }

    protected PromotionalSchemeParty() { }

    public PromotionalSchemeParty(Guid id, Guid promotionalSchemeId, Guid partyId, string? partyName = null) : base(id)
    {
        PromotionalSchemeId = promotionalSchemeId;
        PartyId = partyId;
        PartyName = partyName;
    }

    public override object[] GetKeys() => new object[] { Id };
}

/// <summary>Price/rate discount slab. One Pricing Rule is generated per slab x target x party.</summary>
public class PromotionalSchemePriceDiscountSlab : Entity<Guid>
{
    public Guid PromotionalSchemeId { get; set; }

    /// <summary>Discount (percentage/amount) or Rate (fixed price).</summary>
    public PricingRuleType RateOrDiscount { get; set; } = PricingRuleType.Discount;
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Rate { get; set; }

    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; } = 1;
    public Guid? WarehouseId { get; set; }
    public string? Description { get; set; }
    public bool IsDisabled { get; set; }

    protected PromotionalSchemePriceDiscountSlab() { }

    public PromotionalSchemePriceDiscountSlab(Guid id, Guid promotionalSchemeId, PricingRuleType rateOrDiscount,
        decimal discountPercentage, decimal discountAmount, decimal rate,
        decimal minQty, decimal maxQty, decimal minAmount, decimal maxAmount,
        int priority, Guid? warehouseId, string? description) : base(id)
    {
        PromotionalSchemeId = promotionalSchemeId;
        RateOrDiscount = rateOrDiscount;
        DiscountPercentage = discountPercentage;
        DiscountAmount = discountAmount;
        Rate = rate;
        MinQty = minQty;
        MaxQty = maxQty;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Priority = priority;
        WarehouseId = warehouseId;
        Description = description;
    }

    public override object[] GetKeys() => new object[] { Id };
}

/// <summary>Free-item (buy X get Y) discount slab. One Pricing Rule is generated per slab x target x party.</summary>
public class PromotionalSchemeProductDiscountSlab : Entity<Guid>
{
    public Guid PromotionalSchemeId { get; set; }

    public Guid FreeItemId { get; set; }
    public decimal FreeQty { get; set; }
    public decimal FreeItemRate { get; set; }
    /// <summary>When true, the free item is the same as the item that triggered the rule.</summary>
    public bool SameItem { get; set; }

    public decimal MinQty { get; set; }
    public decimal MaxQty { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int Priority { get; set; } = 1;
    public Guid? WarehouseId { get; set; }
    public string? Description { get; set; }

    /// <summary>Recursive: repeat the free-item award every RecurseFor units above MinQty/MinAmount.</summary>
    public bool IsRecursive { get; set; }
    public decimal RecurseFor { get; set; }
    public bool RoundFreeQty { get; set; }

    protected PromotionalSchemeProductDiscountSlab() { }

    public PromotionalSchemeProductDiscountSlab(Guid id, Guid promotionalSchemeId, Guid freeItemId, decimal freeQty,
        decimal freeItemRate, bool sameItem, decimal minQty, decimal maxQty, decimal minAmount, decimal maxAmount,
        int priority, Guid? warehouseId, string? description, bool isRecursive, decimal recurseFor, bool roundFreeQty) : base(id)
    {
        PromotionalSchemeId = promotionalSchemeId;
        FreeItemId = freeItemId;
        FreeQty = freeQty;
        FreeItemRate = freeItemRate;
        SameItem = sameItem;
        MinQty = minQty;
        MaxQty = maxQty;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Priority = priority;
        WarehouseId = warehouseId;
        Description = description;
        IsRecursive = isRecursive;
        RecurseFor = recurseFor;
        RoundFreeQty = roundFreeQty;
    }

    public override object[] GetKeys() => new object[] { Id };
}
