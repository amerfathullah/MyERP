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

            if (scheduleEntry != null && reference.AllocatedAmount > scheduleEntry.Outstanding)
            {
                throw new BusinessException(MyERPDomainErrorCodes.PaymentTermOutstandingExceeded)
                    .WithData("allocatedAmount", reference.AllocatedAmount)
                    .WithData("termOutstanding", scheduleEntry.Outstanding)
                    .WithData("paymentTerm", scheduleEntry.Description ?? "");
            }
        }
    }

    /// <summary>
    /// Validates that allocated amount does not exceed latest outstanding (stale data guard).
    /// Must be called at post time — concurrent users may have reduced outstanding since allocation.
    /// </summary>
    public void ValidateAllocationNotExceedsOutstanding(
        PaymentEntry paymentEntry,
        decimal currentOutstanding)
    {
        if (paymentEntry.PaidAmount <= 0) return;

        // Only validate when there IS an outstanding to exceed
        if (currentOutstanding > 0 && paymentEntry.PaidAmount > currentOutstanding)
        {
            throw new BusinessException(MyERPDomainErrorCodes.OverAllocation)
                .WithData("outstanding", currentOutstanding)
                .WithData("allocated", paymentEntry.PaidAmount);
        }
    }
}
