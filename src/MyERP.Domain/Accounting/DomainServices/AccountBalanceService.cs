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
/// Account Balance Service — the central query service for real-time account balances.
/// ERPNext equivalent: accounts/utils.py → get_balance_on()
/// 
/// Key feature: uses Account Closing Balance as opening + delta GL entries for efficiency.
/// If no closing balance exists, falls back to full GL scan from inception.
/// 
/// Used by: Trial Balance, Balance Sheet, P&L, Budget validation, Credit Limit checks,
///          Exchange Rate Revaluation, Reconciliation, Dashboard KPIs.
/// 
/// Balance is ALWAYS cumulative from inception to the specified date (ALL-TIME, not FY-scoped).
/// Per ERPNext: "the balance shown is from the inception of the company" (not fiscal year start).
/// 
/// For P&L period reports (revenue/expense for a specific period), callers compute:
///   period_balance = balance_at_end - balance_at_start
/// </summary>
public class AccountBalanceService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;
    private readonly IRepository<AccountClosingBalance, Guid> _closingBalanceRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public AccountBalanceService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository,
        IRepository<AccountClosingBalance, Guid> closingBalanceRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
        _closingBalanceRepository = closingBalanceRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// Gets the balance of an account as of a specific date (cumulative from inception).
    /// Returns: (Debit, Credit, Balance) where Balance = Debit - Credit.
    /// 
    /// Optimization: uses latest AccountClosingBalance as starting point if available,
    /// then only scans GL entries AFTER the closing date.
    /// </summary>
    public async Task<AccountBalanceResult> GetBalanceOnAsync(
        Guid companyId,
        Guid accountId,
        DateTime asOfDate,
        Guid? costCenterId = null,
        string? financeBook = null)
    {
        // Strategy: find latest closing balance ≤ asOfDate, then add delta
        var closingBalance = await FindLatestClosingBalanceAsync(companyId, accountId, asOfDate, costCenterId);

        decimal openingDebit = 0m;
        decimal openingCredit = 0m;
        DateTime scanFromDate;

        if (closingBalance != null)
        {
            openingDebit = closingBalance.Debit;
            openingCredit = closingBalance.Credit;
            scanFromDate = closingBalance.ClosingDate.AddDays(1); // Start AFTER closing date
        }
        else
        {
            scanFromDate = DateTime.MinValue; // Scan from inception
        }

        // Get GL entries from scanFromDate to asOfDate
        var (deltaDebit, deltaCredit) = await GetGlTotalsAsync(
            companyId, accountId, scanFromDate, asOfDate, costCenterId, financeBook);

        var totalDebit = openingDebit + deltaDebit;
        var totalCredit = openingCredit + deltaCredit;

        return new AccountBalanceResult(totalDebit, totalCredit);
    }

    /// <summary>
    /// Gets balances for ALL accounts in a company as of a date (for Trial Balance).
    /// Returns dictionary: AccountId → (Debit, Credit, Balance).
    /// </summary>
    public async Task<Dictionary<Guid, AccountBalanceResult>> GetTrialBalanceAsync(
        Guid companyId,
        DateTime asOfDate,
        Guid? costCenterId = null,
        string? financeBook = null)
    {
        // Get all posted JEs up to date
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate <= asOfDate);

        if (!journals.Any())
            return new Dictionary<Guid, AccountBalanceResult>();

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var allLines = await _lineRepository.GetListAsync(l => journalIds.Contains(l.JournalEntryId));

        // Apply filters
        var filteredLines = allLines.AsEnumerable();
        if (costCenterId.HasValue)
            filteredLines = filteredLines.Where(l => l.CostCenterId == costCenterId);
        if (!string.IsNullOrWhiteSpace(financeBook))
            filteredLines = filteredLines.Where(l => l.FinanceBook == financeBook || l.FinanceBook == null);

        // Aggregate by account
        return filteredLines
            .GroupBy(l => l.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new AccountBalanceResult(
                    g.Where(l => l.IsDebit).Sum(l => l.Amount),
                    g.Where(l => !l.IsDebit).Sum(l => l.Amount)));
    }

    /// <summary>
    /// Gets period balance (activity during a specific date range).
    /// Used for P&L reports: revenue/expenses for a period.
    /// </summary>
    public async Task<AccountBalanceResult> GetPeriodBalanceAsync(
        Guid companyId,
        Guid accountId,
        DateTime fromDate,
        DateTime toDate,
        Guid? costCenterId = null,
        string? financeBook = null)
    {
        var (debit, credit) = await GetGlTotalsAsync(
            companyId, accountId, fromDate, toDate, costCenterId, financeBook);

        return new AccountBalanceResult(debit, credit);
    }

    /// <summary>
    /// Gets balance for a party (customer/supplier) — subledger balance.
    /// Used for: outstanding calculation, aging reports, credit limit checks.
    /// </summary>
    public async Task<AccountBalanceResult> GetPartyBalanceAsync(
        Guid companyId,
        string partyType,
        Guid partyId,
        DateTime asOfDate,
        Guid? accountId = null)
    {
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate <= asOfDate);

        if (!journals.Any())
            return AccountBalanceResult.Zero;

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var allLines = await _lineRepository.GetListAsync(l =>
            journalIds.Contains(l.JournalEntryId) &&
            l.PartyType == partyType &&
            l.PartyId == partyId);

        if (accountId.HasValue)
            allLines = allLines.Where(l => l.AccountId == accountId.Value).ToList();

        return new AccountBalanceResult(
            allLines.Where(l => l.IsDebit).Sum(l => l.Amount),
            allLines.Where(l => !l.IsDebit).Sum(l => l.Amount));
    }

    /// <summary>
    /// Gets account balance in account currency (for multi-currency accounts).
    /// Used for: exchange rate revaluation, multi-currency reports.
    /// </summary>
    public async Task<(decimal BalanceInAccountCurrency, decimal BalanceInCompanyCurrency)> GetMultiCurrencyBalanceAsync(
        Guid companyId,
        Guid accountId,
        DateTime asOfDate)
    {
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate <= asOfDate);

        if (!journals.Any())
            return (0m, 0m);

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var lines = await _lineRepository.GetListAsync(l =>
            journalIds.Contains(l.JournalEntryId) && l.AccountId == accountId);

        var balanceInCompanyCurrency = lines.Sum(l => l.IsDebit ? l.Amount : -l.Amount);
        var balanceInAccountCurrency = lines.Sum(l =>
            l.IsDebit ? l.AmountInAccountCurrency : -l.AmountInAccountCurrency);

        return (balanceInAccountCurrency, balanceInCompanyCurrency);
    }

    #region Private Helpers

    private async Task<AccountClosingBalance?> FindLatestClosingBalanceAsync(
        Guid companyId, Guid accountId, DateTime beforeDate, Guid? costCenterId)
    {
        var balances = await _closingBalanceRepository.GetListAsync(cb =>
            cb.CompanyId == companyId &&
            cb.AccountId == accountId &&
            cb.CostCenterId == costCenterId &&
            cb.ClosingDate <= beforeDate);

        return balances.OrderByDescending(cb => cb.ClosingDate).FirstOrDefault();
    }

    private async Task<(decimal Debit, decimal Credit)> GetGlTotalsAsync(
        Guid companyId, Guid accountId, DateTime fromDate, DateTime toDate,
        Guid? costCenterId = null, string? financeBook = null)
    {
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate >= fromDate &&
            je.PostingDate <= toDate);

        if (!journals.Any())
            return (0m, 0m);

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var lines = await _lineRepository.GetListAsync(l =>
            journalIds.Contains(l.JournalEntryId) && l.AccountId == accountId);

        var filteredLines = lines.AsEnumerable();
        if (costCenterId.HasValue)
            filteredLines = filteredLines.Where(l => l.CostCenterId == costCenterId);
        if (!string.IsNullOrWhiteSpace(financeBook))
            filteredLines = filteredLines.Where(l => l.FinanceBook == financeBook || l.FinanceBook == null);

        var debit = filteredLines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var credit = filteredLines.Where(l => !l.IsDebit).Sum(l => l.Amount);

        return (debit, credit);
    }

    #endregion
}

/// <summary>
/// Result of an account balance query.
/// </summary>
public record AccountBalanceResult(decimal Debit, decimal Credit)
{
    /// <summary>Net balance: Debit - Credit. Positive = debit balance (asset/expense), negative = credit balance (liability/equity/revenue).</summary>
    public decimal Balance => Debit - Credit;

    /// <summary>Returns the absolute closing value.</summary>
    public decimal ClosingBalance => Math.Abs(Balance);

    /// <summary>Returns true if account has a debit balance.</summary>
    public bool IsDebitBalance => Balance > 0;

    /// <summary>Returns true if account has a credit balance.</summary>
    public bool IsCreditBalance => Balance < 0;

    public static AccountBalanceResult Zero => new(0m, 0m);

    /// <summary>Adds two results together (for tree accumulation).</summary>
    public AccountBalanceResult Add(AccountBalanceResult other)
        => new(Debit + other.Debit, Credit + other.Credit);
}
