using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Validates payment schedule integrity on invoices.
/// Per ERPNext accounts_controller.py Step 8: validate_payment_schedule_dates,
/// validate_payment_schedule_amount, set_due_date.
/// </summary>
public class PaymentScheduleValidationService : DomainService
{
    /// <summary>
    /// Validates a payment schedule for an invoice.
    /// Checks: portions sum to 100%, amounts sum to grand total, due dates ≥ posting date.
    /// </summary>
    public PaymentScheduleValidationResult Validate(
        IReadOnlyList<PaymentScheduleInput> entries,
        decimal grandTotal,
        DateTime postingDate)
    {
        var result = new PaymentScheduleValidationResult();

        if (entries == null || entries.Count == 0)
        {
            result.IsValid = true;
            return result;
        }

        // Check 1: Portions must sum to exactly 100%
        var totalPortion = entries.Sum(e => e.InvoicePortion);
        if (Math.Abs(totalPortion - 100m) > 0.01m)
        {
            result.IsValid = false;
            result.Errors.Add($"Payment term portions must sum to 100%, currently {totalPortion:F2}%");
        }

        // Check 2: Amounts must sum to grand total (within tolerance)
        var totalAmount = entries.Sum(e => e.PaymentAmount);
        var tolerance = 1m / (decimal)Math.Pow(10, 2); // 0.01
        if (Math.Abs(totalAmount - grandTotal) > tolerance)
        {
            result.IsValid = false;
            result.Errors.Add(
                $"Payment schedule total ({totalAmount:F2}) does not match grand total ({grandTotal:F2})");
        }

        // Check 3: All due dates must be ≥ posting date (per DO-NOT: due date floor rule)
        foreach (var entry in entries)
        {
            if (entry.DueDate < postingDate)
            {
                result.IsValid = false;
                result.Errors.Add(
                    $"Due date {entry.DueDate:yyyy-MM-dd} is before posting date {postingDate:yyyy-MM-dd}");
            }
        }

        // Check 4: All portions must be > 0
        foreach (var entry in entries)
        {
            if (entry.InvoicePortion <= 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Payment term portion must be positive, got {entry.InvoicePortion:F2}%");
            }
        }

        if (result.Errors.Count == 0)
            result.IsValid = true;

        return result;
    }

    /// <summary>
    /// Resolves the invoice due date from a payment schedule.
    /// Returns the MAXIMUM due date across all terms (latest payment date).
    /// Per ERPNext: set_due_date uses max(payment_schedule.due_date).
    /// </summary>
    public DateTime ResolveDueDate(IReadOnlyList<PaymentScheduleInput> entries, DateTime postingDate)
    {
        if (entries == null || entries.Count == 0)
            return postingDate;

        var maxDueDate = entries.Max(e => e.DueDate);
        // Per DO-NOT: due date floor rule — never before posting date
        return maxDueDate < postingDate ? postingDate : maxDueDate;
    }

    /// <summary>
    /// Recalculates payment schedule amounts when grand total changes (e.g., after discount).
    /// Preserves original proportions, adjusts amounts, remainder to last portioned entry.
    /// Per ERPNext commits 0953a16dba, 4bd25cf1df, &amp; 240a3a97a3:
    /// - Zero-portion rows preserve PaymentAmount and compute BasePaymentAmount from PaymentAmount × exchangeRate.
    /// - keepSettled=true preserves PaidAmount so Outstanding and BaseOutstanding retain settled amounts.
    /// - Preserves DiscountType, DiscountValidTill, ModeOfPaymentId, and scales fixed Amount discounts proportionally.
    /// </summary>
    public IReadOnlyList<RecalculatedScheduleEntry> RecalculateAmounts(
        IReadOnlyList<PaymentScheduleInput> entries,
        decimal newGrandTotal,
        decimal exchangeRate = 1m,
        bool keepSettled = false)
    {
        if (entries == null || entries.Count == 0)
            return Array.Empty<RecalculatedScheduleEntry>();

        var effectiveRate = exchangeRate > 0 ? exchangeRate : 1m;
        var baseGrandTotal = Math.Round(newGrandTotal * effectiveRate, 2);

        var fixedTotal = entries.Where(e => e.InvoicePortion == 0m).Sum(e => e.PaymentAmount);
        var fixedBaseTotal = entries.Where(e => e.InvoicePortion == 0m).Sum(e => Math.Round(e.PaymentAmount * effectiveRate, 2));

        var lastPortionIdx = -1;
        for (int idx = 0; idx < entries.Count; idx++)
        {
            if (entries[idx].InvoicePortion > 0m)
                lastPortionIdx = idx;
        }

        var results = new List<RecalculatedScheduleEntry>(entries.Count);
        var totalAllocated = 0m;
        var totalBaseAllocated = 0m;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            decimal amount;
            decimal baseAmount;

            if (entry.InvoicePortion == 0m)
            {
                amount = entry.PaymentAmount;
                baseAmount = Math.Round(amount * effectiveRate, 2);
            }
            else if (i == lastPortionIdx)
            {
                // Last portioned entry absorbs rounding remainder
                amount = newGrandTotal - fixedTotal - totalAllocated;
                baseAmount = baseGrandTotal - fixedBaseTotal - totalBaseAllocated;
            }
            else
            {
                amount = Math.Round(newGrandTotal * entry.InvoicePortion / 100m, 2);
                baseAmount = Math.Round(baseGrandTotal * entry.InvoicePortion / 100m, 2);
                totalAllocated += amount;
                totalBaseAllocated += baseAmount;
            }

            var paidAmount = keepSettled ? entry.PaidAmount : 0m;

            var discountValue = entry.DiscountPercentage;
            if (entry.DiscountType == "Amount" && entry.DiscountPercentage > 0m
                && entry.InvoicePortion > 0m && entry.PaymentAmount > 0m && entry.PaymentAmount != amount)
            {
                discountValue = Math.Round(entry.DiscountPercentage * amount / entry.PaymentAmount, 2);
            }

            decimal discountedAmount = entry.DiscountedAmount;
            if (entry.DiscountType == "Percentage" && discountValue > 0m)
            {
                discountedAmount = Math.Max(0m, Math.Round(amount * (1m - discountValue / 100m), 2));
            }
            else if (entry.DiscountType == "Amount" && discountValue > 0m)
            {
                discountedAmount = Math.Max(0m, Math.Round(amount - discountValue, 2));
            }

            results.Add(new RecalculatedScheduleEntry
            {
                DueDate = entry.DueDate,
                InvoicePortion = entry.InvoicePortion,
                PaymentAmount = amount,
                BasePaymentAmount = baseAmount,
                PaidAmount = paidAmount,
                Description = entry.Description,
                ModeOfPaymentId = entry.ModeOfPaymentId,
                DiscountType = entry.DiscountType,
                DiscountPercentage = discountValue,
                DiscountValidTill = entry.DiscountValidTill,
                DiscountedAmount = discountedAmount,
            });
        }

        return results;
    }

    /// <summary>
    /// Maps payment schedule entries from a source document (e.g. Sales Order / Purchase Order)
    /// onto a target invoice while preserving early-payment discount metadata and scaling fixed
    /// discount amounts proportionally to the target grand total (ERPNext commit 240a3a97a3).
    /// </summary>
    public List<PaymentScheduleEntry> MapScheduleEntries(
        IReadOnlyList<PaymentScheduleEntry> sourceEntries,
        string targetParentType,
        Guid targetParentId,
        decimal targetGrandTotal,
        decimal exchangeRate = 1m,
        Guid? tenantId = null)
    {
        if (sourceEntries == null || sourceEntries.Count == 0)
            return new List<PaymentScheduleEntry>();

        var inputs = sourceEntries.Select(s => new PaymentScheduleInput
        {
            DueDate = s.DueDate,
            InvoicePortion = s.InvoicePortion,
            PaymentAmount = s.PaymentAmount,
            Description = s.Description,
            ModeOfPaymentId = s.ModeOfPaymentId,
            DiscountType = s.DiscountType,
            DiscountPercentage = s.DiscountPercentage,
            DiscountValidTill = s.DiscountValidTill,
            DiscountedAmount = s.DiscountedAmount,
        }).ToList();

        var recalculated = RecalculateAmounts(inputs, targetGrandTotal, exchangeRate, keepSettled: false);
        return recalculated.Select(r => new PaymentScheduleEntry(
            LazyServiceProvider != null ? GuidGenerator.Create() : Guid.NewGuid(),
            targetParentType,
            targetParentId,
            r.DueDate,
            r.InvoicePortion,
            r.PaymentAmount,
            r.Description)
        {
            TenantId = tenantId,
            BasePaymentAmount = r.BasePaymentAmount,
            PaidAmount = 0m,
            ModeOfPaymentId = r.ModeOfPaymentId,
            DiscountType = r.DiscountType,
            DiscountPercentage = r.DiscountPercentage,
            DiscountValidTill = r.DiscountValidTill,
            DiscountedAmount = r.DiscountedAmount,
        }).ToList();
    }
}

// --- Supporting types ---

public class PaymentScheduleInput
{
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime? DiscountValidTill { get; set; }
    public decimal DiscountedAmount { get; set; }
}

public class PaymentScheduleValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RecalculatedScheduleEntry
{
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal BasePaymentAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding => PaymentAmount - PaidAmount;
    public decimal BaseOutstanding => BasePaymentAmount > 0 && PaymentAmount > 0
        ? Math.Max(0, Math.Round(BasePaymentAmount - (PaidAmount * BasePaymentAmount / PaymentAmount), 2))
        : 0m;
    public string? Description { get; set; }
    public Guid? ModeOfPaymentId { get; set; }
    public string? DiscountType { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime? DiscountValidTill { get; set; }
    public decimal DiscountedAmount { get; set; }
}
