using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Rebuilds Payment Ledger Entries (PLE) from GL entries when PLE data becomes inconsistent
/// with the ledger (e.g. after a manual GL correction or a GlRepostService run that didn't
/// touch PLE). ERPNext equivalent: accounts/doctype/repost_payment_ledger.
///
/// Algorithm per voucher:
/// 1. Delete existing PLE entries for the voucher.
/// 2. Read the voucher's posted JournalEntry line(s) that hit a Receivable/Payable account
///    AND carry a party (PartyId + PartyType) — these are the only lines PLE tracks.
/// 3. Rebuild one PLE row per such line. Sign convention matches the rest of this codebase:
///    Debit line → positive PLE amount, Credit line → negative (see PostSalesInvoiceAsync /
///    PostPaymentEntryAsync in DocumentPostingOrchestrator for the same convention).
///
/// Only 4 voucher types carry PLE-eligible party lines: Sales Invoice, Purchase Invoice,
/// Payment Entry, Journal Entry (manual JEs against a party account).
/// </summary>
public class PaymentLedgerRepostService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public static readonly HashSet<string> AllowedVoucherTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SalesInvoice",
        "PurchaseInvoice",
        "PaymentEntry",
        "JournalEntry",
    };

    public PaymentLedgerRepostService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _journalRepository = journalRepository;
        _pleRepository = pleRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// Reposts PLE for a single voucher. Deletes existing PLE rows and rebuilds them from GL.
    /// </summary>
    public async Task<int> RepostForVoucherAsync(Guid companyId, string voucherType, Guid voucherId)
    {
        if (!AllowedVoucherTypes.Contains(voucherType))
            return 0;

        var existing = await _pleRepository.GetListAsync(p =>
            p.CompanyId == companyId && p.VoucherType == voucherType && p.VoucherId == voucherId);
        foreach (var entry in existing)
            await _pleRepository.DeleteAsync(entry);

        var jeQuery = await _journalRepository.GetQueryableAsync();
        var journals = jeQuery
            .Where(je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && ((je.ReferenceType == voucherType && je.ReferenceId == voucherId)
                    || (voucherType == "JournalEntry" && je.Id == voucherId)))
            .ToList();

        if (journals.Count == 0)
            return 0;

        var partyAccountIds = await GetPartyAccountIdsAsync(companyId);

        var rebuilt = 0;
        foreach (var je in journals)
        {
            foreach (var line in je.Lines)
            {
                if (!line.PartyId.HasValue || string.IsNullOrEmpty(line.PartyType))
                    continue;
                if (!partyAccountIds.Contains(line.AccountId))
                    continue;

                var signedAmount = line.IsDebit ? line.Amount : -line.Amount;
                var signedAccountCurrencyAmount = line.IsDebit ? line.AmountInAccountCurrency : -line.AmountInAccountCurrency;

                var ple = new PaymentLedgerEntry(
                    GuidGenerator.Create(), companyId, je.PostingDate,
                    line.AccountId, line.PartyType!, line.PartyId.Value,
                    voucherType, voucherId,
                    line.AgainstVoucherType ?? voucherType, line.AgainstVoucherId ?? voucherId,
                    signedAmount, signedAccountCurrencyAmount,
                    line.AccountCurrency ?? "MYR");

                await _pleRepository.InsertAsync(ple);
                rebuilt++;
            }
        }

        return rebuilt;
    }

    /// <summary>
    /// Batch repost for every posted voucher of the allowed types with PostingDate >= fromDate.
    /// Per ERPNext: single-voucher failure does not abort the batch — errors are collected.
    /// </summary>
    public async Task<PaymentLedgerRepostResult> RepostForCompanyAsync(Guid companyId, DateTime fromDate)
    {
        var jeQuery = await _journalRepository.GetQueryableAsync();
        var vouchers = jeQuery
            .Where(je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate >= fromDate)
            .Select(je => new { je.ReferenceType, je.ReferenceId, je.Id, je.PostingDate })
            .ToList()
            .Select(je => (
                VoucherType: string.IsNullOrEmpty(je.ReferenceType) ? "JournalEntry" : je.ReferenceType!,
                VoucherId: string.IsNullOrEmpty(je.ReferenceType) ? je.Id : je.ReferenceId!.Value,
                PostingDate: je.PostingDate))
            .Where(v => AllowedVoucherTypes.Contains(v.VoucherType))
            .Distinct()
            .OrderBy(v => v.PostingDate)
            .ToList();

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<string>();

        foreach (var voucher in vouchers)
        {
            try
            {
                await RepostForVoucherAsync(companyId, voucher.VoucherType, voucher.VoucherId);
                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"{voucher.VoucherType}/{voucher.VoucherId}: {ex.Message}");
            }
        }

        return new PaymentLedgerRepostResult(vouchers.Count, successCount, failedCount, errors);
    }

    private async Task<HashSet<Guid>> GetPartyAccountIdsAsync(Guid companyId)
    {
        var query = await _accountRepository.GetQueryableAsync();
        return query
            .Where(a => a.CompanyId == companyId
                && (a.AccountSubType == AccountSubType.AccountsReceivable
                    || a.AccountSubType == AccountSubType.AccountsPayable))
            .Select(a => a.Id)
            .ToHashSet();
    }
}

/// <summary>Result of a company-wide PLE repost batch.</summary>
public record PaymentLedgerRepostResult(int TotalVouchers, int SuccessCount, int FailedCount, List<string> Errors)
{
    public bool HasErrors => FailedCount > 0;
}
