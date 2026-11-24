using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Domain service for Payment Entry business rules.
/// Validates payment term-based allocation, stale outstanding, and per-term limits.
/// Source: erpnext/accounts/doctype/payment_entry/payment_entry.py → validate_allocated_amount_with_latest_data()
/// </summary>
public class PaymentEntryManager : DomainService
{
    private readonly IRepository<PaymentScheduleEntry, Guid> _scheduleRepository;

    public PaymentEntryManager(IRepository<PaymentScheduleEntry, Guid> scheduleRepository)
    {
        _scheduleRepository = scheduleRepository;
    }

    /// <summary>
    /// Validates payment term-based allocation rules at submit time.
    /// When an invoice uses a payment terms template with allocate_payment_based_on_payment_terms=true,
    /// each PE reference row MUST specify a PaymentTermId, and allocated amount cannot exceed term outstanding.
    /// </summary>
    public async Task ValidateTermBasedAllocationAsync(
        PaymentEntry paymentEntry,
        Func<Guid, Task<bool>> isTermBasedInvoice)
    {
        if (paymentEntry.References == null || !paymentEntry.References.Any())
            return;

        var allocatedByTerm = new Dictionary<(Guid ReferenceId, Guid TermId), decimal>();

        foreach (var reference in paymentEntry.References)
        {
            var isTermBased = await isTermBasedInvoice(reference.ReferenceId);
            if (!isTermBased) continue;

            // Rule 1: Payment term must be specified
            if (reference.PaymentTermId == null || reference.PaymentTermId == Guid.Empty)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PaymentTermRequired)
                    .WithData("referenceId", reference.ReferenceId)
                    .WithData("referenceName", reference.ReferenceNumber ?? "");
            }

            // Rule 2: Cannot exceed cumulative per-term outstanding (ERPNext PR #62678 / commit 95704805df)
            var termKey = (reference.ReferenceId, reference.PaymentTermId.Value);
            allocatedByTerm[termKey] = allocatedByTerm.GetValueOrDefault(termKey, 0m) + Math.Round(reference.AllocatedAmount, 2);
            var cumulativeTermAllocated = Math.Round(allocatedByTerm[termKey], 2);

            var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
            var scheduleEntry = scheduleQuery.FirstOrDefault(s =>
                s.ParentId == reference.ReferenceId &&
                s.Id == reference.PaymentTermId);

            if (scheduleEntry != null)
            {
                // Round to currency precision (2 decimals) per ERPNext PR #59783 (commits 15ed5321dc, 280bffe251)
                var roundedOutstanding = Math.Round(scheduleEntry.Outstanding, 2);
                if ((reference.AllocatedAmount > 0m && cumulativeTermAllocated > roundedOutstanding)
                    || (reference.AllocatedAmount < 0m && cumulativeTermAllocated < roundedOutstanding))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.PaymentTermOutstandingExceeded)
                        .WithData("allocatedAmount", cumulativeTermAllocated)
                        .WithData("termOutstanding", scheduleEntry.Outstanding)
                        .WithData("paymentTerm", scheduleEntry.Description ?? "");
                }
            }
        }
    }

    /// <summary>
    /// Validates that allocated amount does not exceed latest outstanding (stale data guard).
    /// Must be called at post time — concurrent users may have reduced outstanding since allocation.
    /// Per ERPNext PR #59783 (commits 15ed5321dc, 280bffe251): compares at currency precision (2 decimals)
    /// to avoid rejecting multi-currency allocations due to floating-point residue.
    /// </summary>
    public void ValidateAllocationNotExceedsOutstanding(
        PaymentEntry paymentEntry,
        decimal currentOutstanding,
        int precision = 2)
    {
        if (paymentEntry.PaidAmount <= 0) return;

        var roundedPaid = Math.Round(paymentEntry.PaidAmount, precision);
        var roundedOutstanding = Math.Round(currentOutstanding, precision);

        // Only validate when there IS an outstanding to exceed
        if (roundedOutstanding > 0 && roundedPaid > roundedOutstanding)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                .WithData("outstanding", currentOutstanding)
                .WithData("allocated", paymentEntry.PaidAmount);
        }
    }

    /// <summary>
    /// Validates allocated amount against reference row outstanding at currency precision (PR #59783).
    /// Prevents false rejection of multi-currency allocations due to floating-point / division residue.
    /// </summary>
    public static void ValidateReferenceAllocation(
        decimal allocatedAmount,
        decimal outstandingAmount,
        string? referenceName = null,
        int precision = 2)
    {
        var allocated = Math.Round(allocatedAmount, precision);
        var outstanding = Math.Round(outstandingAmount, precision);

        if (allocated > 0 && allocated > outstanding)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                .WithData("reference", referenceName ?? "")
                .WithData("allocated", allocatedAmount)
                .WithData("outstanding", outstandingAmount);
        }

        if (allocated < 0 && allocated < outstanding)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                .WithData("reference", referenceName ?? "")
                .WithData("allocated", allocatedAmount)
                .WithData("outstanding", outstandingAmount);
        }
    }

    /// <summary>
    /// Validates cumulative invoice and payment-term allocations across all reference rows on a Payment Entry,
    /// rejecting duplicate/split over-allocations and mixed-sign reference rows (e.g. [+80, -10] against +118).
    /// Source: erpnext/accounts/doctype/payment_entry/payment_entry.py -> validate_allocated_amount_with_latest_data()
    /// (ERPNext PR #62678 / commit 95704805df).
    /// </summary>
    public static void ValidateCumulativeReferenceAllocations(
        IReadOnlyList<ReferenceAllocationSnapshot> references,
        int precision = 2)
    {
        if (references == null || references.Count == 0)
            return;

        var allocatedByInvoice = new Dictionary<(string VoucherType, Guid VoucherId), decimal>();
        var allocatedByTerm = new Dictionary<(string VoucherType, Guid VoucherId, Guid TermId), decimal>();

        for (int i = 0; i < references.Count; i++)
        {
            var row = references[i];
            if (string.IsNullOrWhiteSpace(row.VoucherType) || row.VoucherId == Guid.Empty)
                continue;

            var allocated = Math.Round(row.AllocatedAmount, precision);
            var invoiceOutstanding = Math.Round(row.InvoiceOutstandingAmount, precision);

            var invKey = (row.VoucherType, row.VoucherId);
            allocatedByInvoice[invKey] = allocatedByInvoice.GetValueOrDefault(invKey, 0m) + allocated;
            var totalAllocated = Math.Round(allocatedByInvoice[invKey], precision);

            if (row.IsTermBasedAllocation)
            {
                if (!row.PaymentTermId.HasValue || row.PaymentTermId.Value == Guid.Empty)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.PaymentTermRequired)
                        .WithData("referenceId", row.VoucherId)
                        .WithData("referenceName", row.ReferenceName ?? "");
                }

                var termKey = (row.VoucherType, row.VoucherId, row.PaymentTermId.Value);
                allocatedByTerm[termKey] = allocatedByTerm.GetValueOrDefault(termKey, 0m) + allocated;
                var totalTermAllocated = Math.Round(allocatedByTerm[termKey], precision);
                var termOutstanding = Math.Round(row.TermOutstandingAmount ?? 0m, precision);

                if ((allocated > 0m && totalTermAllocated > termOutstanding)
                    || (allocated < 0m && totalTermAllocated < termOutstanding))
                {
                    throw new BusinessException(MyERPDomainErrorCodes.PaymentTermOutstandingExceeded)
                        .WithData("allocatedAmount", totalTermAllocated)
                        .WithData("termOutstanding", termOutstanding)
                        .WithData("paymentTerm", row.PaymentTermDescription ?? "");
                }
            }

            if (allocated > 0m && totalAllocated > invoiceOutstanding)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                    .WithData("reference", row.ReferenceName ?? "")
                    .WithData("allocated", totalAllocated)
                    .WithData("outstanding", invoiceOutstanding);
            }

            if (allocated < 0m && totalAllocated < invoiceOutstanding)
            {
                throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                    .WithData("reference", row.ReferenceName ?? "")
                    .WithData("allocated", totalAllocated)
                    .WithData("outstanding", invoiceOutstanding);
            }
        }
    }

    /// <summary>
    /// Adjusts the party-currency paid amount for reference reallocation by including non-exchange-gain-loss deductions
    /// converted from company currency to party account currency per ERPNext commit b4d2d8651d
    /// (<c>allocate_party_amount_against_ref_docs</c> in <c>payment_entry.py</c>).
    /// </summary>
    public static decimal CalculateReallocationPaidAmountWithDeductions(
        decimal paidAmountInPartyCurrency,
        PaymentType paymentType,
        decimal partyExchangeRate,
        IEnumerable<(decimal Amount, bool IsExchangeGainLoss)>? deductions,
        int precision = 2)
    {
        var deductionSum = deductions?
            .Where(d => !d.IsExchangeGainLoss)
            .Sum(d => d.Amount) ?? 0m;

        var adjusted = paidAmountInPartyCurrency;
        if (deductionSum != 0m)
        {
            var effectiveRate = partyExchangeRate > 0m ? partyExchangeRate : 1m;
            var sign = paymentType == PaymentType.Receive ? 1m : -1m;
            adjusted += (deductionSum / effectiveRate) * sign;
        }

        return Math.Round(adjusted, precision, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Applies early payment discount to <paramref name="paidAmount"/> and <paramref name="receivedAmount"/>,
    /// converting the bank-side discount only when the bank account currency differs from the party account currency
    /// per ERPNext commit bb60f4696b (<c>apply_early_payment_discount</c> in <c>payment_entry.py</c>).
    /// </summary>
    public static (decimal PaidAmount, decimal ReceivedAmount, decimal TotalDiscountAmount) ApplyEarlyPaymentDiscount(
        decimal paidAmount,
        decimal receivedAmount,
        PaymentType paymentType,
        string partyAccountCurrency,
        string? bankAccountCurrency,
        bool isMultiCurrency,
        decimal documentConversionRate,
        IEnumerable<(string DiscountType, decimal Discount, decimal PaymentAmount)> eligibleTermDiscounts)
    {
        decimal totalDiscount = 0m;
        if (eligibleTermDiscounts == null)
        {
            return (paidAmount, receivedAmount, 0m);
        }

        foreach (var term in eligibleTermDiscounts)
        {
            var discountAmount = string.Equals(term.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase)
                ? term.PaymentAmount * (term.Discount / 100m)
                : term.Discount;

            var conversionRate = isMultiCurrency ? (documentConversionRate > 0m ? documentConversionRate : 1m) : 1m;
            if (!string.IsNullOrWhiteSpace(bankAccountCurrency) &&
                string.Equals(bankAccountCurrency, partyAccountCurrency, StringComparison.OrdinalIgnoreCase))
            {
                conversionRate = 1m;
            }

            var bankSideDiscount = discountAmount * conversionRate;
            if (paymentType == PaymentType.Receive)
            {
                paidAmount -= discountAmount;
                receivedAmount -= bankSideDiscount;
            }
            else
            {
                receivedAmount -= discountAmount;
                paidAmount -= bankSideDiscount;
            }

            totalDiscount += discountAmount;
        }

        return (paidAmount, receivedAmount, totalDiscount);
    }

    /// <summary>
    /// Computes the combined early payment discount percentage across eligible Percentage and Amount
    /// payment term discounts per ERPNext PR #66981 (<c>get_total_discount_percent</c> in <c>payment_entry.py</c>).
    /// </summary>
    public static decimal CalculateTotalEarlyPaymentDiscountPercent(
        decimal documentGrandTotal,
        IEnumerable<(string DiscountType, decimal Discount)>? validDiscounts)
    {
        if (validDiscounts == null)
        {
            return 0m;
        }

        decimal totalDiscountPercent = 0m;
        decimal totalDiscountAmount = 0m;

        foreach (var discount in validDiscounts)
        {
            if (string.Equals(discount.DiscountType, "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                totalDiscountPercent += discount.Discount;
            }
            else if (string.Equals(discount.DiscountType, "Amount", StringComparison.OrdinalIgnoreCase))
            {
                totalDiscountAmount += discount.Discount;
            }
        }

        if (totalDiscountAmount != 0m && documentGrandTotal != 0m)
        {
            totalDiscountPercent += (totalDiscountAmount / documentGrandTotal) * 100m;
        }

        return totalDiscountPercent;
    }

    /// <summary>
    /// Splits an early payment discount into base-currency Income Loss and per-account Tax Loss rows
    /// without requiring a Payment Entry instance per ERPNext PR #66981 (<c>get_early_payment_discount_loss</c>
    /// in <c>payment_entry.py</c>):
    /// - Returns empty rows and <c>0</c> when the combined discount percent is zero.
    /// - Applies <c>-1</c> sign for <see cref="PaymentType.Pay"/> and <c>+1</c> for <see cref="PaymentType.Receive"/>.
    /// - Aggregates repeated tax account heads and skips tax accounts whose total loss is zero.
    /// - Rounds <c>BaseTotalDiscountLoss</c> from unrounded income + tax sum to minimize rounding drift.
    /// </summary>
    public static (IReadOnlyList<EarlyPaymentDiscountLossRow> Rows, decimal BaseTotalDiscountLoss) SplitEarlyPaymentDiscountLoss(
        decimal documentGrandTotal,
        decimal documentBaseTotal,
        IEnumerable<(Guid AccountId, decimal BaseTaxAmountAfterDiscount)>? documentTaxes,
        IEnumerable<(string DiscountType, decimal Discount)>? validDiscounts,
        PaymentType paymentType,
        Guid defaultDiscountAccountId,
        Guid? costCenterId,
        Guid? companyDefaultCostCenterId = null,
        int totalPrecision = 2,
        int taxPrecision = 2,
        int grandTotalPrecision = 2)
    {
        var totalDiscountPercent = CalculateTotalEarlyPaymentDiscountPercent(documentGrandTotal, validDiscounts);
        if (totalDiscountPercent == 0m)
        {
            return (Array.Empty<EarlyPaymentDiscountLossRow>(), 0m);
        }

        var effectiveCostCenterId = costCenterId ?? companyDefaultCostCenterId;
        var sign = paymentType == PaymentType.Pay ? -1m : 1m;

        var baseLossOnIncome = documentBaseTotal * (totalDiscountPercent / 100m);
        var rows = new List<EarlyPaymentDiscountLossRow>
        {
            new(
                defaultDiscountAccountId,
                effectiveCostCenterId,
                Math.Round(baseLossOnIncome, totalPrecision, MidpointRounding.AwayFromZero) * sign)
        };

        var baseLossByTaxAccount = new Dictionary<Guid, decimal>();
        var taxAccountOrder = new List<Guid>();
        if (documentTaxes != null)
        {
            foreach (var tax in documentTaxes)
            {
                var baseTaxLoss = tax.BaseTaxAmountAfterDiscount * (totalDiscountPercent / 100m);
                if (!baseLossByTaxAccount.ContainsKey(tax.AccountId))
                {
                    baseLossByTaxAccount[tax.AccountId] = baseTaxLoss;
                    taxAccountOrder.Add(tax.AccountId);
                }
                else
                {
                    baseLossByTaxAccount[tax.AccountId] += baseTaxLoss;
                }
            }
        }

        decimal baseLossOnTaxes = 0m;
        foreach (var accountId in taxAccountOrder)
        {
            var taxLoss = baseLossByTaxAccount[accountId];
            baseLossOnTaxes += taxLoss;
            if (taxLoss == 0m)
            {
                continue;
            }

            rows.Add(new EarlyPaymentDiscountLossRow(
                accountId,
                effectiveCostCenterId,
                Math.Round(taxLoss, taxPrecision, MidpointRounding.AwayFromZero) * sign));
        }

        var baseTotalDiscountLoss = Math.Round(
            baseLossOnIncome + baseLossOnTaxes,
            grandTotalPrecision,
            MidpointRounding.AwayFromZero);

        return (rows, baseTotalDiscountLoss);
    }
}

public sealed record EarlyPaymentDiscountLossRow(
    Guid AccountId,
    Guid? CostCenterId,
    decimal Amount);

public sealed record ReferenceAllocationSnapshot(
    string VoucherType,
    Guid VoucherId,
    string? ReferenceName,
    decimal AllocatedAmount,
    decimal InvoiceOutstandingAmount,
    bool IsTermBasedAllocation = false,
    Guid? PaymentTermId = null,
    decimal? TermOutstandingAmount = null,
    string? PaymentTermDescription = null);
