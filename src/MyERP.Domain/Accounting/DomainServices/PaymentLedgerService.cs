using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Domain service for Payment Ledger Entry management.
/// PLE is the authoritative source for outstanding amounts on invoices.
/// </summary>
public class PaymentLedgerService : DomainService
{
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<Account, Guid>? _accountRepository;

    public PaymentLedgerService(
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<Account, Guid>? accountRepository = null)
    {
        _pleRepository = pleRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// Create a PLE entry (e.g., when an invoice is submitted).
    /// </summary>
    public async Task<PaymentLedgerEntry> CreateEntryAsync(
        Guid companyId, DateTime postingDate,
        Guid accountId, string partyType, Guid partyId,
        string voucherType, Guid voucherId,
        string againstVoucherType, Guid againstVoucherId,
        decimal amount, decimal amountInAccountCurrency,
        string accountCurrency, DateTime? dueDate = null,
        Guid? tenantId = null)
    {
        var ple = new PaymentLedgerEntry(
            GuidGenerator.Create(), companyId, postingDate,
            accountId, partyType, partyId,
            voucherType, voucherId,
            againstVoucherType, againstVoucherId,
            amount, amountInAccountCurrency,
            accountCurrency, tenantId)
        {
            DueDate = dueDate,
        };

        await _pleRepository.InsertAsync(ple);
        return ple;
    }

    /// <summary>
    /// Get outstanding amount for a specific voucher.
    /// Outstanding = SUM(AmountInAccountCurrency) WHERE against_voucher matches AND delinked = false.
    /// </summary>
    public async Task<decimal> GetOutstandingAsync(string voucherType, Guid voucherId)
    {
        var query = await _pleRepository.GetQueryableAsync();
        var outstanding = query
            .Where(p => p.AgainstVoucherType == voucherType
                && p.AgainstVoucherId == voucherId
                && !p.Delinked)
            .Sum(p => p.AmountInAccountCurrency);
        return outstanding;
    }

    /// <summary>
    /// Get all outstanding invoices for a party (for payment allocation).
    /// Returns only those with non-zero outstanding.
    /// Per ERPNext PR #59515 (commit 88dbf5e0e7): evaluates each party account on its own so
    /// vouchers spanning multiple receivable/payable accounts are not netted to zero and dropped.
    /// </summary>
    public async Task<List<OutstandingVoucher>> GetOutstandingVouchersAsync(string partyType, Guid partyId, Guid? accountId = null)
    {
        var query = await _pleRepository.GetQueryableAsync();

        query = query.Where(p => p.PartyType == partyType && p.PartyId == partyId && !p.Delinked);
        if (accountId.HasValue)
        {
            query = query.Where(p => p.AccountId == accountId.Value);
        }

        var grouped = query
            .GroupBy(p => new { p.AccountId, p.AgainstVoucherType, p.AgainstVoucherId })
            .Select(g => new OutstandingVoucher
            {
                AccountId = g.Key.AccountId,
                VoucherType = g.Key.AgainstVoucherType,
                VoucherId = g.Key.AgainstVoucherId,
                Outstanding = g.Sum(p => p.AmountInAccountCurrency),
            })
            .Where(v => v.Outstanding != 0)
            .ToList();

        return grouped;
    }

    /// <summary>
    /// Gets total overdue outstanding amount for a customer/supplier past their due date in company currency.
    /// Per ERPNext PR #57786 (commit b5a3815a64): reads from Payment Ledger rather than GL tags,
    /// so post-submit reconciled payments and advances are properly accounted for.
    /// </summary>
    public async Task<decimal> GetOverdueAmountAsync(string partyType, Guid partyId, Guid companyId, DateTime asOfDate)
    {
        var query = await _pleRepository.GetQueryableAsync();
        var overdueVouchers = query
            .Where(p => p.PartyType == partyType
                && p.PartyId == partyId
                && p.CompanyId == companyId
                && !p.Delinked)
            .GroupBy(p => new { p.AgainstVoucherType, p.AgainstVoucherId, p.DueDate })
            .Select(g => new
            {
                g.Key.DueDate,
                Outstanding = g.Sum(p => p.Amount)
            })
            .Where(v => v.Outstanding > 0 && v.DueDate.HasValue && v.DueDate.Value.Date < asOfDate.Date)
            .ToList();

        return overdueVouchers.Sum(v => v.Outstanding);
    }

    /// <summary>
    /// Create a reversal PLE (for cancellation).
    /// </summary>
    public async Task<PaymentLedgerEntry> CreateReversalAsync(PaymentLedgerEntry original, Guid? tenantId = null)
    {
        var reversal = new PaymentLedgerEntry(
            GuidGenerator.Create(), original.CompanyId, DateTime.UtcNow,
            original.AccountId, original.PartyType, original.PartyId,
            original.VoucherType, original.VoucherId,
            original.AgainstVoucherType, original.AgainstVoucherId,
            -original.Amount, -original.AmountInAccountCurrency,
            original.AccountCurrency, tenantId)
        {
            DueDate = original.DueDate,
            IsReversal = true,
        };

        await _pleRepository.InsertAsync(reversal);
        return reversal;
    }

    /// <summary>
    /// Reconcile a payment against an invoice.
    /// Creates a new PLE entry that points from the payment voucher to the target invoice voucher,
    /// reducing outstanding on the invoice.
    /// 
    /// Validates stale outstanding at execution time (not just allocation UI time).
    /// </summary>
    public async Task<PaymentLedgerEntry> ReconcileAsync(
        Guid companyId,
        DateTime postingDate,
        Guid accountId,
        string partyType,
        Guid partyId,
        string paymentVoucherType,
        Guid paymentVoucherId,
        string invoiceVoucherType,
        Guid invoiceVoucherId,
        decimal allocatedAmount,
        decimal allocatedAmountInAccountCurrency,
        string accountCurrency,
        Guid? tenantId = null)
    {
        // Real-time outstanding validation at reconcile time (prevents over-allocation)
        var currentOutstanding = await GetOutstandingAsync(invoiceVoucherType, invoiceVoucherId);
        if (Math.Abs(allocatedAmountInAccountCurrency) > Math.Abs(currentOutstanding) + 0.01m)
        {
            throw new Volo.Abp.BusinessException("MyERP:02009")
                .WithData("outstanding", currentOutstanding)
                .WithData("allocatedAmount", allocatedAmountInAccountCurrency);
        }

        // Per ERPNext commit 7623dae273: determine Receivable vs Payable direction from the selected
        // party Account rather than party_type (e.g. Employee on Receivable vs Payable account).
        var sign = await ResolveReconciliationSignAsync(accountId, partyType, currentOutstanding);

        var ple = new PaymentLedgerEntry(
            GuidGenerator.Create(), companyId, postingDate,
            accountId, partyType, partyId,
            paymentVoucherType, paymentVoucherId,
            invoiceVoucherType, invoiceVoucherId,
            sign * Math.Abs(allocatedAmount),
            sign * Math.Abs(allocatedAmountInAccountCurrency),
            accountCurrency, tenantId);

        await _pleRepository.InsertAsync(ple);
        return ple;
    }

    private async Task<decimal> ResolveReconciliationSignAsync(Guid accountId, string partyType, decimal currentOutstanding)
    {
        var accountRepo = _accountRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Account, Guid>>();
        if (accountRepo != null && accountId != Guid.Empty)
        {
            var account = await accountRepo.FindAsync(accountId);
            if (account != null)
            {
                if (account.AccountSubType == AccountSubType.AccountsPayable) return 1m;
                if (account.AccountSubType == AccountSubType.AccountsReceivable) return -1m;
                if (account.AccountType == AccountType.Liability) return 1m;
                if (account.AccountType == AccountType.Asset) return -1m;
            }
        }

        if (currentOutstanding != 0m)
            return currentOutstanding > 0m ? -1m : 1m;

        return string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase) ? -1m : 1m;
    }

    /// <summary>
    /// Unreconcile (delink) a payment from an invoice.
    /// Sets Delinked=true on all PLE entries matching the payment→invoice relationship.
    /// This causes outstanding to be recalculated excluding delinked entries.
    /// </summary>
    public async Task UnreconcileAsync(
        string paymentVoucherType,
        Guid paymentVoucherId,
        string invoiceVoucherType,
        Guid invoiceVoucherId)
    {
        var query = await _pleRepository.GetQueryableAsync();
        var entries = query
            .Where(p => p.VoucherType == paymentVoucherType
                && p.VoucherId == paymentVoucherId
                && p.AgainstVoucherType == invoiceVoucherType
                && p.AgainstVoucherId == invoiceVoucherId
                && !p.Delinked)
            .ToList();

        foreach (var entry in entries)
        {
            entry.Delinked = true;
        }

        if (entries.Count > 0)
        {
            await _pleRepository.UpdateManyAsync(entries);
        }
    }

    /// <summary>
    /// Determines whether the Payment Ledger view/preview button should be shown for a voucher
    /// per ERPNext PR #68176 (<c>payment_entry.js</c>, <c>journal_entry.js</c>, <c>sales_invoice.js</c>,
    /// <c>purchase_invoice.js</c>, <c>invoice_discounting.js</c>):
    /// - Payment Entry: only when a party is set (internal transfers have no party and hit no PLE).
    /// - Journal Entry: only when at least one row uses a Receivable or Payable account.
    /// - Sales Invoice, Purchase Invoice, Invoice Discounting: always shown.
    /// </summary>
    public static bool ShouldShowPaymentLedgerForVoucher(
        string? voucherType,
        bool hasParty,
        bool hasReceivableOrPayableAccount)
    {
        if (string.IsNullOrWhiteSpace(voucherType))
            return false;

        var normalized = voucherType.Replace(" ", string.Empty).Trim();
        if (string.Equals(normalized, "PaymentEntry", StringComparison.OrdinalIgnoreCase))
            return hasParty;
        if (string.Equals(normalized, "JournalEntry", StringComparison.OrdinalIgnoreCase))
            return hasReceivableOrPayableAccount;

        return string.Equals(normalized, "SalesInvoice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "PurchaseInvoice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "InvoiceDiscounting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns Payment Ledger Entries for a voucher ordered by creation time, excluding delinked
    /// entries unless <paramref name="showCancelledEntries"/> is true.
    /// Per ERPNext PR #68176 (<c>ledger_preview.py: get_pl_entries_for_preview</c> &amp; <c>payment_ledger.py</c>).
    /// </summary>
    public async Task<List<PaymentLedgerEntry>> GetVoucherPaymentLedgerEntriesAsync(
        string voucherType,
        Guid voucherId,
        bool showCancelledEntries = false)
    {
        var query = await _pleRepository.GetQueryableAsync();
        query = query.Where(p => p.VoucherType == voucherType && p.VoucherId == voucherId);
        if (!showCancelledEntries)
        {
            query = query.Where(p => !p.Delinked);
        }

        return query.OrderBy(p => p.CreationTime).ToList();
    }
}

/// <summary>Summary of outstanding amount for a voucher.</summary>
public class OutstandingVoucher
{
    public Guid? AccountId { get; set; }
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public decimal Outstanding { get; set; }
}
