using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Sales.Entities;

/// <summary>
/// Loyalty Program — defines how customers earn and redeem loyalty points.
/// Points are earned on Sales Invoice submit and redeemed as invoice discounts.
/// 
/// Per ERPNext:
/// - Tiers sorted by min_spent ASC; lowest tier MUST have min_spent = 0
/// - Current transaction IS included in tier determination
/// - Points calculation: FLOOR(grand_total / conversion_factor) × tier.collection_factor
/// - FIFO redemption of points (oldest first)
/// - Cannot cancel earning invoice if points already redeemed
/// 
/// Source: erpnext/accounts/doctype/loyalty_program/loyalty_program.py
/// </summary>
public class LoyaltyProgram : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Program name (e.g., "MyERP Rewards").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Points to currency conversion factor. Points = FLOOR(amount / ConversionFactor).</summary>
    public decimal ConversionFactor { get; set; }

    /// <summary>Number of days before earned points expire. 0 = never expires.</summary>
    public int ExpiryDurationDays { get; set; }

    /// <summary>GL account for loyalty expense on redemption.</summary>
    public Guid? ExpenseAccountId { get; set; }

    /// <summary>Cost center for loyalty GL entries.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Whether this program is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Tiers within this program (higher spend = better rates).</summary>
    public ICollection<LoyaltyProgramTier> Tiers { get; private set; }
        = new List<LoyaltyProgramTier>();

    protected LoyaltyProgram() { }

    public LoyaltyProgram(
        Guid id,
        Guid companyId,
        string name,
        decimal conversionFactor,
        int expiryDurationDays = 365,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));

        if (conversionFactor <= 0)
            throw new BusinessException("MyERP:03008")
                .WithData("value", conversionFactor);

        ConversionFactor = conversionFactor;
        ExpiryDurationDays = expiryDurationDays;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a tier to the program. Lowest tier must have minSpent = 0.
    /// </summary>
    public void AddTier(string tierName, decimal minSpent, decimal collectionFactor, decimal redemptionFactor)
    {
        Tiers.Add(new LoyaltyProgramTier(
            Guid.NewGuid(), Id, tierName, minSpent, collectionFactor, redemptionFactor));
    }

    /// <summary>
    /// Determine which tier a customer qualifies for based on total spend.
    /// Includes current transaction amount in calculation.
    /// </summary>
    public LoyaltyProgramTier DetermineTier(decimal totalSpent, decimal currentTransactionAmount)
    {
        var combinedSpend = totalSpent + currentTransactionAmount;
        var orderedTiers = Tiers.OrderBy(t => t.MinSpent).ToList();

        if (!orderedTiers.Any())
            throw new BusinessException("MyERP:03009")
                .WithData("program", Name);

        LoyaltyProgramTier qualifyingTier = orderedTiers[0]; // Default to lowest

        foreach (var tier in orderedTiers)
        {
            if (combinedSpend >= tier.MinSpent)
                qualifyingTier = tier;
            else
                break; // Break on first non-qualifying tier (ascending order)
        }

        return qualifyingTier;
    }

    /// <summary>
    /// Calculate points earned for a transaction amount.
    /// </summary>
    public int CalculatePointsEarned(decimal eligibleAmount, LoyaltyProgramTier tier)
    {
        if (eligibleAmount <= 0) return 0;
        return (int)(Math.Floor(eligibleAmount / ConversionFactor) * tier.CollectionFactor);
    }

    /// <summary>
    /// Calculate the redemption value (currency amount) for a number of points.
    /// </summary>
    public decimal CalculateRedemptionValue(int points, LoyaltyProgramTier tier)
    {
        return points * tier.RedemptionFactor;
    }

    /// <summary>
    /// Validate the program configuration.
    /// </summary>
    public void Validate()
    {
        if (!Tiers.Any())
            throw new BusinessException("MyERP:03009")
                .WithData("program", Name);

        // Lowest tier must have min_spent = 0
        var lowestTier = Tiers.OrderBy(t => t.MinSpent).First();
        if (lowestTier.MinSpent != 0)
        {
            throw new BusinessException("MyERP:03010")
                .WithData("program", Name);
        }
    }
}

/// <summary>
/// A tier within a loyalty program. Higher tiers have better collection/redemption rates.
/// </summary>
public class LoyaltyProgramTier : Entity<Guid>
{
    public Guid LoyaltyProgramId { get; set; }
    public string TierName { get; set; } = null!;

    /// <summary>Minimum cumulative spend to qualify for this tier.</summary>
    public decimal MinSpent { get; set; }

    /// <summary>Multiplier for points earned (e.g., 1 = standard, 2 = double points).</summary>
    public decimal CollectionFactor { get; set; }

    /// <summary>Currency value per point when redeeming (e.g., 0.01 = 1 point = RM 0.01).</summary>
    public decimal RedemptionFactor { get; set; }

    protected LoyaltyProgramTier() { }

    public LoyaltyProgramTier(Guid id, Guid programId, string tierName,
        decimal minSpent, decimal collectionFactor, decimal redemptionFactor) : base(id)
    {
        LoyaltyProgramId = programId;
        TierName = tierName;
        MinSpent = minSpent;
        CollectionFactor = collectionFactor;
        RedemptionFactor = redemptionFactor;
    }
}

/// <summary>
/// Records points earned or redeemed by a customer.
/// Positive points = earned, negative = redeemed.
/// </summary>
public class LoyaltyPointEntry : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid LoyaltyProgramId { get; set; }

    /// <summary>Points earned (positive) or redeemed (negative).</summary>
    public int Points { get; set; }

    /// <summary>Date the points were earned/redeemed.</summary>
    public DateTime PostingDate { get; set; }

    /// <summary>Expiry date for earned points. Null = never expires.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Source invoice type (e.g., "SalesInvoice").</summary>
    public string? InvoiceType { get; set; }

    /// <summary>Source invoice ID.</summary>
    public Guid? InvoiceId { get; set; }

    /// <summary>For redemptions: the original earn entry being consumed (FIFO).</summary>
    public Guid? RedeemAgainstId { get; set; }

    /// <summary>Tier at the time of earning.</summary>
    public string? TierName { get; set; }

    protected LoyaltyPointEntry() { }

    public LoyaltyPointEntry(
        Guid id,
        Guid companyId,
        Guid customerId,
        Guid loyaltyProgramId,
        int points,
        DateTime postingDate,
        DateTime? expiryDate = null,
        Guid? tenantId = null) : base(id)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        LoyaltyProgramId = loyaltyProgramId;
        Points = points;
        PostingDate = postingDate;
        ExpiryDate = expiryDate;
        TenantId = tenantId;
    }

    /// <summary>Whether this entry's points have expired.</summary>
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow.Date;

    /// <summary>Whether this is an earning entry (positive points).</summary>
    public bool IsEarning => Points > 0;

    /// <summary>Whether this is a redemption entry (negative points).</summary>
    public bool IsRedemption => Points < 0;
}
