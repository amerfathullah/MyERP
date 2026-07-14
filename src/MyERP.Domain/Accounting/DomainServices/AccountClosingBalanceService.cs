using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Account Closing Balance Service — builds and queries pre-aggregated period-end account balances.
/// ERPNext equivalent: accounts/doctype/account_closing_balance/account_closing_balance.py
/// 
/// Key operations:
/// 1. BuildForPeriodAsync — aggregates GL entries up to a date and stores closing balances
/// 2. GetBalanceAsync — retrieves cached balance for instant reporting
/// 3. RebuildAsync — clears and regenerates balances (after GL repost)
/// 
/// Used by: Trial Balance, Balance Sheet, Profit & Loss reports for instant queries.
/// </summary>
public class AccountClosingBalanceService : DomainService
{
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;

    public AccountClosingBalanceService(
        IRepository<AccountClosingBalance, Guid> closingBalanceRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository)
    {
        _closingBalanceRepository = closingBalanceRepository;
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
    }

    /// <summary>
    /// Builds closing balances for all accounts in a company up to the specified date.
    /// Aggregates all posted JE lines from inception to closingDate (inclusive).
    /// Stores one AccountClosingBalance row per account (and optionally per cost center).
    /// </summary>
    public async Task<int> BuildForPeriodAsync(Guid companyId, DateTime closingDate, string period, Guid? tenantId = null)
    {
        // Get all posted journal entries up to closing date for this company
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == Core.DocumentStatus.Posted &&
            je.PostingDate <= closingDate);

        if (!journals.Any())
            return 0;

        var journalIds = journals.Select(j => j.Id).ToHashSet();

        // Get all lines for these journals
        var allLines = await _lineRepository.GetListAsync(l => journalIds.Contains(l.JournalEntryId));

        // Aggregate by account (and optionally by cost center)
        var aggregations = allLines
            .GroupBy(l => new { l.AccountId, l.CostCenterId })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.CostCenterId,
                Debit = g.Where(l => l.IsDebit).Sum(l => l.Amount),
                Credit = g.Where(l => !l.IsDebit).Sum(l => l.Amount)
            })
            .ToList();

        // Delete existing closing balances for this period+company (rebuild)
        var existing = await _closingBalanceRepository.GetListAsync(cb =>
            cb.CompanyId == companyId && cb.Period == period);
        if (existing.Any())
        {
            foreach (var e in existing)
                await _closingBalanceRepository.DeleteAsync(e);
        }

        // Insert new closing balances
        int count = 0;
        foreach (var agg in aggregations)
        {
            if (agg.Debit == 0 && agg.Credit == 0)
                continue; // Skip zero-balance accounts

            var balance = new AccountClosingBalance(
                GuidGenerator.Create(),
                companyId,
                agg.AccountId,
                closingDate,
                period,
                agg.Debit,
                agg.Credit,
                agg.CostCenterId,
                tenantId: tenantId);

            await _closingBalanceRepository.InsertAsync(balance);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Gets the closing balance for a specific account at a specific period.
    /// Returns null if no closing balance exists (must fall back to GL scan).
    /// </summary>
    public async Task<AccountClosingBalance?> GetBalanceAsync(
        Guid companyId, Guid accountId, string period, Guid? costCenterId = null)
    {
        return await _closingBalanceRepository.FindAsync(cb =>
            cb.CompanyId == companyId &&
            cb.AccountId == accountId &&
            cb.Period == period &&
            cb.CostCenterId == costCenterId);
    }

    /// <summary>
    /// Gets all closing balances for a company at a specific period (for Trial Balance).
    /// </summary>
    public async Task<List<AccountClosingBalance>> GetAllBalancesAsync(Guid companyId, string period)
    {
        return await _closingBalanceRepository.GetListAsync(cb =>
            cb.CompanyId == companyId && cb.Period == period);
    }

    /// <summary>
    /// Gets the latest closing balance period for a company.
    /// Used to determine the starting point for incremental GL queries.
    /// </summary>
    public async Task<AccountClosingBalance?> GetLatestClosingAsync(Guid companyId)
    {
        var all = await _closingBalanceRepository.GetListAsync(cb => cb.CompanyId == companyId);
        return all.OrderByDescending(cb => cb.ClosingDate).FirstOrDefault();
    }

    /// <summary>
    /// Rebuilds closing balances from scratch for a company at a given date.
    /// Called after GL repost, Period Closing Voucher submission, or manual trigger.
    /// </summary>
    public async Task<int> RebuildAsync(Guid companyId, DateTime closingDate, string period, Guid? tenantId = null)
    {
        // Simply delegate to BuildForPeriodAsync which handles delete-and-rebuild
        return await BuildForPeriodAsync(companyId, closingDate, period, tenantId);
    }

    /// <summary>
    /// Deletes all closing balances for a company at a specific period.
    /// Called when a Period Closing Voucher is cancelled.
    /// </summary>
    public async Task DeleteForPeriodAsync(Guid companyId, string period)
    {
        var existing = await _closingBalanceRepository.GetListAsync(cb =>
            cb.CompanyId == companyId && cb.Period == period);

        foreach (var e in existing)
            await _closingBalanceRepository.DeleteAsync(e);
    }

    /// <summary>
    /// Generates period string from a date (format: "YYYY-MM").
    /// </summary>
    public static string GetPeriodFromDate(DateTime date)
    {
        return date.ToString("yyyy-MM");
    }
}
