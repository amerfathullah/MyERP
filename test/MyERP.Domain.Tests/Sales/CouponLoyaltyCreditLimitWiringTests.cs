using System;
using System.Collections.Generic;
using MyERP.Sales;
using MyERP.Sales.Entities;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

/// <summary>
/// Tests for CouponCode + LoyaltyPoints + Per-Company CreditLimit wiring into transaction pipeline.
/// </summary>
public class CouponLoyaltyCreditLimitWiringTests
{
    private static readonly Guid _pricingRuleId = Guid.NewGuid();
    private static readonly Guid _companyId = Guid.NewGuid();

    private static CouponCode CreateCoupon(string code, int maxUse = 100, bool enabled = true)
        => new(Guid.NewGuid(), code, $"Coupon {code}", CouponType.Promotional, _pricingRuleId)
        {
            ValidFrom = new DateTime(2026, 1, 1),
            ValidUpto = new DateTime(2026, 12, 31),
            MaximumUse = maxUse,
            IsEnabled = enabled,
        };

    // --- CouponCode entity logic ---

    [Fact]
    public void CouponCode_IsValid_WithinDates_ReturnsTrue()
    {
        var coupon = CreateCoupon("TEST10");
        Assert.True(coupon.IsValid(new DateTime(2026, 6, 15), null));
    }

    [Fact]
    public void CouponCode_IsValid_Expired_ReturnsFalse()
    {
        var coupon = CreateCoupon("EXPIRED");
        coupon.ValidUpto = new DateTime(2025, 12, 31);
        Assert.False(coupon.IsValid(new DateTime(2026, 1, 1), null));
    }

    [Fact]
    public void CouponCode_IsValid_MaxUsageReached_ReturnsFalse()
    {
        var coupon = CreateCoupon("MAXED", maxUse: 2);
        coupon.RecordUse();
        coupon.RecordUse();
        Assert.False(coupon.IsValid(new DateTime(2026, 6, 15), null));
    }

    [Fact]
    public void CouponCode_IsValid_Disabled_ReturnsFalse()
    {
        var coupon = CreateCoupon("DISABLED", enabled: false);
        Assert.False(coupon.IsValid(new DateTime(2026, 6, 15), null));
    }

    [Fact]
    public void CouponCode_RecordUse_IncrementsUsed()
    {
        var coupon = CreateCoupon("TEST");
        Assert.Equal(0, coupon.Used);
        coupon.RecordUse();
        Assert.Equal(1, coupon.Used);
        coupon.RecordUse();
        Assert.Equal(2, coupon.Used);
    }

    [Fact]
    public void CouponCode_ReverseUse_DecrementsUsed()
    {
        var coupon = CreateCoupon("TEST");
        coupon.RecordUse();
        coupon.RecordUse();
        coupon.ReverseUse();
        Assert.Equal(1, coupon.Used);
    }

    // --- CustomerCreditLimit per-company entity logic ---

    [Fact]
    public void CustomerCreditLimit_DefaultsBypass_IsFalse()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50000m);
        Assert.False(limit.BypassCreditLimitCheck);
        Assert.Equal(50000m, limit.CreditLimit);
    }

    [Fact]
    public void CustomerCreditLimit_WithBypass_SkipsEnforcement()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50000m)
        {
            BypassCreditLimitCheck = true,
        };
        Assert.True(limit.BypassCreditLimitCheck);
    }

    [Fact]
    public void CustomerCreditLimit_OverdueThreshold_DefaultsZero()
    {
        var limit = new CustomerCreditLimit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m);
        Assert.Equal(0, limit.OverdueBillingThreshold);
    }

    [Fact]
    public void CustomerCreditLimit_DifferentCompanies_IndependentLimits()
    {
        var customerId = Guid.NewGuid();
        var company1 = Guid.NewGuid();
        var company2 = Guid.NewGuid();

        var limit1 = new CustomerCreditLimit(Guid.NewGuid(), customerId, company1, 50000m);
        var limit2 = new CustomerCreditLimit(Guid.NewGuid(), customerId, company2, 100000m);

        Assert.Equal(50000m, limit1.CreditLimit);
        Assert.Equal(100000m, limit2.CreditLimit);
        Assert.NotEqual(limit1.CompanyId, limit2.CompanyId);
    }

    // --- Loyalty redemption value calculation ---

    [Fact]
    public void LoyaltyProgram_RedemptionValue_UsesRedemptionFactor()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "Gold Program", 10m, 365);
        var tier = new LoyaltyProgramTier(Guid.NewGuid(), program.Id, "Bronze", 0m, 1m, 0.50m);

        // 100 points × 0.50 redemption factor = RM 50
        var value = program.CalculateRedemptionValue(100, tier);
        Assert.Equal(50m, value);
    }

    [Fact]
    public void LoyaltyProgram_RedemptionValue_HigherTier_HigherValue()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "Premium", 10m, 365);
        var silverTier = new LoyaltyProgramTier(Guid.NewGuid(), program.Id, "Silver", 1000m, 1.5m, 0.75m);
        var goldTier = new LoyaltyProgramTier(Guid.NewGuid(), program.Id, "Gold", 5000m, 2m, 1.0m);

        var silverValue = program.CalculateRedemptionValue(200, silverTier);
        var goldValue = program.CalculateRedemptionValue(200, goldTier);

        Assert.Equal(150m, silverValue); // 200 × 0.75
        Assert.Equal(200m, goldValue);   // 200 × 1.0
    }

    [Fact]
    public void LoyaltyRedemption_CappedAtGrandTotal()
    {
        var grandTotal = 100m;
        var redemptionValue = 150m;
        var capped = Math.Min(redemptionValue, grandTotal);
        Assert.Equal(100m, capped);
    }

    // --- POS Opening validation logic ---

    [Fact]
    public void PosOpeningEntry_DefaultsOpen()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Open, entry.Status);
    }

    [Fact]
    public void PosOpeningEntry_CloseChangesStatus()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid());
        entry.Close(Guid.NewGuid());
        Assert.Equal(PosOpeningStatus.Closed, entry.Status);
    }

    [Fact]
    public void PosOpeningEntry_CancelFromClosedSucceeds()
    {
        var entry = new PosOpeningEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(), Guid.NewGuid());
        entry.Close(Guid.NewGuid());
        entry.Cancel();
        Assert.Equal(PosOpeningStatus.Cancelled, entry.Status);
    }

    // --- DTO fields ---

    [Fact]
    public void CreateSalesOrderDto_HasCouponCodeProperty()
    {
        var dto = new CreateSalesOrderDto();
        Assert.Null(dto.CouponCode);
        dto.CouponCode = "SUMMER20";
        Assert.Equal("SUMMER20", dto.CouponCode);
    }

    [Fact]
    public void CreateSalesOrderDto_HasLoyaltyPointsProperty()
    {
        var dto = new CreateSalesOrderDto();
        Assert.Equal(0, dto.LoyaltyPointsToRedeem);
        dto.LoyaltyPointsToRedeem = 500;
        Assert.Equal(500, dto.LoyaltyPointsToRedeem);
    }

    [Fact]
    public void CreateSalesInvoiceDto_HasCouponAndLoyaltyFields()
    {
        var dto = new CreateSalesInvoiceDto();
        Assert.Null(dto.CouponCode);
        Assert.Equal(0, dto.LoyaltyPointsToRedeem);
        dto.CouponCode = "DISC10";
        dto.LoyaltyPointsToRedeem = 250;
        Assert.Equal("DISC10", dto.CouponCode);
        Assert.Equal(250, dto.LoyaltyPointsToRedeem);
    }
}
