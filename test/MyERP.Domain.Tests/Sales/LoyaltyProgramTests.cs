using System;
using System.Linq;
using MyERP.Sales.Entities;
using Volo.Abp;
using Xunit;

namespace MyERP.Domain.Tests.Sales;

public class LoyaltyProgramTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    private LoyaltyProgram CreateProgram(decimal conversionFactor = 10m, int expiryDays = 365)
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "MyERP Rewards", conversionFactor, expiryDays);
        program.AddTier("Bronze", 0m, 1m, 0.01m);       // Base: 1x earn, RM 0.01/point
        program.AddTier("Silver", 1000m, 1.5m, 0.015m); // 1.5x earn, RM 0.015/point
        program.AddTier("Gold", 5000m, 2m, 0.02m);      // 2x earn, RM 0.02/point
        return program;
    }

    [Fact]
    public void Program_DefaultState()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "Test", 10m);
        Assert.True(program.IsEnabled);
        Assert.Equal(365, program.ExpiryDurationDays);
        Assert.Equal(10m, program.ConversionFactor);
        Assert.Empty(program.Tiers);
    }

    [Fact]
    public void Program_InvalidConversionFactor_Throws()
    {
        Assert.Throws<BusinessException>(() =>
            new LoyaltyProgram(Guid.NewGuid(), _companyId, "Test", 0m));
        Assert.Throws<BusinessException>(() =>
            new LoyaltyProgram(Guid.NewGuid(), _companyId, "Test", -5m));
    }

    [Fact]
    public void Program_DetermineTier_LowestTier()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 500m); // total 500, qualifies for Bronze only
        Assert.Equal("Bronze", tier.TierName);
    }

    [Fact]
    public void Program_DetermineTier_IncludesCurrentTransaction()
    {
        var program = CreateProgram();
        // Total spent = 800, current transaction = 300 → combined = 1100 → Silver
        var tier = program.DetermineTier(800m, 300m);
        Assert.Equal("Silver", tier.TierName);
    }

    [Fact]
    public void Program_DetermineTier_HighestQualifying()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(5000m, 100m); // 5100 → Gold
        Assert.Equal("Gold", tier.TierName);
    }

    [Fact]
    public void Program_DetermineTier_ExactBoundary()
    {
        var program = CreateProgram();
        var tier = program.DetermineTier(0m, 1000m); // Exactly 1000 = Silver threshold
        Assert.Equal("Silver", tier.TierName);
    }

    [Fact]
    public void Program_CalculatePoints_BasicEarning()
    {
        var program = CreateProgram(conversionFactor: 10m);
        var bronzeTier = program.Tiers.First(t => t.TierName == "Bronze");

        // RM 500 / 10 = 50 base points × 1 (Bronze factor) = 50
        Assert.Equal(50, program.CalculatePointsEarned(500m, bronzeTier));
    }

    [Fact]
    public void Program_CalculatePoints_SilverMultiplier()
    {
        var program = CreateProgram(conversionFactor: 10m);
        var silverTier = program.Tiers.First(t => t.TierName == "Silver");

        // RM 500 / 10 = 50 base × 1.5 (Silver) = 75
        Assert.Equal(75, program.CalculatePointsEarned(500m, silverTier));
    }

    [Fact]
    public void Program_CalculatePoints_FloorRounding()
    {
        var program = CreateProgram(conversionFactor: 10m);
        var bronzeTier = program.Tiers.First(t => t.TierName == "Bronze");

        // RM 99 / 10 = 9.9 → FLOOR = 9 × 1 = 9
        Assert.Equal(9, program.CalculatePointsEarned(99m, bronzeTier));
    }

    [Fact]
    public void Program_CalculatePoints_ZeroAmount_ReturnsZero()
    {
        var program = CreateProgram();
        var tier = program.Tiers.First();
        Assert.Equal(0, program.CalculatePointsEarned(0m, tier));
    }

    [Fact]
    public void Program_CalculatePoints_NegativeAmount_ReturnsZero()
    {
        var program = CreateProgram();
        var tier = program.Tiers.First();
        Assert.Equal(0, program.CalculatePointsEarned(-100m, tier));
    }

    [Fact]
    public void Program_CalculateRedemptionValue()
    {
        var program = CreateProgram();
        var goldTier = program.Tiers.First(t => t.TierName == "Gold");

        // 1000 points × RM 0.02/point = RM 20
        Assert.Equal(20m, program.CalculateRedemptionValue(1000, goldTier));
    }

    [Fact]
    public void Program_Validate_NoTiers_Throws()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "Empty", 10m);
        var ex = Assert.Throws<BusinessException>(() => program.Validate());
        Assert.Equal("MyERP:03009", ex.Code);
    }

    [Fact]
    public void Program_Validate_LowestTierNotZero_Throws()
    {
        var program = new LoyaltyProgram(Guid.NewGuid(), _companyId, "Bad", 10m);
        program.AddTier("Silver", 1000m, 1.5m, 0.015m); // Lowest is 1000, not 0

        var ex = Assert.Throws<BusinessException>(() => program.Validate());
        Assert.Equal("MyERP:03010", ex.Code);
    }

    [Fact]
    public void Program_Validate_ValidConfig()
    {
        var program = CreateProgram();
        program.Validate(); // Should not throw
    }

    [Fact]
    public void PointEntry_EarnEntry()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            Guid.NewGuid(), 100, DateTime.Today, DateTime.Today.AddDays(365));

        Assert.True(entry.IsEarning);
        Assert.False(entry.IsRedemption);
        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void PointEntry_RedemptionEntry()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            Guid.NewGuid(), -50, DateTime.Today);

        Assert.False(entry.IsEarning);
        Assert.True(entry.IsRedemption);
    }

    [Fact]
    public void PointEntry_ExpiredEntry()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            Guid.NewGuid(), 100, DateTime.Today.AddDays(-400),
            DateTime.Today.AddDays(-35)); // Expired 35 days ago

        Assert.True(entry.IsExpired);
    }

    [Fact]
    public void PointEntry_NotExpired_NullExpiryDate()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            Guid.NewGuid(), 100, DateTime.Today, expiryDate: null);

        Assert.False(entry.IsExpired); // No expiry = never expires
    }

    [Fact]
    public void PointEntry_NotExpired_FutureDate()
    {
        var entry = new LoyaltyPointEntry(Guid.NewGuid(), _companyId, Guid.NewGuid(),
            Guid.NewGuid(), 100, DateTime.Today,
            DateTime.Today.AddDays(100)); // Expires in 100 days

        Assert.False(entry.IsExpired);
    }

    [Fact]
    public void Tier_Properties()
    {
        var tier = new LoyaltyProgramTier(Guid.NewGuid(), Guid.NewGuid(),
            "Platinum", 10000m, 3m, 0.03m);

        Assert.Equal("Platinum", tier.TierName);
        Assert.Equal(10000m, tier.MinSpent);
        Assert.Equal(3m, tier.CollectionFactor);
        Assert.Equal(0.03m, tier.RedemptionFactor);
    }
}
