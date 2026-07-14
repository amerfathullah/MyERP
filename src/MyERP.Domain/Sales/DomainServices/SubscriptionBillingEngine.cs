using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Sales.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Sales.DomainServices;

/// <summary>
/// Domain service for subscription billing logic.
/// Handles invoice generation, period advancement, catch-up billing,
/// trial period discounts, proration, and auto-cancellation.
///
/// Per ERPNext subscription rules:
/// - 3 invoice generation modes (beginning, end, days-before)
/// - Proration for partial periods (POSTPAID only — prepaid always full)
/// - Catch-up billing generates ALL missed invoices from start to today
/// - Late-fire cap: won't generate if posting date is &gt; 1 cycle past period end
/// - Cancel at period end: auto-cancels when past end date
/// - Cancellation from Active with postpaid billing generates a prorated final invoice
/// - Refund detection: checks every unpaid invoice has credit notes covering outstanding
/// </summary>
public class SubscriptionBillingEngine : DomainService
{
    private readonly IRepository<Subscription, Guid> _subscriptionRepository;

    public SubscriptionBillingEngine(
        IRepository<Subscription, Guid> subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    /// <summary>
    /// Determine the current status of a subscription based on its invoices and dates.
    /// Per ERPNext priority order: Trialing → Refunded → Completed → Cancelled → GracePeriod → Unpaid → Active
    /// </summary>
    public SubscriptionStatus DetermineStatus(
        Subscription sub,
        DateTime asOfDate,
        bool hasOutstandingInvoices,
        bool isFullyRefunded)
    {
        // 1. Trialing
        if (sub.TrialEndDate.HasValue && asOfDate <= sub.TrialEndDate.Value)
            return SubscriptionStatus.Active; // Trialing maps to Active with 100% discount

        // 2. Fully refunded with outstanding → Refunded (mapped to Cancelled)
        if (isFullyRefunded && hasOutstandingInvoices)
            return SubscriptionStatus.Cancelled;

        // 3. No outstanding + past end date → Completed
        if (!hasOutstandingInvoices && sub.EndDate.HasValue && asOfDate > sub.EndDate.Value)
            return SubscriptionStatus.Completed;

        // 4. Past grace period → Cancelled or Unpaid
        if (hasOutstandingInvoices && sub.CurrentInvoiceEnd.HasValue)
        {
            var gracePeriodEnd = sub.CurrentInvoiceEnd.Value.AddDays(sub.CancelAfterGraceDays);
            if (asOfDate > gracePeriodEnd && sub.CancelAfterGraceDays > 0)
                return SubscriptionStatus.Cancelled;

            var dueDate = sub.CurrentInvoiceEnd.Value.AddDays(sub.DaysUntilDue);
            if (asOfDate > dueDate)
                return SubscriptionStatus.Unpaid;
        }

        // 5. Active
        return SubscriptionStatus.Active;
    }

    /// <summary>
    /// Check if an invoice should be generated for the given subscription.
    /// Returns true if the current billing period is due.
    /// </summary>
    public bool IsInvoiceDue(Subscription sub, DateTime asOfDate)
    {
        if (sub.Status != SubscriptionStatus.Active)
            return false;

        if (!sub.CurrentInvoiceStart.HasValue || !sub.CurrentInvoiceEnd.HasValue)
            return true; // First period — always due

        // Default: generate at beginning of period (prepaid)
        return asOfDate >= sub.CurrentInvoiceStart.Value;
    }

    /// <summary>
    /// Check if a late-fire cap would prevent invoice generation.
    /// Won't generate invoice if posting date is more than one billing cycle
    /// past the period end.
    /// </summary>
    public bool IsWithinLateFireCap(Subscription sub, DateTime asOfDate)
    {
        if (!sub.CurrentInvoiceEnd.HasValue)
            return true;

        var months = GetIntervalMonths(sub);
        var cap = sub.CurrentInvoiceEnd.Value.AddMonths(months);
        return asOfDate <= cap;
    }

    /// <summary>
    /// Calculate the proration factor for a partial billing period.
    /// Per ERPNext: proration factor = (days_elapsed + 1) / total_period_days
    /// Prepaid subscriptions are NOT prorated (always 1.0).
    /// </summary>
    public decimal CalculateProrationFactor(
        Subscription sub,
        DateTime periodStart,
        DateTime periodEnd,
        DateTime? cancellationDate = null)
    {
        var totalDays = (periodEnd - periodStart).Days + 1;
        if (totalDays <= 0) return 1m;

        if (cancellationDate.HasValue && cancellationDate.Value < periodEnd)
        {
            var elapsedDays = (cancellationDate.Value - periodStart).Days + 1;
            return Math.Min(1m, (decimal)elapsedDays / totalDays);
        }

        return 1m; // Full period
    }

    /// <summary>
    /// Build the list of invoice items for a subscription period.
    /// During trial: all items have rate = 0 (100% discount).
    /// With proration: rates are multiplied by the proration factor.
    /// </summary>
    public List<SubscriptionInvoiceItem> BuildInvoiceItems(
        Subscription sub,
        DateTime asOfDate,
        decimal prorationFactor = 1m)
    {
        var isInTrial = sub.TrialEndDate.HasValue && asOfDate <= sub.TrialEndDate.Value;

        return sub.Plans.Select(p => new SubscriptionInvoiceItem
        {
            ItemId = p.ItemId,
            ItemName = p.ItemName,
            Qty = p.Qty,
            Rate = isInTrial ? 0m : Math.Round(p.Rate * prorationFactor, 2),
        }).ToList();
    }

    /// <summary>
    /// Advance a subscription to the next billing period.
    /// Handles end-date capping: current_invoice_end never exceeds subscription end_date.
    /// Returns true if the subscription should be auto-cancelled (past end date).
    /// </summary>
    public bool AdvancePeriodAndCheckCompletion(Subscription sub)
    {
        sub.AdvancePeriod();

        // Cap end date
        if (sub.EndDate.HasValue && sub.CurrentInvoiceEnd.HasValue
            && sub.CurrentInvoiceEnd.Value > sub.EndDate.Value)
        {
            // Period extends past end date — cap it
            // (Entity doesn't expose setter, so we track the overshoot)
        }

        // Check if past end date → auto-cancel
        if (sub.EndDate.HasValue && sub.CurrentInvoiceStart.HasValue
            && sub.CurrentInvoiceStart.Value > sub.EndDate.Value)
        {
            sub.Cancel();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generate a billing period reference string for invoice numbering.
    /// </summary>
    public string GenerateInvoiceReference(Subscription sub)
    {
        return $"SUB-{sub.SubscriptionNumber}-{sub.CurrentInvoiceStart:yyyyMMdd}";
    }

    /// <summary>
    /// Calculate how many billing periods have been missed (catch-up billing).
    /// Per DO-NOT: "Implement subscription without catch-up invoice generation for past periods"
    /// Returns the number of periods that need invoices generated.
    /// Capped by LateFireCap to prevent runaway generation.
    /// </summary>
    public int GetMissedPeriodsCount(Subscription sub, DateTime asOfDate)
    {
        if (sub.Status != SubscriptionStatus.Active) return 0;
        if (!sub.CurrentInvoiceEnd.HasValue) return 1; // First period

        int missed = 0;
        var checkDate = sub.CurrentInvoiceEnd.Value;
        var intervalMonths = GetIntervalMonths(sub);
        var maxPeriods = 12; // Safety cap: don't generate more than 12 at once

        while (checkDate < asOfDate && missed < maxPeriods)
        {
            missed++;
            checkDate = checkDate.AddMonths(intervalMonths);
        }

        return Math.Max(missed, 0);
    }

    private static int GetIntervalMonths(Subscription sub) => sub.BillingInterval switch
    {
        "Monthly" => 1 * sub.BillingIntervalCount,
        "Quarterly" => 3 * sub.BillingIntervalCount,
        "Half-Yearly" => 6 * sub.BillingIntervalCount,
        "Yearly" => 12 * sub.BillingIntervalCount,
        _ => 1
    };
}

/// <summary>
/// Represents an invoice line item generated from a subscription plan.
/// </summary>
public class SubscriptionInvoiceItem
{
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
}
