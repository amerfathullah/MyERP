using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Auto-matches unreconciled bank transactions against unlinked Payment Entries and
/// bank-touching Journal Entries.
/// Per ERPNext bank_reconciliation_tool.py:
/// - Auto-reconcile uses STRICT reference_number matching only (exact match required).
/// - Manual matching uses ranked scoring (amount + date + reference).
/// - Background job threshold: >10 transactions = batch processing.
/// - get_matching_queries ranks Payment Entry AND Journal Entry as candidates (also Sales
///   Invoice for POS is_paid invoices — not covered here, narrower scope than full parity).
///
/// Known remaining gap vs ERPNext (deferred, not folded into this fix): no party_rank term.
/// BankTransaction has no PartyType/PartyId at all (only BankTransactionRule — a different
/// auto-create-voucher feature — carries a party, as a rule ACTION, not a transaction field),
/// so party can't be compared for ranking without first adding those fields to BankTransaction.
/// </summary>
public class BankAutoMatchService : ApplicationService
{
    /// <summary>JE voucher types that can touch a bank/cash GL account. Mirrors
    /// BankClearanceAppService's restriction so both bank-side tools treat "is this JE
    /// bank-related" consistently.</summary>
    private static readonly JournalEntryVoucherType[] BankTouchingVoucherTypes =
    {
        JournalEntryVoucherType.BankEntry,
        JournalEntryVoucherType.ContraEntry,
        JournalEntryVoucherType.CreditCardEntry,
    };

    private readonly IRepository<BankTransaction, Guid> _transactionRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepository;
    private readonly IRepository<BankAccount, Guid> _bankAccountRepository;

    /// <summary>
    /// Per ERPNext: auto-reconcile runs as background job when >10 unreconciled transactions.
    /// </summary>
    public const int BackgroundJobThreshold = 10;

    public BankAutoMatchService(
        IRepository<BankTransaction, Guid> transactionRepository,
        IRepository<PaymentEntry, Guid> paymentRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> journalLineRepository,
        IRepository<BankAccount, Guid> bankAccountRepository)
    {
        _transactionRepository = transactionRepository;
        _paymentRepository = paymentRepository;
        _journalRepository = journalRepository;
        _journalLineRepository = journalLineRepository;
        _bankAccountRepository = bankAccountRepository;
    }

    /// <summary>
    /// Automatically matches unreconciled bank transactions against posted payment entries
    /// and bank-touching journal entries. Uses STRICT reference_number matching only
    /// (per ERPNext auto_reconcile_vouchers). Fuzzy/ranked matching is reserved for manual
    /// reconciliation.
    /// </summary>
    public async Task<AutoMatchResult> AutoMatchAsync(Guid bankAccountId, Guid companyId)
    {
        var txQuery = await _transactionRepository.GetQueryableAsync();
        var unreconciledTxs = txQuery
            .Where(t => t.BankAccountId == bankAccountId && !t.IsReconciled)
            .ToList();

        if (!unreconciledTxs.Any())
            return new AutoMatchResult { MatchedCount = 0, UnmatchedCount = 0 };

        var peQuery = await _paymentRepository.GetQueryableAsync();
        var postedPayments = peQuery
            .Where(p => p.CompanyId == companyId
                     && p.Status == Core.DocumentStatus.Posted)
            .ToList();

        var reconciledPeIds = txQuery
            .Where(t => t.PaymentEntryId.HasValue && t.IsReconciled)
            .Select(t => t.PaymentEntryId!.Value)
            .ToHashSet();

        var unmatchedPayments = postedPayments
            .Where(p => !reconciledPeIds.Contains(p.Id))
            .ToList();

        var unmatchedJournalEntries = await GetUnmatchedBankJournalEntriesAsync(bankAccountId, companyId, txQuery);

        int matchedCount = 0;
        int partiallyReconciledCount = 0;

        foreach (var tx in unreconciledTxs)
        {
            // Auto-reconcile: STRICT reference_number match required
            var peMatch = FindStrictReferenceMatch(tx, unmatchedPayments);
            if (peMatch != null)
            {
                tx.Reconcile(peMatch.Id, peMatch.PaymentNumber);
                await _transactionRepository.UpdateAsync(tx);

                // Feed the match into ClearanceDate — see BankReconciliationAppService.ReconcileAsync
                // for why the Bank Reconciliation Statement needs this, not just IsReconciled.
                peMatch.SetClearanceDate(tx.TransactionDate);
                await _paymentRepository.UpdateAsync(peMatch);

                unmatchedPayments.Remove(peMatch);
                matchedCount++;
                continue;
            }

            var jeMatch = FindStrictReferenceMatch(tx, unmatchedJournalEntries, bankAccountId);
            if (jeMatch != null)
            {
                tx.ReconcileWithJournalEntry(jeMatch.Entry.Id, jeMatch.Entry.EntryNumber);
                await _transactionRepository.UpdateAsync(tx);

                jeMatch.Entry.SetClearanceDate(tx.TransactionDate);
                await _journalRepository.UpdateAsync(jeMatch.Entry);

                unmatchedJournalEntries.Remove(jeMatch);
                matchedCount++;
            }
        }

        return new AutoMatchResult
        {
            MatchedCount = matchedCount,
            PartiallyReconciledCount = partiallyReconciledCount,
            UnmatchedCount = unreconciledTxs.Count - matchedCount
        };
    }

    /// <summary>
    /// Finds matching vouchers for manual reconciliation using ranked scoring, across both
    /// Payment Entries and bank-touching Journal Entries.
    /// Returns candidates sorted by match quality (highest rank first).
    /// Per ERPNext check_matching: rank = ref_rank + amount_rank (+ date proximity here).
    /// </summary>
    public async Task<List<MatchCandidate>> GetMatchCandidatesAsync(
        Guid bankTransactionId, Guid companyId)
    {
        var tx = await _transactionRepository.GetAsync(bankTransactionId);

        var peQuery = await _paymentRepository.GetQueryableAsync();
        var peCandidates = peQuery
            .Where(p => p.CompanyId == companyId
                     && p.Status == Core.DocumentStatus.Posted)
            .ToList();

        var txQuery = await _transactionRepository.GetQueryableAsync();
        var reconciledPeIds = txQuery
            .Where(t => t.PaymentEntryId.HasValue && t.IsReconciled)
            .Select(t => t.PaymentEntryId!.Value)
            .ToHashSet();

        var results = peCandidates
            .Where(p => !reconciledPeIds.Contains(p.Id))
            .Where(p => AmountsMatch(tx, p))
            .Select(p => new MatchCandidate
            {
                VoucherType = "PaymentEntry",
                PaymentEntryId = p.Id,
                PaymentNumber = p.PaymentNumber,
                Amount = p.PaidAmount,
                PostingDate = p.PostingDate,
                ReferenceNumber = p.ReferenceNumber,
                Rank = CalculateRank(tx, p.ReferenceNumber, p.PostingDate, ExactAmountMatch(tx, p)),
            })
            .ToList();

        var unmatchedJes = await GetUnmatchedBankJournalEntriesAsync(tx.BankAccountId, companyId, txQuery);
        results.AddRange(unmatchedJes
            .Where(j => JeAmountsMatch(tx, j))
            .Select(j => new MatchCandidate
            {
                VoucherType = "JournalEntry",
                JournalEntryId = j.Entry.Id,
                PaymentNumber = j.Entry.EntryNumber,
                Amount = j.BankAmount,
                PostingDate = j.Entry.PostingDate,
                ReferenceNumber = j.Entry.ReferenceNumber,
                Rank = CalculateRank(tx, j.Entry.ReferenceNumber, j.Entry.PostingDate, JeExactAmountMatch(tx, j)),
            }));

        return results.OrderByDescending(c => c.Rank).ToList();
    }

    /// <summary>Loads bank-touching JEs on this bank account not yet linked to any reconciled
    /// transaction, aggregating each JE's net movement on that one account.</summary>
    private async Task<List<JeCandidate>> GetUnmatchedBankJournalEntriesAsync(
        Guid bankAccountId, Guid companyId, IQueryable<BankTransaction> txQuery)
    {
        var bankAccount = await _bankAccountRepository.FindAsync(bankAccountId);
        if (bankAccount == null) return new List<JeCandidate>();

        var reconciledJeIds = txQuery
            .Where(t => t.JournalEntryId.HasValue && t.IsReconciled)
            .Select(t => t.JournalEntryId!.Value)
            .ToHashSet();

        var jeQuery = await _journalRepository.GetQueryableAsync();
        var candidateEntries = jeQuery
            .Where(je => je.CompanyId == companyId
                && je.Status == Core.DocumentStatus.Posted
                && !je.IsOpening
                && BankTouchingVoucherTypes.Contains(je.VoucherType)
                && !reconciledJeIds.Contains(je.Id))
            .ToList();

        if (candidateEntries.Count == 0) return new List<JeCandidate>();

        var jeIds = candidateEntries.Select(je => je.Id).ToHashSet();
        var lineQuery = await _journalLineRepository.GetQueryableAsync();
        var bankLines = lineQuery
            .Where(l => l.AccountId == bankAccount.AccountId && jeIds.Contains(l.JournalEntryId))
            .ToList();

        return bankLines
            .GroupBy(l => l.JournalEntryId)
            .Select(g => new JeCandidate
            {
                Entry = candidateEntries.First(je => je.Id == g.Key),
                BankAmount = g.Where(l => l.IsDebit).Sum(l => l.Amount) - g.Where(l => !l.IsDebit).Sum(l => l.Amount),
            })
            .ToList();
    }

    /// <summary>
    /// Auto-reconcile: STRICT reference_number match + amount match.
    /// Per ERPNext: auto_reconcile flag adds WHERE reference_no = tx.reference_number.
    /// Only matches when BOTH reference AND amount are exact.
    /// </summary>
    private static PaymentEntry? FindStrictReferenceMatch(BankTransaction tx, List<PaymentEntry> payments)
    {
        if (string.IsNullOrEmpty(tx.ReferenceNumber))
            return null;

        return payments.FirstOrDefault(p =>
            AmountsMatch(tx, p) &&
            !string.IsNullOrEmpty(p.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(p.ReferenceNumber, StringComparison.OrdinalIgnoreCase));
    }

    private static JeCandidate? FindStrictReferenceMatch(BankTransaction tx, List<JeCandidate> journalEntries, Guid bankAccountId)
    {
        if (string.IsNullOrEmpty(tx.ReferenceNumber))
            return null;

        return journalEntries.FirstOrDefault(j =>
            JeAmountsMatch(tx, j) &&
            !string.IsNullOrEmpty(j.Entry.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(j.Entry.ReferenceNumber, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calculates composite rank score for manual matching: base(1) + ref match + amount match
    /// + date proximity. No party term — BankTransaction carries no party fields (see class doc).
    /// </summary>
    private static int CalculateRank(BankTransaction tx, string? voucherReferenceNumber, DateTime voucherPostingDate, bool exactAmountMatch)
    {
        int rank = 1; // base rank

        if (!string.IsNullOrEmpty(tx.ReferenceNumber) &&
            !string.IsNullOrEmpty(voucherReferenceNumber) &&
            tx.ReferenceNumber.Equals(voucherReferenceNumber, StringComparison.OrdinalIgnoreCase))
            rank++;

        if (exactAmountMatch)
            rank++;

        if (Math.Abs((tx.TransactionDate - voucherPostingDate).TotalDays) <= 3)
            rank++;

        return rank;
    }

    private static bool AmountsMatch(BankTransaction tx, PaymentEntry pe)
    {
        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);

        if (tx.Deposit > 0 || tx.Amount > 0)
            return pe.PaymentType is PaymentType.Receive or PaymentType.InternalTransfer
                && Math.Abs(txAmount - pe.PaidAmount) < 0.01m;
        if (tx.Withdrawal > 0 || tx.Amount < 0)
            return pe.PaymentType is PaymentType.Pay or PaymentType.InternalTransfer
                && Math.Abs(txAmount - pe.PaidAmount) < 0.01m;
        return false;
    }

    private static bool ExactAmountMatch(BankTransaction tx, PaymentEntry pe)
    {
        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);
        return Math.Abs(txAmount - pe.PaidAmount) < 0.01m;
    }

    /// <summary>Deposit (money in) means the bank account line is a net Debit; withdrawal
    /// means a net Credit — mirrors AmountsMatch's PaymentType direction check for PEs.</summary>
    private static bool JeAmountsMatch(BankTransaction tx, JeCandidate je)
    {
        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);

        bool isDeposit = tx.Deposit > 0 || tx.Amount > 0;
        decimal expectedDirection = isDeposit ? je.BankAmount : -je.BankAmount;
        return expectedDirection > 0 && Math.Abs(txAmount - Math.Abs(je.BankAmount)) < 0.01m;
    }

    private static bool JeExactAmountMatch(BankTransaction tx, JeCandidate je)
    {
        decimal txAmount = tx.Deposit > 0 ? tx.Deposit : tx.Withdrawal;
        if (txAmount == 0) txAmount = Math.Abs(tx.Amount);
        return Math.Abs(txAmount - Math.Abs(je.BankAmount)) < 0.01m;
    }

    private class JeCandidate
    {
        public JournalEntry Entry { get; set; } = null!;
        /// <summary>Net movement on the bank's GL account for this JE (positive = debit/deposit,
        /// negative = credit/withdrawal).</summary>
        public decimal BankAmount { get; set; }
    }
}

public class AutoMatchResult
{
    public int MatchedCount { get; set; }
    public int PartiallyReconciledCount { get; set; }
    public int UnmatchedCount { get; set; }
}

public class MatchCandidate
{
    /// <summary>"PaymentEntry" or "JournalEntry".</summary>
    public string VoucherType { get; set; } = "PaymentEntry";
    public Guid? PaymentEntryId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? PaymentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public int Rank { get; set; }
}
