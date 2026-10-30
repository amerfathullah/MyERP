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
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(string partyType, Guid partyId, Guid? accountId = null)
    {
        var vouchers = await _pleService.GetOutstandingVouchersAsync(partyType, partyId, accountId);
        return vouchers.Select(v => new OutstandingInvoiceDto
        {
            AccountId = v.AccountId,
            VoucherId = v.VoucherId,
            VoucherType = v.VoucherType,
            Outstanding = v.Outstanding,
        }).ToList();
    }

    /// <summary>
    /// Get unallocated payments (Payment Entries and Journal Entries) for a party — the payment
    /// side of the reconciliation UI, optionally filtered by Receivable/Payable account (ERPNext commit 7623dae273).
    /// </summary>
    public async Task<List<UnreconciledPaymentDto>> GetUnreconciledPaymentsAsync(string partyType, Guid partyId, Guid? accountId = null)
    {
        var payments = await _engine.GetUnreconciledPaymentsAsync(partyType, partyId, accountId);
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
    /// Per ERPNext commit 7623dae273: resolves the Receivable/Payable account (explicit AccountId
    /// or PLE account) so Employee and cross-type reconciliations determine GL direction accurately.
    /// </summary>
    public async Task ReconcileAsync(ReconcilePaymentDto input)
    {
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var matchingPle = pleQuery
            .Where(p => p.PartyType == input.PartyType && p.PartyId == input.PartyId && !p.Delinked)
            .FirstOrDefault();

        var partyAccount = input.AccountId.HasValue && input.AccountId.Value != Guid.Empty
            ? input.AccountId.Value
            : (matchingPle?.AccountId ?? Guid.Empty);

        var accountRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Account, Guid>>();
        var resolvedAccount = partyAccount != Guid.Empty ? await accountRepo.FindAsync(partyAccount) : null;

        var companyId = input.CompanyId != Guid.Empty
            ? input.CompanyId
            : (resolvedAccount?.CompanyId ?? matchingPle?.CompanyId ?? Guid.Empty);
        var company = await _companyRepository.GetAsync(companyId);
        var accountCurrency = !string.IsNullOrWhiteSpace(resolvedAccount?.Currency)
            ? resolvedAccount!.Currency!
            : company.CurrencyCode;

        var allocations = input.Allocations.Select(a => new ReconciliationAllocation
        {
            PaymentVoucherType = a.PaymentVoucherType,
            PaymentVoucherId = a.PaymentVoucherId,
            InvoiceVoucherType = a.InvoiceVoucherType,
            InvoiceVoucherId = a.InvoiceVoucherId,
            AllocatedAmount = a.AllocatedAmount,
        }).ToList();

        await _engine.ReconcileAndApplyAsync(
            companyId, input.PartyType, input.PartyId,
            partyAccount, accountCurrency, allocations);
    }

    /// <summary>
    /// Computes a greedy first-fit allocation plan (payments × outstanding invoices) without
    /// applying it — lets the Angular "Auto-Allocate" convenience button pre-fill allocation amounts
    /// for the user to review/adjust before calling <see cref="ReconcileAsync"/>.
    /// </summary>
    public async Task<List<ReconcileAllocationDto>> GetAutoAllocationAsync(string partyType, Guid partyId, Guid? accountId = null)
    {
        var payments = await _engine.GetUnreconciledPaymentsAsync(partyType, partyId, accountId);
        var invoices = await _pleService.GetOutstandingVouchersAsync(partyType, partyId, accountId);

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
        var pleQueryBefore = await _pleRepository.GetQueryableAsync();
        var matchingPles = pleQueryBefore
            .Where(p => p.VoucherType == input.PaymentVoucherType
                     && p.VoucherId == input.PaymentVoucherId
                     && p.AgainstVoucherType == input.InvoiceVoucherType
                     && p.AgainstVoucherId == input.InvoiceVoucherId
                     && !p.Delinked && !p.IsReversal)
            .Select(p => new { p.AccountCurrency, p.Amount })
            .ToList();

        var pleAccountCurrency = matchingPles.FirstOrDefault()?.AccountCurrency;
        var pleBaseAmount = matchingPles.Sum(p => Math.Abs(p.Amount));

        var allocatedAmount = await _engine.UnreconcileAsync(
            input.PaymentVoucherType, input.PaymentVoucherId,
            input.InvoiceVoucherType, input.InvoiceVoucherId);

        if (allocatedAmount > 0)
        {
            var amountInInvoiceCurrency = allocatedAmount;
            if (input.InvoiceVoucherType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.GetAsync(input.InvoiceVoucherId);
                if (!string.IsNullOrWhiteSpace(pleAccountCurrency)
                    && !string.Equals(pleAccountCurrency, si.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    amountInInvoiceCurrency = si.ExchangeRate > 0 ? Math.Round(pleBaseAmount / si.ExchangeRate, 2) : allocatedAmount;
                }
            }
            else if (input.InvoiceVoucherType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.GetAsync(input.InvoiceVoucherId);
                if (!string.IsNullOrWhiteSpace(pleAccountCurrency)
                    && !string.Equals(pleAccountCurrency, pi.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                {
                    amountInInvoiceCurrency = pi.ExchangeRate > 0 ? Math.Round(pleBaseAmount / pi.ExchangeRate, 2) : allocatedAmount;
                }
            }

            await UpdateInvoiceAmountPaidAsync(input.InvoiceVoucherType, input.InvoiceVoucherId, -amountInInvoiceCurrency);

            if (input.PaymentVoucherType == "PaymentEntry")
            {
                var pe = (await _paymentEntryRepository.WithDetailsAsync(p => p.References))
                    .FirstOrDefault(p => p.Id == input.PaymentVoucherId)
                    ?? await _paymentEntryRepository.FindAsync(input.PaymentVoucherId);
                if (pe != null)
                {
                    var matchingRef = pe.References.FirstOrDefault(r =>
                        r.ReferenceType == input.InvoiceVoucherType && r.ReferenceId == input.InvoiceVoucherId);
                    if (matchingRef != null)
                    {
                        pe.References.Remove(matchingRef);
                    }
                    if (pe.AgainstInvoiceType == input.InvoiceVoucherType && pe.AgainstInvoiceId == input.InvoiceVoucherId)
                    {
                        pe.AgainstInvoiceId = null;
                        pe.AgainstInvoiceType = null;
                    }
                    await _paymentEntryRepository.UpdateAsync(pe, autoSave: true);
                }
            }
        }

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

    public async Task UnreconcileAllocationsAsync(List<UnreconcileDto> inputs)
    {
        if (inputs == null || inputs.Count == 0)
            return;

        foreach (var item in inputs)
        {
            await UnreconcileAsync(item);
        }
    }

    public async Task<List<LinkedAllocationDto>> GetLinkedAllocationsAsync(string voucherType, Guid voucherId)
    {
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var result = new List<LinkedAllocationDto>();

        bool isPaymentVoucher = voucherType is "PaymentEntry" or "JournalEntry";

        if (isPaymentVoucher)
        {
            var entries = pleQuery
                .Where(p => p.VoucherType == voucherType
                         && p.VoucherId == voucherId
                         && p.AgainstVoucherId != voucherId
                         && !p.Delinked
                         && !p.IsReversal)
                .ToList();

            if (entries.Count == 0)
                return result;

            string paymentNumber = string.Empty;
            DateTime paymentDate = DateTime.UtcNow;

            if (voucherType == "PaymentEntry")
            {
                var pe = await _paymentEntryRepository.FindAsync(voucherId);
                if (pe != null)
                {
                    paymentNumber = pe.PaymentNumber ?? string.Empty;
                    paymentDate = pe.PostingDate;
                }
            }
            else if (voucherType == "JournalEntry")
            {
                var je = await _journalEntryRepository.FindAsync(voucherId);
                if (je != null)
                {
                    paymentNumber = je.EntryNumber ?? string.Empty;
                    paymentDate = je.PostingDate;
                }
            }

            var grouped = entries
                .GroupBy(p => new { p.AgainstVoucherType, p.AgainstVoucherId, p.AccountCurrency })
                .Select(g => new
                {
                    g.Key.AgainstVoucherType,
                    g.Key.AgainstVoucherId,
                    g.Key.AccountCurrency,
                    AllocatedAmount = g.Sum(x => Math.Abs(x.AmountInAccountCurrency))
                })
                .Where(x => x.AllocatedAmount > 0)
                .ToList();

            foreach (var item in grouped)
            {
                string invoiceNumber = string.Empty;
                DateTime invoiceDate = DateTime.UtcNow;

                if (item.AgainstVoucherType == "SalesInvoice")
                {
                    var si = await _salesInvoiceRepository.FindAsync(item.AgainstVoucherId);
                    if (si != null)
                    {
                        invoiceNumber = si.InvoiceNumber ?? string.Empty;
                        invoiceDate = si.IssueDate;
                    }
                }
                else if (item.AgainstVoucherType == "PurchaseInvoice")
                {
                    var pi = await _purchaseInvoiceRepository.FindAsync(item.AgainstVoucherId);
                    if (pi != null)
                    {
                        invoiceNumber = pi.InvoiceNumber ?? string.Empty;
                        invoiceDate = pi.IssueDate;
                    }
                }

                result.Add(new LinkedAllocationDto
                {
                    PaymentVoucherType = voucherType,
                    PaymentVoucherId = voucherId,
                    PaymentVoucherNumber = paymentNumber,
                    PaymentDate = paymentDate,
                    InvoiceVoucherType = item.AgainstVoucherType,
                    InvoiceVoucherId = item.AgainstVoucherId,
                    InvoiceVoucherNumber = invoiceNumber,
                    InvoiceDate = invoiceDate,
                    AllocatedAmount = item.AllocatedAmount,
                    AccountCurrency = item.AccountCurrency
                });
            }
        }
        else
        {
            var entries = pleQuery
                .Where(p => p.AgainstVoucherType == voucherType
                         && p.AgainstVoucherId == voucherId
                         && p.VoucherId != voucherId
                         && !p.Delinked
                         && !p.IsReversal)
                .ToList();

            if (entries.Count == 0)
                return result;

            string invoiceNumber = string.Empty;
            DateTime invoiceDate = DateTime.UtcNow;

            if (voucherType == "SalesInvoice")
            {
                var si = await _salesInvoiceRepository.FindAsync(voucherId);
                if (si != null)
                {
                    invoiceNumber = si.InvoiceNumber ?? string.Empty;
                    invoiceDate = si.IssueDate;
                }
            }
            else if (voucherType == "PurchaseInvoice")
            {
                var pi = await _purchaseInvoiceRepository.FindAsync(voucherId);
                if (pi != null)
                {
                    invoiceNumber = pi.InvoiceNumber ?? string.Empty;
                    invoiceDate = pi.IssueDate;
                }
            }

            var grouped = entries
                .GroupBy(p => new { p.VoucherType, p.VoucherId, p.AccountCurrency })
                .Select(g => new
                {
                    g.Key.VoucherType,
                    g.Key.VoucherId,
                    g.Key.AccountCurrency,
                    AllocatedAmount = g.Sum(x => Math.Abs(x.AmountInAccountCurrency))
                })
                .Where(x => x.AllocatedAmount > 0)
                .ToList();

            foreach (var item in grouped)
            {
                string paymentNumber = string.Empty;
                DateTime paymentDate = DateTime.UtcNow;

                if (item.VoucherType == "PaymentEntry")
                {
                    var pe = await _paymentEntryRepository.FindAsync(item.VoucherId);
                    if (pe != null)
                    {
                        paymentNumber = pe.PaymentNumber ?? string.Empty;
                        paymentDate = pe.PostingDate;
                    }
                }
                else if (item.VoucherType == "JournalEntry")
                {
                    var je = await _journalEntryRepository.FindAsync(item.VoucherId);
                    if (je != null)
                    {
                        paymentNumber = je.EntryNumber ?? string.Empty;
                        paymentDate = je.PostingDate;
                    }
                }

                result.Add(new LinkedAllocationDto
                {
                    PaymentVoucherType = item.VoucherType,
                    PaymentVoucherId = item.VoucherId,
                    PaymentVoucherNumber = paymentNumber,
                    PaymentDate = paymentDate,
                    InvoiceVoucherType = voucherType,
                    InvoiceVoucherId = voucherId,
                    InvoiceVoucherNumber = invoiceNumber,
                    InvoiceDate = invoiceDate,
                    AllocatedAmount = item.AllocatedAmount,
                    AccountCurrency = item.AccountCurrency
                });
            }
        }

        return result;
    }
}
