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

    public PaymentLedgerService(IRepository<PaymentLedgerEntry, Guid> pleRepository)
    {
        _pleRepository = pleRepository;
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
    /// </summary>
    public async Task<List<OutstandingVoucher>> GetOutstandingVouchersAsync(string partyType, Guid partyId)
    {
        var query = await _pleRepository.GetQueryableAsync();

        var grouped = query
            .Where(p => p.PartyType == partyType && p.PartyId == partyId && !p.Delinked)
            .GroupBy(p => new { p.AgainstVoucherType, p.AgainstVoucherId })
            .Select(g => new OutstandingVoucher
            {
                VoucherType = g.Key.AgainstVoucherType,
                VoucherId = g.Key.AgainstVoucherId,
                Outstanding = g.Sum(p => p.AmountInAccountCurrency),
            })
            .Where(v => v.Outstanding != 0)
            .ToList();

        return grouped;
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

        // Determine sign: receive from customer (CR = negative), pay to supplier (DR = positive)
        var sign = partyType == "Customer" ? -1m : 1m;

        var ple = new PaymentLedgerEntry(
            GuidGenerator.Create(), companyId, postingDate,
            accountId, partyType, partyId,
            paymentVoucherType, paymentVoucherId,
            invoiceVoucherType, invoiceVoucherId,
            sign * allocatedAmount,
            sign * allocatedAmountInAccountCurrency,
            accountCurrency, tenantId);

        await _pleRepository.InsertAsync(ple);
        return ple;
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
}

/// <summary>Summary of outstanding amount for a voucher.</summary>
public class OutstandingVoucher
{
    public string VoucherType { get; set; } = null!;
    public Guid VoucherId { get; set; }
    public decimal Outstanding { get; set; }
}
