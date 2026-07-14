using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Tax.Entities;
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
    /// </summary>
    public TransactionTotals Calculate(
        List<TransactionItem> items,
        List<TransactionTaxRow> taxRows,
        decimal exchangeRate = 1m,
        decimal discountAmount = 0m,
        string applyDiscountOn = "Grand Total",
        List<Guid>? roundOffApplicableAccountIds = null)
    {
        // Step 1: Calculate net total from items
        decimal netTotal = items.Sum(i => i.NetAmount);

        // Step 2: Apply discount on net total (re-distributes to items)
        if (discountAmount > 0 && applyDiscountOn == "Net Total" && netTotal > 0)
        {
            foreach (var item in items)
            {
                var proportion = item.NetAmount / netTotal;
                item.DiscountAmount = Math.Round(discountAmount * proportion, 2);
                item.NetAmount -= item.DiscountAmount;
            }
            // Last item absorbs rounding difference
            var totalDistributed = items.Sum(i => i.DiscountAmount);
            if (totalDistributed != discountAmount && items.Count > 0)
                items[^1].NetAmount -= (discountAmount - totalDistributed);

            netTotal = items.Sum(i => i.NetAmount);
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

                decimal currentTaxAmount = CalculateTaxForItem(tax, item, orderedTaxes, i, netTotal, perItemGrandTotal, effectiveRate);

                tax.TaxAmount += currentTaxAmount;

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
            grandTotal -= discountAmount;
        }

        // Step 6: Rounding
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
        List<TransactionTaxRow> allTaxes,
        int taxIndex,
        decimal netTotal,
        decimal[] perItemGrandTotal,
        decimal effectiveRate)
    {
        return tax.ChargeType switch
        {
            "On Net Total" => (effectiveRate / 100m) * item.NetAmount,
            "On Previous Row Amount" when tax.ReferenceRowIndex.HasValue =>
                (effectiveRate / 100m) * GetPreviousRowItemAmount(allTaxes, tax.ReferenceRowIndex.Value - 1),
            "On Previous Row Total" when tax.ReferenceRowIndex.HasValue && tax.ReferenceRowIndex.Value - 1 < perItemGrandTotal.Length =>
                (effectiveRate / 100m) * perItemGrandTotal[tax.ReferenceRowIndex.Value - 1],
            "On Item Quantity" => effectiveRate * item.Qty,
            "Actual" => netTotal != 0
                ? (item.NetAmount / netTotal) * effectiveRate
                : 0,
            _ => 0,
        };
    }

    private static decimal GetPreviousRowItemAmount(List<TransactionTaxRow> taxes, int refIndex)
    {
        if (refIndex >= 0 && refIndex < taxes.Count)
            return taxes[refIndex].TaxAmount;
        return 0;
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
