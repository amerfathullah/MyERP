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
}

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
