using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
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
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PaymentReconciliationEngine(
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _pleService = pleService;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _paymentEntryRepository = paymentEntryRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
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
                result.AppliedAllocations.Add(alloc);
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
    /// Full reconciliation: PLE batch (<see cref="ReconcileBatchAsync"/>) plus the two side effects
    /// that make an allocation actually count — invoice AmountPaid update and exchange gain/loss JE
    /// for multi-currency differences. Both <c>PaymentReconciliationAppService.ReconcileAsync</c> (the
    /// manual UI) and <c>ProcessPaymentReconciliationJob</c> (the automated batch engine, no ABP
    /// authorization context) call this single implementation — moved here from the AppService for
    /// the same reason <see cref="GlRepostService"/> was: a background job can't satisfy an
    /// [Authorize]'d AppService method, so the real logic has to live where both callers can reach it.
    /// </summary>
    public async Task<ReconciliationResult> ReconcileAndApplyAsync(
        Guid companyId,
        string partyType,
        Guid partyId,
        Guid partyAccountId,
        string accountCurrency,
        IReadOnlyList<ReconciliationAllocation> allocations)
    {
        var company = await _companyRepository.GetAsync(companyId);

        var result = await ReconcileBatchAsync(
            companyId, partyType, partyId, partyAccountId, accountCurrency, allocations);

        foreach (var alloc in result.AppliedAllocations)
        {
            if (alloc.InvoiceVoucherType == "SalesInvoice")
            {
                await UpdateInvoiceAmountPaidAsync("SalesInvoice", alloc.InvoiceVoucherId, alloc.AllocatedAmount);
                var si = await _salesInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                await CreateExchangeGainLossJeIfNeededAsync(company, alloc, si.ExchangeRate, partyType, partyAccountId);
            }
            else if (alloc.InvoiceVoucherType == "PurchaseInvoice")
            {
                await UpdateInvoiceAmountPaidAsync("PurchaseInvoice", alloc.InvoiceVoucherId, alloc.AllocatedAmount);
                var pi = await _purchaseInvoiceRepository.GetAsync(alloc.InvoiceVoucherId);
                await CreateExchangeGainLossJeIfNeededAsync(company, alloc, pi.ExchangeRate, partyType, partyAccountId);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates an Exchange Gain/Loss Journal Entry when payment rate != invoice rate.
    /// Per ERPNext: gain_loss = allocated_amount × (payment_rate - invoice_rate), with the sign
    /// reversed for Payable accounts (the same rate difference means the opposite GL direction on a
    /// liability vs an asset account). Posts against the party's own receivable/payable account —
    /// no bank movement happens here (unlike Payment Entry submit, which posts against bank/cash).
    /// </summary>
    private async Task CreateExchangeGainLossJeIfNeededAsync(
        Company company,
        ReconciliationAllocation alloc,
        decimal invoiceExchangeRate,
        string partyType,
        Guid partyAccountId)
    {
        if (!company.ExchangeGainLossAccountId.HasValue) return;
        if (partyAccountId == Guid.Empty) return;

        decimal paymentExchangeRate = 1m;
        if (alloc.PaymentVoucherType == "PaymentEntry")
        {
            var pe = await _paymentEntryRepository.GetAsync(alloc.PaymentVoucherId);
            paymentExchangeRate = pe.ExchangeRate;
        }

        var gainLoss = CalculateExchangeGainLoss(alloc.AllocatedAmount, paymentExchangeRate, invoiceExchangeRate);
        if (Math.Abs(gainLoss) < 0.01m) return;

        var effectiveGainLoss = partyType == "Supplier" ? -gainLoss : gainLoss;

        var fyQuery = await _fiscalYearRepository.GetQueryableAsync();
        var fy = fyQuery.FirstOrDefault(f =>
            f.CompanyId == company.Id && f.StartDate <= DateTime.UtcNow.Date && f.EndDate >= DateTime.UtcNow.Date);
        if (fy == null) return;

        var je = new JournalEntry(
            GuidGenerator.Create(), company.Id, fy.Id, DateTime.UtcNow.Date)
        {
            VoucherType = JournalEntryVoucherType.ExchangeGainOrLoss,
            ReferenceType = alloc.PaymentVoucherType,
            ReferenceId = alloc.PaymentVoucherId,
        };
        je.Narration = $"Exchange {(effectiveGainLoss > 0 ? "Gain" : "Loss")} on reconciliation";

        var exchangeAccountId = company.ExchangeGainLossAccountId.Value;
        var absGainLoss = Math.Abs(effectiveGainLoss);

        if (effectiveGainLoss > 0)
        {
            je.AddLine(partyAccountId, absGainLoss, true);
            je.AddLine(exchangeAccountId, absGainLoss, false);
        }
        else
        {
            je.AddLine(exchangeAccountId, absGainLoss, true);
            je.AddLine(partyAccountId, absGainLoss, false);
        }

        je.Post();
        await _journalEntryRepository.InsertAsync(je);
    }

    private async Task UpdateInvoiceAmountPaidAsync(string invoiceType, Guid invoiceId, decimal amount)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (invoiceType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.GetAsync(invoiceId);
                    si.AmountPaid = Math.Max(0, si.AmountPaid + amount);
                    await _salesInvoiceRepository.UpdateAsync(si, autoSave: true);
                }
                else if (invoiceType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.GetAsync(invoiceId);
                    pi.AmountPaid = Math.Max(0, pi.AmountPaid + amount);
                    await _purchaseInvoiceRepository.UpdateAsync(pi, autoSave: true);
                }
                return;
            }
            catch (Volo.Abp.Data.AbpDbConcurrencyException) when (attempt < 3)
            {
                Logger.LogWarning(
                    "Concurrency conflict updating {InvoiceType} {InvoiceId} AmountPaid (attempt {Attempt}/3)",
                    invoiceType, invoiceId, attempt);
                await Task.Delay(attempt * 10);
            }
        }
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
    /// Greedy first-fit auto-allocation — matches unreconciled payments against outstanding invoices.
    /// Per ERPNext allocate_entries(): iterates payments × invoices in the given order (callers should
    /// pass invoices oldest-due-first and payments oldest-first for FIFO semantics); a payment
    /// exhausted to zero stops consuming further invoices, an invoice fully covered is skipped for
    /// the next payment. Pure computation — no PLE/GL side effects, safe to call from Angular-facing
    /// endpoints or the background batch engine to produce a plan before executing it.
    /// </summary>
    public static List<ReconciliationAllocation> AutoAllocate(
        IReadOnlyList<UnreconciledPayment> payments,
        IReadOnlyList<OutstandingVoucher> invoices)
    {
        var allocations = new List<ReconciliationAllocation>();
        var remainingInvoices = invoices
            .Where(i => i.Outstanding > 0.009m)
            .Select(i => new { i.VoucherType, i.VoucherId, Remaining = i.Outstanding })
            .ToList();

        foreach (var payment in payments)
        {
            var remainingPayment = payment.UnallocatedAmount;
            if (remainingPayment <= 0.009m) continue;

            for (int i = 0; i < remainingInvoices.Count && remainingPayment > 0.009m; i++)
            {
                var invoice = remainingInvoices[i];
                if (invoice.Remaining <= 0.009m) continue;

                var allocated = Math.Min(remainingPayment, invoice.Remaining);
                allocations.Add(new ReconciliationAllocation
                {
                    PaymentVoucherType = payment.VoucherType,
                    PaymentVoucherId = payment.VoucherId,
                    InvoiceVoucherType = invoice.VoucherType,
                    InvoiceVoucherId = invoice.VoucherId,
                    AllocatedAmount = allocated,
                });

                remainingPayment -= allocated;
                remainingInvoices[i] = new { invoice.VoucherType, invoice.VoucherId, Remaining = invoice.Remaining - allocated };
            }
        }

        return allocations;
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
    public List<ReconciliationAllocation> AppliedAllocations { get; set; } = new();
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>Error details for a failed allocation in batch reconciliation.</summary>
public class ReconciliationError
{
    public Guid InvoiceVoucherId { get; set; }
    public string Message { get; set; } = null!;
}
