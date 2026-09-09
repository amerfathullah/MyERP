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
/// Loyalty Point Service — manages earning and redemption of loyalty points.
/// 
/// Per ERPNext:
/// - Points earned on SI submit: FLOOR(eligible_amount / conversion_factor) × collection_factor
/// - Eligible amount = grand_total - loyalty_amount - returned_amount
/// - Redemption uses FIFO (oldest non-expired points consumed first)
/// - Cancel guard: can't cancel earning SI if points already redeemed
/// - Tier re-evaluation after earn/delete can demote customers
/// 
/// Source: erpnext/accounts/doctype/loyalty_program/loyalty_program.py
/// </summary>
public class LoyaltyPointService : DomainService
{
    private readonly IRepository<LoyaltyProgram, Guid> _programRepository;
    private readonly IRepository<LoyaltyPointEntry, Guid> _entryRepository;

    public LoyaltyPointService(
        IRepository<LoyaltyProgram, Guid> programRepository,
        IRepository<LoyaltyPointEntry, Guid> entryRepository)
    {
        _programRepository = programRepository;
        _entryRepository = entryRepository;
    }

    /// <summary>
    /// Earn loyalty points for a transaction.
    /// </summary>
    public async Task<LoyaltyPointEntry?> EarnPointsAsync(
        Guid loyaltyProgramId,
        Guid customerId,
        Guid companyId,
        decimal eligibleAmount,
        decimal totalSpent,
        DateTime postingDate,
        string? invoiceType = null,
        Guid? invoiceId = null,
        Guid? tenantId = null)
    {
        var program = await _programRepository.GetAsync(loyaltyProgramId);
        if (!program.IsEnabled) return null;

        var tier = program.DetermineTier(totalSpent, eligibleAmount);
        var points = program.CalculatePointsEarned(eligibleAmount, tier);

        if (points <= 0) return null;

        DateTime? expiryDate = program.ExpiryDurationDays > 0
            ? postingDate.AddDays(program.ExpiryDurationDays)
            : null;

        var entry = new LoyaltyPointEntry(
            Guid.NewGuid(), companyId, customerId, loyaltyProgramId,
            points, postingDate, expiryDate, tenantId)
        {
            InvoiceType = invoiceType,
            InvoiceId = invoiceId,
            TierName = tier.TierName
        };

        await _entryRepository.InsertAsync(entry);
        return entry;
    }

    /// <summary>
    /// Redeem loyalty points. Uses FIFO (oldest non-expired earn entry first), splitting the
    /// redemption across as many earn entries as needed and stamping each split's RedeemAgainstId —
    /// this is what lets <see cref="HasPointsBeenRedeemedAsync"/> actually find a match; a single
    /// unlinked redemption entry (the previous behavior) left that cancel-guard permanently dead.
    /// Returns the total currency value of redeemed points.
    /// </summary>
    public async Task<LoyaltyRedemptionResult> RedeemPointsAsync(
        Guid loyaltyProgramId,
        Guid customerId,
        int pointsToRedeem,
        DateTime postingDate,
        Guid companyId,
        string? invoiceType = null,
        Guid? invoiceId = null,
        Guid? tenantId = null)
    {
        var program = await _programRepository.GetAsync(loyaltyProgramId);
        var availablePoints = await GetAvailablePointsAsync(customerId, loyaltyProgramId, postingDate);

        if (pointsToRedeem > availablePoints)
        {
            throw new BusinessException("MyERP:03011")
                .WithData("requested", pointsToRedeem)
                .WithData("available", availablePoints);
        }

        // Determine tier for redemption value
        var totalSpent = await GetTotalSpentAsync(customerId, loyaltyProgramId);
        var tier = program.DetermineTier(totalSpent, 0);

        var query = await _entryRepository.GetQueryableAsync();
        var earnEntries = query
            .Where(e => e.CustomerId == customerId
                && e.LoyaltyProgramId == loyaltyProgramId
                && e.Points > 0
                && e.PostingDate <= postingDate
                && (!e.ExpiryDate.HasValue || e.ExpiryDate.Value >= postingDate))
            .OrderBy(e => e.PostingDate)
            .ToList();

        var remaining = pointsToRedeem;
        Guid lastEntryId = Guid.Empty;

        foreach (var earnEntry in earnEntries)
        {
            if (remaining <= 0) break;

            var alreadyRedeemed = query
                .Where(e => e.RedeemAgainstId == earnEntry.Id && e.Points < 0)
                .Sum(e => -e.Points);
            var availableFromEntry = earnEntry.Points - alreadyRedeemed;
            if (availableFromEntry <= 0) continue;

            var redeemFromEntry = Math.Min(availableFromEntry, remaining);

            var redemptionEntry = new LoyaltyPointEntry(
                Guid.NewGuid(), companyId, customerId, loyaltyProgramId,
                -redeemFromEntry, postingDate, earnEntry.ExpiryDate, tenantId)
            {
                InvoiceType = invoiceType,
                InvoiceId = invoiceId,
                TierName = earnEntry.TierName,
                RedeemAgainstId = earnEntry.Id
            };

            await _entryRepository.InsertAsync(redemptionEntry);
            lastEntryId = redemptionEntry.Id;
            remaining -= redeemFromEntry;
        }

        var redemptionValue = program.CalculateRedemptionValue(pointsToRedeem, tier);

        return new LoyaltyRedemptionResult
        {
            PointsRedeemed = pointsToRedeem,
            RedemptionValue = redemptionValue,
            EntryId = lastEntryId
        };
    }

    /// <summary>
    /// Get available (non-expired, non-redeemed) points for a customer.
    /// </summary>
    public async Task<int> GetAvailablePointsAsync(
        Guid customerId, Guid loyaltyProgramId, DateTime asOfDate)
    {
        var query = await _entryRepository.GetQueryableAsync();
        var totalEarned = query
            .Where(e => e.CustomerId == customerId
                && e.LoyaltyProgramId == loyaltyProgramId
                && e.Points > 0
                && e.PostingDate <= asOfDate
                && (!e.ExpiryDate.HasValue || e.ExpiryDate.Value >= asOfDate))
            .Sum(e => e.Points);

        var totalRedeemed = query
            .Where(e => e.CustomerId == customerId
                && e.LoyaltyProgramId == loyaltyProgramId
                && e.Points < 0)
            .Sum(e => Math.Abs(e.Points));

        return Math.Max(0, totalEarned - totalRedeemed);
    }

    /// <summary>
    /// Check if an earning entry's points have been redeemed (cancel guard).
    /// </summary>
    public async Task<bool> HasPointsBeenRedeemedAsync(Guid invoiceId, string invoiceType)
    {
        var query = await _entryRepository.GetQueryableAsync();

        // Find the earning entry for this invoice
        var earnEntry = query.FirstOrDefault(e =>
            e.InvoiceId == invoiceId && e.InvoiceType == invoiceType && e.Points > 0);

        if (earnEntry == null) return false;

        // Check if any redemption references this entry
        var hasRedemption = query.Any(e =>
            e.RedeemAgainstId == earnEntry.Id && e.Points < 0);

        return hasRedemption;
    }

    /// <summary>
    /// Get the customer's current tier name.
    /// </summary>
    public async Task<string?> GetCurrentTierAsync(Guid customerId, Guid loyaltyProgramId)
    {
        var program = await _programRepository.GetAsync(loyaltyProgramId);
        var totalSpent = await GetTotalSpentAsync(customerId, loyaltyProgramId);
        var tier = program.DetermineTier(totalSpent, 0);
        return tier.TierName;
    }

    private async Task<decimal> GetTotalSpentAsync(Guid customerId, Guid loyaltyProgramId)
    {
        // Total spent is approximated by sum of earned points × conversion factor
        // In production, this would query actual invoice totals
        var query = await _entryRepository.GetQueryableAsync();
        var totalPoints = query
            .Where(e => e.CustomerId == customerId
                && e.LoyaltyProgramId == loyaltyProgramId
                && e.Points > 0)
            .Sum(e => e.Points);

        var program = await _programRepository.GetAsync(loyaltyProgramId);
        return totalPoints * program.ConversionFactor;
    }
}

/// <summary>
/// Result of a points redemption operation.
/// </summary>
public class LoyaltyRedemptionResult
{
    public int PointsRedeemed { get; set; }
    public decimal RedemptionValue { get; set; }
    public Guid EntryId { get; set; }
}
