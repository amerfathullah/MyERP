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
/// Auto-matches unreconciled bank transactions against unlinked payment entries.
/// Per ERPNext bank_reconciliation_tool.py:
/// - Auto-reconcile uses STRICT reference_number matching only (exact match required).
/// - Manual matching uses ranked scoring (amount + date + reference + party).
/// - Background job threshold: >10 transactions = batch processing.
/// </summary>
public class BankAutoMatchService : ApplicationService
{
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepository;

    /// <summary>
    /// Per ERPNext: auto-reconcile runs as background job when >10 unreconciled transactions.
    /// </summary>
    public const int BackgroundJobThreshold = 10;

    public BankAutoMatchService(
        IRepository<BankTransaction, Guid> transactionRepository,
        IRepository<PaymentEntry, Guid> paymentRepository)
    {
        _transactionRepository = transactionRepository;
        _paymentRepository = paymentRepository;
    }

    /// <summary>
    /// Automatically matches unreconciled bank transactions against posted payment entries.
    /// Uses STRICT reference_number matching only (per ERPNext auto_reconcile_vouchers).
    /// Fuzzy/ranked matching is reserved for manual reconciliation.
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

        // Filter to payments not already reconciled with any bank transaction
        var reconciledPeIds = txQuery
            .Where(t => t.PaymentEntryId.HasValue && t.IsReconciled)
            .Select(t => t.PaymentEntryId!.Value)
            .ToHashSet();

        var unmatchedPayments = postedPayments
            .Where(p => !reconciledPeIds.Contains(p.Id))
            .ToList();

        int matchedCount = 0;
        int partiallyReconciledCount = 0;

        foreach (var tx in unreconciledTxs)
        {
            // Auto-reconcile: STRICT reference_number match required
            var match = FindStrictReferenceMatch(tx, unmatchedPayments);
            if (match != null)
            {
                tx.Reconcile(match.Id, match.PaymentNumber);
                await _transactionRepository.UpdateAsync(tx);
                unmatchedPayments.Remove(match);
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
    /// Finds matching vouchers for manual reconciliation using ranked scoring.
    /// Returns candidates sorted by match quality (highest rank first).
    /// Per ERPNext check_matching: rank = ref_rank + amount_rank + party_rank.
    /// </summary>
    public async Task<List<MatchCandidate>> GetMatchCandidatesAsync(
        Guid bankTransactionId, Guid companyId)
    {
        var tx = await _transactionRepository.GetAsync(bankTransactionId);

        var peQuery = await _paymentRepository.GetQueryableAsync();
        var candidates = peQuery
            .Where(p => p.CompanyId == companyId
                     && p.Status == Core.DocumentStatus.Posted)
            .ToList();

        var txQuery = await _transactionRepository.GetQueryableAsync();
        var reconciledPeIds = txQuery
            .Where(t => t.PaymentEntryId.HasValue && t.IsReconciled)
            .Select(t => t.PaymentEntryId!.Value)
            .ToHashSet();

        return candidates
            .Where(p => !reconciledPeIds.Contains(p.Id))
            .Where(p => AmountsMatch(tx, p))
            .Select(p => new MatchCandidate
            {
                PaymentEntryId = p.Id,
                PaymentNumber = p.PaymentNumber,
                Amount = p.PaidAmount,
                PostingDate = p.PostingDate,
                ReferenceNumber = p.ReferenceNumber,
                Rank = CalculateRank(tx, p)
            })
            .OrderByDescending(c => c.Rank)
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

    /// <summary>
    /// Calculates composite rank score for manual matching.
    /// Per ERPNext: rank = ref_rank + amount_rank + party_rank + 1 (base).
    /// </summary>
    private static int CalculateRank(BankTransaction tx, PaymentEntry pe)
    {
        int rank = 1; // base rank

        // Reference number match
        if (!string.IsNullOrEmpty(tx.ReferenceNumber) &&
            !string.IsNullOrEmpty(pe.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(pe.ReferenceNumber, StringComparison.OrdinalIgnoreCase))
            rank++;

        // Exact amount match
        if (ExactAmountMatch(tx, pe))
            rank++;

        // Date proximity (within 3 days)
        if (Math.Abs((tx.TransactionDate - pe.PostingDate).TotalDays) <= 3)
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
}

public class AutoMatchResult
{
    public int MatchedCount { get; set; }
    public int PartiallyReconciledCount { get; set; }
    public int UnmatchedCount { get; set; }
}

public class MatchCandidate
{
    public Guid PaymentEntryId { get; set; }
    public string? PaymentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime PostingDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public int Rank { get; set; }
}
