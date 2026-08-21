using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Payment Reconciliation AppService — matches unallocated payments to outstanding invoices.
/// Delegates to PaymentReconciliationEngine for business logic.
/// 
/// Per ERPNext: reconciliation of multi-currency payments MUST create exchange gain/loss JE.
/// Per DO-NOT: "Process Payment Reconciliation without exchange gain/loss JE for multi-currency differences"
/// Per DO-NOT: "Unreconcile without cancelling related exchange gain/loss Journal Entries"
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class PaymentReconciliationAppService : ApplicationService, IPaymentReconciliationAppService
{
    private readonly PaymentReconciliationEngine _engine;
    private readonly PaymentLedgerService _pleService;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly IRepository<PurchaseInvoice, Guid> _purchaseInvoiceRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentEntryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public PaymentReconciliationAppService(
        PaymentReconciliationEngine engine,
        PaymentLedgerService pleService,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        IRepository<PurchaseInvoice, Guid> purchaseInvoiceRepository,
        IRepository<PaymentEntry, Guid> paymentEntryRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _engine = engine;
        _pleService = pleService;
        _pleRepository = pleRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _paymentEntryRepository = paymentEntryRepository;
        _journalEntryRepository = journalEntryRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Get all outstanding invoices for a party (for reconciliation UI).
    /// </summary>
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId)
    {
        var vouchers = await _pleService.GetOutstandingVouchersAsync(partyType, partyId);
        return vouchers.Select(v => new OutstandingInvoiceDto
        {
            VoucherId = v.VoucherId,
            VoucherType = v.VoucherType,
            Outstanding = v.Outstanding,
        }).ToList();
    }

    /// <summary>
    /// Get unallocated payments (Payment Entries and Journal Entries) for a party — the payment
    /// side of the reconciliation UI. <see cref="PaymentReconciliationEngine.GetUnreconciledPaymentsAsync"/>
    /// already implemented this lookup but had zero callers: the Angular reconciliation page only
    /// ever fetched outstanding invoices, with no way to pick which payment to allocate — so
    /// "Reconcile" sent whatever default value the allocation DTO's PaymentVoucherType/Id happened to
    /// have (Angular never set them), creating PLE rows against a nonexistent, all-zeros payment
    /// voucher. This endpoint is what the fixed Angular page now calls to let the user pick a real one.
    /// </summary>
    public async Task<List<UnreconciledPaymentDto>> GetUnreconciledPaymentsAsync(string partyType, Guid partyId)
    {
        var payments = await _engine.GetUnreconciledPaymentsAsync(partyType, partyId);
        var result = new List<UnreconciledPaymentDto>();

        foreach (var p in payments)
        {
            if (p.VoucherType == "PaymentEntry")
            {
                var pe = await _paymentEntryRepository.FindAsync(p.VoucherId);
                if (pe == null) continue;
                result.Add(new UnreconciledPaymentDto
                {
                    VoucherType = p.VoucherType,
                    VoucherId = p.VoucherId,
                    DocumentNumber = pe.PaymentNumber,
                    PostingDate = pe.PostingDate,
                    TotalAmount = p.TotalAmount,
                    UnallocatedAmount = p.UnallocatedAmount,
                    CurrencyCode = pe.CurrencyCode,
                    ExchangeRate = pe.ExchangeRate,
                });
            }
            else if (p.VoucherType == "JournalEntry")
            {
                var je = await _journalEntryRepository.FindAsync(p.VoucherId);
                if (je == null) continue;
                var jeCompany = await _companyRepository.GetAsync(je.CompanyId);
                result.Add(new UnreconciledPaymentDto
                {
                    VoucherType = p.VoucherType,
                    VoucherId = p.VoucherId,
                    DocumentNumber = je.EntryNumber,
                    PostingDate = je.PostingDate,
                    TotalAmount = p.TotalAmount,
                    UnallocatedAmount = p.UnallocatedAmount,
                    CurrencyCode = jeCompany.CurrencyCode,
                    ExchangeRate = 1m,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Reconcile payments against invoices.
    /// Delegates to PaymentReconciliationEngine for stale-outstanding validation
    /// and batch processing.
    /// 
    /// Per ERPNext: when payment exchange rate differs from invoice exchange rate,
    /// creates an Exchange Gain/Loss JE to book the difference.
    /// gain_loss = allocated_amount × (payment_rate - invoice_rate)
    /// </summary>
    public async Task ReconcileAsync(ReconcilePaymentDto input)
    {
        // Resolve party account
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var partyAccount = pleQuery
            .Where(p => p.PartyType == input.PartyType && p.PartyId == input.PartyId)
            .Select(p => p.AccountId)
            .FirstOrDefault();

        var company = await _companyRepository.GetAsync(input.CompanyId);

        var allocations = input.Allocations.Select(a => new ReconciliationAllocation
        {
            PaymentVoucherType = a.PaymentVoucherType,
            PaymentVoucherId = a.PaymentVoucherId,
            InvoiceVoucherType = a.InvoiceVoucherType,
            InvoiceVoucherId = a.InvoiceVoucherId,
            AllocatedAmount = a.AllocatedAmount,
        }).ToList();

        // Delegate to the engine — handles stale-outstanding validation, PLE batch, invoice
        // AmountPaid updates, and exchange gain/loss JE creation all in one place (also used by
        // ProcessPaymentReconciliationJob, which has no ABP authorization context to call this
        // AppService method directly).
        await _engine.ReconcileAndApplyAsync(
            input.CompanyId, input.PartyType, input.PartyId,
            partyAccount, company.CurrencyCode, allocations);
    }

    /// <summary>
    /// Computes a greedy first-fit allocation plan (payments × outstanding invoices) without
    /// applying it — lets the Angular "Auto-Allocate" convenience button pre-fill allocation amounts
    /// for the user to review/adjust before calling <see cref="ReconcileAsync"/>.
    /// </summary>
    public async Task<List<ReconcileAllocationDto>> GetAutoAllocationAsync(string partyType, Guid partyId)
    {
        var payments = await _engine.GetUnreconciledPaymentsAsync(partyType, partyId);
        var invoices = await _pleService.GetOutstandingVouchersAsync(partyType, partyId);

        var plan = PaymentReconciliationEngine.AutoAllocate(payments, invoices);
        return plan.Select(a => new ReconcileAllocationDto
        {
            PaymentVoucherId = a.PaymentVoucherId,
            PaymentVoucherType = a.PaymentVoucherType,
            InvoiceVoucherId = a.InvoiceVoucherId,
            InvoiceVoucherType = a.InvoiceVoucherType,
            AllocatedAmount = a.AllocatedAmount,
        }).ToList();
    }

    /// <summary>
    /// Unreconcile a previous payment-to-invoice allocation.
    /// Delegates to PaymentReconciliationEngine which handles delink + amount tracking.
    /// Per DO-NOT: "Unreconcile without cancelling related exchange gain/loss Journal Entries"
    /// </summary>
    public async Task UnreconcileAsync(UnreconcileDto input)
    {
        // Engine returns the allocated amount that was delinked
        var allocatedAmount = await _engine.UnreconcileAsync(
            input.PaymentVoucherType, input.PaymentVoucherId,
            input.InvoiceVoucherType, input.InvoiceVoucherId);

        // Reduce the invoice's AmountPaid (with concurrency retry)
        if (allocatedAmount > 0)
        {
            await UpdateInvoiceAmountPaidAsync(input.InvoiceVoucherType, input.InvoiceVoucherId, -allocatedAmount);
        }

        // Cancel related exchange gain/loss JEs
        // Per ERPNext: unreconciliation must cancel exchange gain/loss JE that was created during reconcile.
        // CreateExchangeGainLossJeIfNeeded tags the JE with the actual payment voucher's own type
        // ("PaymentEntry"/"JournalEntry"), not a "PaymentReconciliation" constant — this query used to
        // filter on that literal, which never matched anything, so no exchange JE was ever found or
        // cancelled here. Also scope to VoucherType=ExchangeGainOrLoss: a payment voucher's own main GL
        // JE shares the same (ReferenceType, ReferenceId) and must never be touched by unreconcile.
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var relatedJes = jeQuery
            .Where(je => je.ReferenceType == input.PaymentVoucherType
                      && je.ReferenceId == input.PaymentVoucherId
                      && je.VoucherType == JournalEntryVoucherType.ExchangeGainOrLoss
                      && je.Status == Core.DocumentStatus.Posted)
            .ToList();

        foreach (var je in relatedJes)
        {
            je.Cancel();
            await _journalEntryRepository.UpdateAsync(je);
        }
    }

    /// <summary>
    /// Concurrency-safe AmountPaid update with retry on optimistic concurrency conflict.
    /// </summary>
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
}
