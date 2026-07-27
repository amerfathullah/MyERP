using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Preserves original proportions, adjusts amounts, remainder to last entry.
    /// </summary>
    public IReadOnlyList<RecalculatedScheduleEntry> RecalculateAmounts(
        IReadOnlyList<PaymentScheduleInput> entries, decimal newGrandTotal)
    {
        if (entries == null || entries.Count == 0)
            return Array.Empty<RecalculatedScheduleEntry>();

        var results = new List<RecalculatedScheduleEntry>(entries.Count);
        var totalAllocated = 0m;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            decimal amount;

            if (i == entries.Count - 1)
            {
                // Last entry absorbs rounding remainder
                amount = newGrandTotal - totalAllocated;
            }
            else
            {
                amount = Math.Round(newGrandTotal * entry.InvoicePortion / 100m, 2);
                totalAllocated += amount;
            }

            results.Add(new RecalculatedScheduleEntry
            {
                DueDate = entry.DueDate,
                InvoicePortion = entry.InvoicePortion,
                PaymentAmount = amount,
                Description = entry.Description
            });
        }

        return results;
    }
}

// --- Supporting types ---

public class PaymentScheduleInput
{
    public DateTime DueDate { get; set; }
    public decimal InvoicePortion { get; set; }
    public decimal PaymentAmount { get; set; }
    public string? Description { get; set; }
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
    public string? Description { get; set; }
}
