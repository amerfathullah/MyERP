using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Coupon Code — promotional or gift card codes linked to Pricing Rules.
/// Promotional: auto-generated from first 8 non-digit uppercase chars of name.
/// Gift Card: random 10-char hex hash, forced maximum_use=1, customer required.
/// Maps to ERPNext accounts/doctype/coupon_code.
/// Per gotcha #165: auto-generation algorithm described in pricing-discounts instructions.
/// </summary>
public class CouponCode : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    /// <summary>The coupon code string (unique per tenant).</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Display name.</summary>
    public string CouponName { get; set; } = null!;

    /// <summary>Promotional or Gift Card.</summary>
    public CouponType CouponType { get; set; } = CouponType.Promotional;

    /// <summary>Linked pricing rule that provides the discount.</summary>
    public Guid PricingRuleId { get; set; }

    /// <summary>Maximum number of times this coupon can be used. 0 = unlimited.</summary>
    public int MaximumUse { get; set; }

    /// <summary>Number of times this coupon has been used.</summary>
    public int Used { get; private set; }

    /// <summary>Valid from date.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Valid until date.</summary>
    public DateTime? ValidUpto { get; set; }

    /// <summary>Required for Gift Card coupons.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>Maximum uses per customer (0 = no per-customer limit).</summary>
    public int MaximumUsePerCustomer { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string? Description { get; set; }

    protected CouponCode() { }

    public CouponCode(
        Guid id,
        string code,
        string couponName,
        CouponType couponType,
        Guid pricingRuleId,
        Guid? tenantId = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), 50);
        CouponName = Check.NotNullOrWhiteSpace(couponName, nameof(couponName), 200);
        CouponType = couponType;
        PricingRuleId = Check.NotDefaultOrNull<Guid>(pricingRuleId, nameof(pricingRuleId));
        TenantId = tenantId;

        // Gift cards always max_use=1 and require customer
        if (couponType == CouponType.GiftCard)
        {
            MaximumUse = 1;
        }
    }

    /// <summary>Generate promotional code from name: first 8 non-digit uppercase chars.</summary>
    public static string GeneratePromotionalCode(string name)
    {
        var chars = new System.Text.StringBuilder();
        foreach (var c in name.ToUpper())
        {
            if (!char.IsDigit(c) && char.IsLetterOrDigit(c) && chars.Length < 8)
                chars.Append(c);
        }
        return chars.Length > 0 ? chars.ToString() : name[..Math.Min(8, name.Length)].ToUpper();
    }

    /// <summary>Generate gift card code: random 10-char hex hash.</summary>
    public static string GenerateGiftCardCode()
    {
        return Guid.NewGuid().ToString("N")[..10].ToUpper();
    }

    /// <summary>Record a use of this coupon. Validates against maximum usage.</summary>
    public void RecordUse()
    {
        if (MaximumUse > 0 && Used >= MaximumUse)
            throw new BusinessException("MyERP:03017")
                .WithData("couponCode", Code)
                .WithData("maxUse", MaximumUse);

        Used++;
    }

    /// <summary>Reverse a coupon use (e.g., when invoice is cancelled).</summary>
    public void ReverseUse()
    {
        Used = Math.Max(0, Used - 1);
    }

    /// <summary>Check if coupon is currently valid.</summary>
    public bool IsValid(DateTime asOfDate, Guid? customerId = null)
    {
        if (!IsEnabled) return false;
        if (ValidFrom.HasValue && asOfDate < ValidFrom.Value) return false;
        if (ValidUpto.HasValue && asOfDate > ValidUpto.Value) return false;
        if (MaximumUse > 0 && Used >= MaximumUse) return false;
        if (CouponType == CouponType.GiftCard && customerId != CustomerId) return false;
        return true;
    }
}

public enum CouponType
{
    Promotional = 0,
    GiftCard = 1
}
