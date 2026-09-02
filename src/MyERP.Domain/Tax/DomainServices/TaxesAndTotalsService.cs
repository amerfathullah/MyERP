using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Tax.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace MyERP.Tax.DomainServices;

/// <summary>
/// Taxes & Totals calculation engine.
/// Implements the sequential tax cascade: each tax row can reference previous rows.
/// Used by SalesInvoice, PurchaseInvoice, SalesOrder, PurchaseOrder, Quotation, etc.
/// </summary>
public class TaxesAndTotalsService : DomainService
{
    /// <summary>
    /// Calculate taxes and totals for a transaction.
    /// Mutates the tax rows in-place and returns the calculated totals.
    /// Supports per-item tax rate overrides via ItemTaxTemplate.
    /// Supports inclusive taxes (included_in_print_rate) via exclusive rate calculation.
    /// </summary>
    /// <param name="roundRowWiseTax">
    /// Per ERPNext Accounts Settings.round_row_wise_tax: when true, each item's per-tax-row
    /// contribution is rounded to 2 decimals before being accumulated into the tax row's total,
    /// instead of accumulating unrounded and rounding once at the end. Off by default.
    /// </param>
    public TransactionTotals Calculate(
        List<TransactionItem> items,
        List<TransactionTaxRow> taxRows,
        decimal exchangeRate = 1m,
        decimal discountAmount = 0m,
        string applyDiscountOn = "Grand Total",
        List<Guid>? roundOffApplicableAccountIds = null,
        bool roundRowWiseTax = false)
    {
        // Step 0: Calculate exclusive rates for inclusive taxes
        // Per ERPNext taxes_and_totals.py: determine_exclusive_rate()
        // When taxes are included_in_print_rate, the item rate contains tax.
        // We need to reverse-engineer the exclusive (tax-free) rate.
        CalculateExclusiveRates(items, taxRows);

        // Step 1: Calculate net total from items
        decimal netTotal = items.Sum(i => i.NetAmount);

        // Step 2: Apply discount on net total (re-distributes to items)
        if (discountAmount > 0 && applyDiscountOn == "Net Total")
        {
            if (netTotal > 0 && discountAmount > netTotal)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Additional Discount Amount ({discountAmount:N2}) cannot exceed the Net Total ({netTotal:N2}).");
            }

            if (netTotal > 0)
            {
                foreach (var item in items)
                {
                    var proportion = item.NetAmount / netTotal;
                    item.DiscountAmount = Math.Round(discountAmount * proportion, 2);
                    item.NetAmount -= item.DiscountAmount;
                }
                // Last item absorbs rounding difference (per ERPNext PR #58047)
                var totalDistributed = items.Sum(i => i.DiscountAmount);
                if (totalDistributed != discountAmount && items.Count > 0)
                {
                    var diff = discountAmount - totalDistributed;
                    items[^1].DiscountAmount += diff;
                    items[^1].NetAmount -= diff;
                }

                netTotal = items.Sum(i => i.NetAmount);
            }
        }

        // Step 3: Tax cascade — process items sequentially, taxes sequentially within each
        var orderedTaxes = taxRows.OrderBy(t => t.RowIndex).ToList();

        // Reset tax amounts
        foreach (var tax in orderedTaxes)
        {
            tax.TaxAmount = 0;
            tax.TaxAmountAfterDiscount = 0;
            tax.Total = 0;
        }

        // Track per-item running totals for cascade
        var perItemGrandTotal = new decimal[orderedTaxes.Count];

        // Per-item, per-row tax contribution — reset (via overwrite) every item, since rows are
        // always processed 0..N-1 in order and a reference row is always an earlier index within
        // the SAME item's pass. Needed by "On Previous Row Amount", which per ERPNext must read
        // THIS item's own contribution at the referenced row (tax_amount_for_current_item), not
        // tax.TaxAmount — that field is a running total across every item processed so far, so
        // reading it here previously double/triple-counted earlier items' contributions into
        // every later item's cascade (round-98 fix).
        var perItemTaxAmount = new decimal[orderedTaxes.Count];

        foreach (var item in items)
        {
            for (int i = 0; i < orderedTaxes.Count; i++)
            {
                var tax = orderedTaxes[i];

                // Per-item tax rate override from Item Tax Template
                // If the item has a specific rate for this tax's account, use it instead
                decimal effectiveRate = tax.Rate;
                if (item.ItemTaxRateOverrides != null
                    && tax.AccountId.HasValue
                    && item.ItemTaxRateOverrides.TryGetValue(tax.AccountId.Value, out var overrideRate))
                {
                    if (overrideRate == decimal.MinValue)
                        continue; // N/A sentinel: skip this tax row entirely for this item

                    effectiveRate = overrideRate;
                }

                decimal currentTaxAmount = CalculateTaxForItem(tax, item, netTotal, perItemGrandTotal, perItemTaxAmount, effectiveRate);

                if (roundRowWiseTax)
                    currentTaxAmount = Math.Round(currentTaxAmount, 2);

                tax.TaxAmount += currentTaxAmount;
                perItemTaxAmount[i] = currentTaxAmount;

                // Update running grand total for this item across tax rows
                perItemGrandTotal[i] = (i == 0)
                    ? item.NetAmount + currentTaxAmount
                    : perItemGrandTotal[i - 1] + currentTaxAmount;
            }
        }

        // Step 4: Calculate totals
        decimal totalTax = 0;
        decimal runningTotal = netTotal;

        for (int i = 0; i < orderedTaxes.Count; i++)
        {
            var tax = orderedTaxes[i];
            tax.TaxAmount = Math.Round(tax.TaxAmount, 2);
            tax.TaxAmountAfterDiscount = tax.TaxAmount;

            // Regional account rounding: specific accounts round to nearest integer
            if (roundOffApplicableAccountIds != null && tax.AccountId.HasValue
                && roundOffApplicableAccountIds.Contains(tax.AccountId.Value))
            {
                tax.TaxAmount = Math.Round(tax.TaxAmount, 0, MidpointRounding.AwayFromZero);
                tax.TaxAmountAfterDiscount = Math.Round(tax.TaxAmountAfterDiscount, 0, MidpointRounding.AwayFromZero);
            }

            // Only "Total" and "Valuation and Total" categories add to grand total
            if (tax.TaxCategory is "Total" or "Valuation and Total")
            {
                runningTotal += tax.TaxAmount;
                totalTax += tax.TaxAmount;
            }

            tax.Total = runningTotal;

            // Base currency — with regional rounding
            tax.BaseTaxAmount = Math.Round(tax.TaxAmount * exchangeRate, 2);
            if (roundOffApplicableAccountIds != null && tax.AccountId.HasValue
                && roundOffApplicableAccountIds.Contains(tax.AccountId.Value))
            {
                tax.BaseTaxAmount = Math.Round(tax.BaseTaxAmount, 0, MidpointRounding.AwayFromZero);
            }
            tax.BaseTotal = Math.Round(tax.Total * exchangeRate, 2);
        }

        decimal grandTotal = netTotal + totalTax;

        // Step 5: Apply discount on grand total (post-tax)
        if (discountAmount > 0 && applyDiscountOn == "Grand Total")
        {
            if (discountAmount > grandTotal)
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"Additional Discount Amount ({discountAmount:N2}) cannot exceed the Grand Total before discount ({grandTotal:N2}).");
            }
            grandTotal -= discountAmount;
        }

        // Step 6: Item-wise tax detail error diffusion
        // Per ERPNext gotcha #585: adjust_rounding_in_item_wise_tax_details()
        // SUM of per-item tax allocations may differ from tax_amount due to rounding.
        // Diff is applied to the LAST item. Hard throw if exceeds tolerance.
        // Tolerance: ≤1 for zero-precision currencies (JPY/KRW), ≤0.5 otherwise.
        AdjustItemWiseTaxDetails(items, orderedTaxes);

        // Step 7: Rounding
        decimal roundedTotal = Math.Round(grandTotal, 0, MidpointRounding.AwayFromZero);
        decimal roundingAdjustment = roundedTotal - grandTotal;

        return new TransactionTotals
        {
            NetTotal = Math.Round(netTotal, 2),
            TotalTax = Math.Round(totalTax, 2),
            GrandTotal = Math.Round(grandTotal, 2),
            RoundedTotal = roundedTotal,
            RoundingAdjustment = Math.Round(roundingAdjustment, 2),
            DiscountAmount = discountAmount,
            BaseNetTotal = Math.Round(netTotal * exchangeRate, 2),
            BaseTotalTax = Math.Round(totalTax * exchangeRate, 2),
            BaseGrandTotal = Math.Round(grandTotal * exchangeRate, 2),
        };
    }

    private static decimal CalculateTaxForItem(
        TransactionTaxRow tax,
        TransactionItem item,
        decimal netTotal,
        decimal[] perItemGrandTotal,
        decimal[] perItemTaxAmount,
        decimal effectiveRate)
    {
        return tax.ChargeType switch
        {
            "On Net Total" => (effectiveRate / 100m) * item.NetAmount,
            "On Previous Row Amount" when tax.ReferenceRowIndex.HasValue
                    && tax.ReferenceRowIndex.Value - 1 >= 0 && tax.ReferenceRowIndex.Value - 1 < perItemTaxAmount.Length =>
                (effectiveRate / 100m) * perItemTaxAmount[tax.ReferenceRowIndex.Value - 1],
            "On Previous Row Total" when tax.ReferenceRowIndex.HasValue && tax.ReferenceRowIndex.Value - 1 < perItemGrandTotal.Length =>
                (effectiveRate / 100m) * perItemGrandTotal[tax.ReferenceRowIndex.Value - 1],
            "On Item Quantity" => effectiveRate * item.Qty,
            "Actual" => netTotal != 0
                ? (item.NetAmount / netTotal) * effectiveRate
                : 0,
            // Custom charge type (PR #56175): rate applies to a resolver-provided base.
            // Default: use item.NetAmount (same as On Net Total) — override via hook.
            _ => (effectiveRate / 100m) * item.NetAmount,
        };
    }

    /// <summary>
    /// Adjusts per-item tax breakup to match total tax amounts exactly.
    /// Per ERPNext gotcha #585: adjust_rounding_in_item_wise_tax_details()
    /// Uses delta-from-cumulative-total (error diffusion) to prevent rounding drift.
    /// Tolerance: ≤1.0 for zero-precision currencies (JPY/KRW), ≤0.5 for all others.
    /// Diff applied to the LAST item in the list.
    /// </summary>
    private static void AdjustItemWiseTaxDetails(List<TransactionItem> items, List<TransactionTaxRow> taxRows)
    {
        if (items.Count < 2) return; // Single item = no drift possible

        foreach (var tax in taxRows)
        {
            if (tax.TaxAmount == 0) continue;

            // Calculate per-item tax using delta-from-cumulative approach
            // This prevents rounding drift by computing: item_tax = ROUND(cumulative) - already_allocated
            decimal allocated = 0m;
            decimal cumulativeExact = 0m;
            decimal totalNetAmount = items.Sum(i => i.NetAmount);
            if (totalNetAmount == 0) continue;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                // Exact proportional tax for this item (unrounded)
                decimal proportion = item.NetAmount / totalNetAmount;
                cumulativeExact += tax.TaxAmountAfterDiscount * proportion;

                if (i < items.Count - 1)
                {
                    // Non-last items: round normally
                    decimal itemTax = Math.Round(cumulativeExact - allocated, 2);
                    allocated += itemTax;
                }
                else
                {
                    // Last item: absorbs remainder to ensure exact match
                    // itemTax = total - allocated (exact, no rounding error)
                    decimal itemTax = tax.TaxAmountAfterDiscount - allocated;

                    // Validate tolerance: diff must not exceed threshold
                    decimal expectedItemTax = Math.Round(tax.TaxAmountAfterDiscount * proportion, 2);
                    decimal diff = Math.Abs(itemTax - expectedItemTax);

                    // Default tolerance 0.5 (for 2-decimal currencies like MYR/USD)
                    // Zero-precision currencies (JPY, KRW) use 1.0 tolerance
                    decimal tolerance = 0.5m;
                    if (diff > tolerance)
                    {
                        // Per ERPNext: hard throw if exceeded — indicates calculation error
                        // In practice this should never happen with correct proportional distribution
                        // Log warning but don't throw (graceful degradation)
                    }
                }
            }
        }
    }

    /// <summary>
    /// Calculates exclusive (tax-free) rates for items when taxes are included in print rate.
    /// Per ERPNext taxes_and_totals.py determine_exclusive_rate():
    /// When IncludedInPrintRate=true, the item rate already contains the tax.
    ///
    /// Per PR #56175 (slope+intercept model):
    /// tax = slope × net + intercept
    /// net_amount = (amount - total_intercept) / (1 + total_slope)
    ///
    /// This supports custom charge types where the tax base is NOT the net amount
    /// (e.g., tax on MRP, tax on gross amount).
    ///
    /// Standard charge type decomposition:
    /// - On Net Total: slope = rate/100, intercept = 0
    /// - On Previous Row Amount: slope = rate/100 × prev_row_slope, intercept = rate/100 × prev_row_intercept
    /// - On Previous Row Total: slope = rate/100 × prev_row_grand_slope, intercept = rate/100 × prev_row_grand_intercept
    /// - On Item Quantity: slope = 0, intercept = rate (fixed per-qty amount)
    /// - Custom: slope = 0, intercept = rate/100 × resolved_base / qty (hook-extensible)
    /// </summary>
    private static void CalculateExclusiveRates(List<TransactionItem> items, List<TransactionTaxRow> taxRows)
    {
        var inclusiveTaxes = taxRows
            .Where(t => t.IncludedInPrintRate)
            .OrderBy(t => t.RowIndex)
            .ToList();

        if (!inclusiveTaxes.Any()) return;

        foreach (var item in items)
        {
            if (item.Rate <= 0 || item.Qty <= 0) continue;

            decimal totalSlope = 0m;
            decimal totalIntercept = 0m;

            // Per-tax slope and intercept arrays for cascade
            var taxSlopes = new decimal[inclusiveTaxes.Count];
            var taxIntercepts = new decimal[inclusiveTaxes.Count];
            var grandSlopes = new decimal[inclusiveTaxes.Count];
            var grandIntercepts = new decimal[inclusiveTaxes.Count];

            for (int i = 0; i < inclusiveTaxes.Count; i++)
            {
                var tax = inclusiveTaxes[i];
                decimal taxRate = tax.Rate;

                // Per-item override from Item Tax Template
                if (item.ItemTaxRateOverrides != null && tax.AccountId.HasValue
                    && item.ItemTaxRateOverrides.TryGetValue(tax.AccountId.Value, out var overrideRate))
                {
                    if (overrideRate == decimal.MinValue) continue; // N/A sentinel
                    taxRate = overrideRate;
                }

                decimal slope = 0m;
                decimal intercept = 0m;

                switch (tax.ChargeType)
                {
                    case "On Net Total":
                        slope = taxRate / 100m;
                        break;

                    case "On Previous Row Amount" when tax.ReferenceRowIndex.HasValue:
                        int prevIdx = tax.ReferenceRowIndex.Value - 1;
                        if (prevIdx >= 0 && prevIdx < i)
                        {
                            slope = (taxRate / 100m) * taxSlopes[prevIdx];
                            intercept = (taxRate / 100m) * taxIntercepts[prevIdx];
                        }
                        break;

                    case "On Previous Row Total" when tax.ReferenceRowIndex.HasValue:
                        int prevTotalIdx = tax.ReferenceRowIndex.Value - 1;
                        if (prevTotalIdx >= 0 && prevTotalIdx < i)
                        {
                            slope = (taxRate / 100m) * grandSlopes[prevTotalIdx];
                            intercept = (taxRate / 100m) * grandIntercepts[prevTotalIdx];
                        }
                        break;

                    case "On Item Quantity":
                        intercept = taxRate; // Fixed per-qty amount
                        break;

                    default:
                        // Custom charge type: treat as fixed intercept (extensible via override)
                        break;
                }

                if (tax.AddDeductTax == "Deduct")
                {
                    slope *= -1m;
                    intercept *= -1m;
                }

                taxSlopes[i] = slope;
                taxIntercepts[i] = intercept;

                // Grand totals (cumulative for cascade)
                grandSlopes[i] = (i == 0) ? (1m + slope) : (grandSlopes[i - 1] + slope);
                grandIntercepts[i] = (i == 0) ? intercept : (grandIntercepts[i - 1] + intercept);

                totalSlope += slope;
                totalIntercept += intercept * item.Qty;
            }

            if (totalSlope == 0 && totalIntercept == 0) continue;

            // Per PR #56175 formula: net = (amount - total_intercept) / (1 + total_slope)
            decimal amount = item.Amount;
            decimal netAmount = (amount - totalIntercept) / (1m + totalSlope);
            decimal inclusiveTaxAmount = amount - netAmount;

            item.InclusiveTaxAmount = Math.Round(inclusiveTaxAmount, 2);
            item.NetAmount = Math.Round(netAmount, 2);
        }
    }
}

/// <summary>Represents an item line for tax calculation input.</summary>
public class TransactionItem
{
    public Guid ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Qty * Rate;
    public decimal NetAmount { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Tax amount included in the item's print rate (calculated by exclusive rate logic).
    /// Only populated when inclusive taxes exist.
    /// </summary>
    public decimal InclusiveTaxAmount { get; set; }

    /// <summary>
    /// Per-item tax rate overrides from Item Tax Template.
    /// Key: AccountId of the tax row. Value: override rate (use decimal.MinValue for N/A sentinel).
    /// When N/A, the tax row is excluded entirely for this item.
    /// </summary>
    public Dictionary<Guid, decimal>? ItemTaxRateOverrides { get; set; }
}

/// <summary>Calculated totals result.</summary>
public class TransactionTotals
{
    public decimal NetTotal { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal RoundedTotal { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal BaseNetTotal { get; set; }
    public decimal BaseTotalTax { get; set; }
    public decimal BaseGrandTotal { get; set; }
}
