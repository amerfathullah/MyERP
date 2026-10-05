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

            // Rule 2: Cannot exceed per-term outstanding
            var scheduleQuery = await _scheduleRepository.GetQueryableAsync();
            var scheduleEntry = scheduleQuery.FirstOrDefault(s =>
                s.ParentId == reference.ReferenceId &&
                s.Id == reference.PaymentTermId);

            if (scheduleEntry != null)
            {
                // Round to currency precision (2 decimals) per ERPNext PR #59783 (commits 15ed5321dc, 280bffe251)
                var roundedAllocated = Math.Round(reference.AllocatedAmount, 2);
                var roundedOutstanding = Math.Round(scheduleEntry.Outstanding, 2);
                if (roundedAllocated > roundedOutstanding)
                {
                    throw new BusinessException(MyERPDomainErrorCodes.PaymentTermOutstandingExceeded)
                        .WithData("allocatedAmount", reference.AllocatedAmount)
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
}
