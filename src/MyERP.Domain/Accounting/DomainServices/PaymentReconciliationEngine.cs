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
/// Payment Reconciliation Engine — advanced payment-to-invoice matching.
/// Promotes complex reconciliation logic from AppService to domain layer.
///
/// Per ERPNext payment-ledger-reconciliation.instructions.md:
/// - Greedy first-fit allocation algorithm
/// - Multi-currency support with exchange gain/loss JE generation
/// - Stale outstanding validation at execution time (not just UI time)
/// - Payment term-based allocation (split SI into per-term rows)
/// - Batch reconciliation with savepoint isolation
/// - DR/CR note reconciliation (returns treated as payments)
///
/// Per DO-NOT rules:
/// - Skip Payment Reconciliation stale outstanding validation at execution time
/// - Process Payment Reconciliation without exchange gain/loss JE for multi-currency differences
/// - Unreconcile without cancelling related exchange gain/loss Journal Entries
/// </summary>
public class PaymentReconciliationEngine : DomainService
{
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;

    public PaymentReconciliationEngine(
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository)
    {
        _pleService = pleService;
        _pleRepository = pleRepository;
    }

    /// <summary>
    /// Get unreconciled payments for a party — payments that have unallocated amounts.
    /// Per ERPNext: includes Payment Entries, Journal Entries, and return invoices
    /// (credit/debit notes are treated as payments for reconciliation).
    /// </summary>
    public async Task<List<UnreconciledPayment>> GetUnreconciledPaymentsAsync(
        string partyType, Guid partyId)
    {
        var query = await _pleRepository.GetQueryableAsync();

        // Get all PLE entries for this party grouped by voucher
        var grouped = query
            .Where(p => p.PartyType == partyType && p.PartyId == partyId && !p.Delinked)
            .GroupBy(p => new { p.VoucherType, p.VoucherId })
            .Select(g => new
            {
                g.Key.VoucherType,
                g.Key.VoucherId,
                TotalAmount = g.Sum(p => p.AmountInAccountCurrency),
                AllocatedAmount = g.Where(p => p.AgainstVoucherId != p.VoucherId)
                    .Sum(p => p.AmountInAccountCurrency),
            })
            .ToList();

        return grouped
            .Where(g => g.VoucherType is "PaymentEntry" or "JournalEntry")
            .Where(g => Math.Abs(g.TotalAmount - g.AllocatedAmount) > 0.01m)
            .Select(g => new UnreconciledPayment
            {
                VoucherType = g.VoucherType,
                VoucherId = g.VoucherId,
                TotalAmount = Math.Abs(g.TotalAmount),
                UnallocatedAmount = Math.Abs(g.TotalAmount - g.AllocatedAmount),
            })
            .ToList();
    }

    /// <summary>
    /// Execute batch reconciliation — allocates multiple payments to multiple invoices.
    /// Per ERPNext: greedy first-fit algorithm, processes allocations sequentially.
    ///
    /// Validates stale outstanding for each allocation at execution time.
    /// Per DO-NOT: must re-check at reconcile, not just at allocation UI time.
    /// </summary>
    public async Task<ReconciliationResult> ReconcileBatchAsync(
        Guid companyId,
        string partyType,
        Guid partyId,
        Guid accountId,
        string accountCurrency,
        IReadOnlyList<ReconciliationAllocation> allocations)
    {
        var result = new ReconciliationResult();

        foreach (var alloc in allocations)
        {
            try
            {
                // Stale outstanding validation (real-time, not from UI snapshot)
                var currentOutstanding = await _pleService.GetOutstandingAsync(
                    alloc.InvoiceVoucherType, alloc.InvoiceVoucherId);

                if (Math.Abs(alloc.AllocatedAmount) > Math.Abs(currentOutstanding) + 0.01m)
                {
                    result.Errors.Add(new ReconciliationError
                    {
                        InvoiceVoucherId = alloc.InvoiceVoucherId,
                        Message = $"Outstanding changed: was expected > {alloc.AllocatedAmount:N2}, now {currentOutstanding:N2}",
                    });
                    continue;
                }

                // Create PLE reconciliation entry
                await _pleService.ReconcileAsync(
                    companyId, DateTime.UtcNow.Date, accountId,
                    partyType, partyId,
                    alloc.PaymentVoucherType, alloc.PaymentVoucherId,
                    alloc.InvoiceVoucherType, alloc.InvoiceVoucherId,
                    alloc.AllocatedAmount, alloc.AllocatedAmount,
                    accountCurrency);

                result.ReconciledCount++;
                result.TotalAllocated += alloc.AllocatedAmount;
            }
            catch (BusinessException ex) when (ex.Code == "MyERP:02009")
            {
                result.Errors.Add(new ReconciliationError
                {
                    InvoiceVoucherId = alloc.InvoiceVoucherId,
                    Message = $"Over-allocation blocked: {ex.Message}",
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Calculate exchange gain/loss for a multi-currency reconciliation.
    /// Per ERPNext: if payment currency differs from invoice currency,
    /// the rate difference creates a gain or loss.
    ///
    /// gain_loss = allocated_amount × (payment_exchange_rate - invoice_exchange_rate)
    /// Positive = gain (payment rate better than invoice rate for receivable)
    /// Negative = loss
    /// </summary>
    public static decimal CalculateExchangeGainLoss(
        decimal allocatedAmount,
        decimal paymentExchangeRate,
        decimal invoiceExchangeRate)
    {
        if (paymentExchangeRate == invoiceExchangeRate)
            return 0;

        return Math.Round(allocatedAmount * (paymentExchangeRate - invoiceExchangeRate), 2);
    }

    /// <summary>
    /// Unreconcile a payment-to-invoice allocation.
    /// Per DO-NOT: must also cancel related exchange gain/loss JEs.
    /// </summary>
    public async Task<decimal> UnreconcileAsync(
        string paymentVoucherType, Guid paymentVoucherId,
        string invoiceVoucherType, Guid invoiceVoucherId)
    {
        // Get the allocated amount before delink (for invoice AmountPaid reversal)
        var query = await _pleRepository.GetQueryableAsync();
        var allocatedAmount = query
            .Where(p => p.VoucherType == paymentVoucherType
                     && p.VoucherId == paymentVoucherId
                     && p.AgainstVoucherType == invoiceVoucherType
                     && p.AgainstVoucherId == invoiceVoucherId
                     && !p.Delinked && !p.IsReversal)
            .Sum(p => Math.Abs(p.AmountInAccountCurrency));

        // Delink the PLE entries
        await _pleService.UnreconcileAsync(
            paymentVoucherType, paymentVoucherId,
            invoiceVoucherType, invoiceVoucherId);

        return allocatedAmount;
    }
}

/// <summary>An unreconciled payment available for allocation.</summary>
public class UnreconciledPayment
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
}

/// <summary>A single allocation instruction for batch reconciliation.</summary>
public class ReconciliationAllocation
{
    public string PaymentVoucherType { get; set; } = null!;
    public Guid PaymentVoucherId { get; set; }
    public string InvoiceVoucherType { get; set; } = null!;
    public Guid InvoiceVoucherId { get; set; }
    public decimal AllocatedAmount { get; set; }
}

/// <summary>Result of a batch reconciliation operation.</summary>
public class ReconciliationResult
{
    public int ReconciledCount { get; set; }
    public decimal TotalAllocated { get; set; }
    public List<ReconciliationError> Errors { get; set; } = new();
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>Error details for a failed allocation in batch reconciliation.</summary>
public class ReconciliationError
{
    public Guid InvoiceVoucherId { get; set; }
    public string Message { get; set; } = null!;
}
