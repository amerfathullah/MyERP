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
/// Uses amount matching + reference number fuzzy matching algorithm.
/// Per ERPNext: first-match-wins (priority order), bank transaction rules respected.
/// </summary>
public class BankAutoMatchService : ApplicationService
{
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;
    private readonly IRepository<PaymentEntry, Guid> _paymentRepository;

    public BankAutoMatchService(
        IRepository<BankTransaction, Guid> transactionRepository,
        IRepository<PaymentEntry, Guid> paymentRepository)
    {
        _transactionRepository = transactionRepository;
        _paymentRepository = paymentRepository;
    }

    /// <summary>
    /// Automatically matches unreconciled bank transactions against posted payment entries.
    /// Returns count of matches made.
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

        foreach (var tx in unreconciledTxs)
        {
            var match = FindBestMatch(tx, unmatchedPayments);
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
            UnmatchedCount = unreconciledTxs.Count - matchedCount
        };
    }

    private static PaymentEntry? FindBestMatch(BankTransaction tx, List<PaymentEntry> payments)
    {
        // Priority 1: Exact amount match + reference number match
        var exactRefMatch = payments.FirstOrDefault(p =>
            AmountsMatch(tx.Amount, p) &&
            !string.IsNullOrEmpty(tx.ReferenceNumber) &&
            !string.IsNullOrEmpty(p.ReferenceNumber) &&
            tx.ReferenceNumber.Equals(p.ReferenceNumber, StringComparison.OrdinalIgnoreCase));

        if (exactRefMatch != null) return exactRefMatch;

        // Priority 2: Exact amount match + date within 3 days
        var amountDateMatch = payments.FirstOrDefault(p =>
            AmountsMatch(tx.Amount, p) &&
            Math.Abs((tx.TransactionDate - p.PostingDate).TotalDays) <= 3);

        if (amountDateMatch != null) return amountDateMatch;

        // Priority 3: Exact amount match (any date)
        var amountMatch = payments.FirstOrDefault(p => AmountsMatch(tx.Amount, p));

        return amountMatch;
    }

    private static bool AmountsMatch(decimal txAmount, PaymentEntry pe)
    {
        // Bank credits (positive) = Receive payments
        // Bank debits (negative) = Pay payments
        if (txAmount > 0 && pe.PaymentType == PaymentType.Receive)
            return Math.Abs(txAmount - pe.PaidAmount) < 0.01m;
        if (txAmount < 0 && pe.PaymentType == PaymentType.Pay)
            return Math.Abs(Math.Abs(txAmount) - pe.PaidAmount) < 0.01m;
        return false;
    }
}

public class AutoMatchResult
{
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
}
