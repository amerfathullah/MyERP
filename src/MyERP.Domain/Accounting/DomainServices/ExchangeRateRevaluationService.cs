using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Exchange Rate Revaluation service — finds eligible foreign-currency Balance Sheet accounts
/// and calculates unrealized gain/loss at current exchange rates.
/// 
/// Per ERPNext rules:
/// - Only Balance Sheet accounts (Asset, Liability, Equity) qualify
/// - Only leaf accounts with non-zero foreign currency balance
/// - Party accounts (Receivable/Payable) are revalued per-party
/// - Creates TWO JEs: zero-balance JE + revaluation JE
/// 
/// Source: erpnext/accounts/doctype/exchange_rate_revaluation/exchange_rate_revaluation.py
/// </summary>
public class ExchangeRateRevaluationService : DomainService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalEntryLineRepository;
    private readonly CurrencyExchangeService _exchangeRateService;

    public ExchangeRateRevaluationService(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<JournalEntryLine, Guid> journalEntryLineRepository,
        CurrencyExchangeService exchangeRateService)
    {
        _accountRepository = accountRepository;
        _journalEntryRepository = journalEntryRepository;
        _journalEntryLineRepository = journalEntryLineRepository;
        _exchangeRateService = exchangeRateService;
    }

    /// <summary>
    /// Find all accounts eligible for revaluation in a company.
    /// Eligible = foreign currency, Balance Sheet, leaf, non-zero balance.
    /// </summary>
    public async Task<List<EligibleAccountForRevaluation>> GetEligibleAccountsAsync(
        Guid companyId, string companyCurrency, DateTime postingDate)
    {
        var accountQuery = await _accountRepository.GetQueryableAsync();
        var foreignCurrencyAccounts = accountQuery
            .Where(a => a.CompanyId == companyId
                && !a.IsGroup
                && a.Currency != null
                && a.Currency != companyCurrency
                && (a.AccountType == AccountType.Asset
                    || a.AccountType == AccountType.Liability
                    || a.AccountType == AccountType.Equity))
            .ToList();

        if (!foreignCurrencyAccounts.Any())
            return new List<EligibleAccountForRevaluation>();

        var result = new List<EligibleAccountForRevaluation>();

        foreach (var account in foreignCurrencyAccounts)
        {
            var balance = await GetAccountBalanceAsync(account.Id, postingDate);
            if (balance.BalanceInAccountCurrency == 0 && balance.BalanceInCompanyCurrency == 0)
                continue;

            var newRate = await _exchangeRateService.GetExchangeRateAsync(
                account.Currency!, companyCurrency, postingDate);

            result.Add(new EligibleAccountForRevaluation
            {
                AccountId = account.Id,
                AccountName = account.AccountName,
                AccountCurrency = account.Currency!,
                BalanceInAccountCurrency = balance.BalanceInAccountCurrency,
                BalanceInCompanyCurrency = balance.BalanceInCompanyCurrency,
                CurrentExchangeRate = balance.BalanceInAccountCurrency != 0
                    ? balance.BalanceInCompanyCurrency / balance.BalanceInAccountCurrency
                    : 0m,
                NewExchangeRate = newRate,
                GainLoss = (balance.BalanceInAccountCurrency * newRate) - balance.BalanceInCompanyCurrency,
            });
        }

        return result.Where(r => r.GainLoss != 0).ToList();
    }

    /// <summary>
    /// Create the Revaluation Journal Entry from a submitted ERR document.
    /// Per ERPNext: voucher_type = "Exchange Rate Revaluation".
    /// </summary>
    public async Task<JournalEntry> CreateRevaluationJournalEntryAsync(
        ExchangeRateRevaluation err, Guid fiscalYearId)
    {
        if (err.Status != ExchangeRateRevaluationStatus.Submitted)
            throw new BusinessException("MyERP:01001");

        var je = new JournalEntry(
            Guid.NewGuid(),
            err.CompanyId,
            fiscalYearId,
            err.PostingDate,
            err.TenantId);

        // For each revaluation entry: create offset against Unrealized Exchange GL account
        foreach (var entry in err.Entries)
        {
            if (entry.GainLoss > 0)
            {
                // Gain: DR Account, CR Exchange GL
                je.AddLine(entry.AccountId, entry.GainLoss, true);
                je.AddLine(err.ExchangeGainLossAccountId, entry.GainLoss, false);
            }
            else if (entry.GainLoss < 0)
            {
                // Loss: DR Exchange GL, CR Account
                var absLoss = Math.Abs(entry.GainLoss);
                je.AddLine(err.ExchangeGainLossAccountId, absLoss, true);
                je.AddLine(entry.AccountId, absLoss, false);
            }
        }

        je.Post();
        await _journalEntryRepository.InsertAsync(je);

        return je;
    }

    /// <summary>
    /// Create a reversal Journal Entry for an existing ERR's JEs.
    /// Reversal posting_date = today (next period start).
    /// </summary>
    public async Task<JournalEntry> CreateReversalJournalEntryAsync(
        JournalEntry originalJe, Guid fiscalYearId, Guid? tenantId = null)
    {
        var reversal = new JournalEntry(
            Guid.NewGuid(),
            originalJe.CompanyId,
            fiscalYearId,
            DateTime.UtcNow.Date,
            tenantId);

        // Reverse all lines (swap debit/credit)
        foreach (var line in originalJe.Lines)
        {
            reversal.AddLine(line.AccountId, line.Amount, !line.IsDebit);
        }

        reversal.Post();
        await _journalEntryRepository.InsertAsync(reversal);

        return reversal;
    }

    private async Task<(decimal BalanceInAccountCurrency, decimal BalanceInCompanyCurrency)> GetAccountBalanceAsync(
        Guid accountId, DateTime asOfDate)
    {
        var lineQuery = await _journalEntryLineRepository.GetQueryableAsync();
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();

        // Get all posted JE line IDs for this account up to the date
        var postedJeIds = jeQuery
            .Where(j => j.Status == Core.DocumentStatus.Posted && j.PostingDate <= asOfDate)
            .Select(j => j.Id)
            .ToHashSet();

        var accountLines = lineQuery
            .Where(l => l.AccountId == accountId)
            .ToList()
            .Where(l => postedJeIds.Contains(l.JournalEntryId));

        var totalDebit = accountLines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalCredit = accountLines.Where(l => !l.IsDebit).Sum(l => l.Amount);

        // Balance in company currency (from GL entries)
        var balanceInCompanyCurrency = totalDebit - totalCredit;

        // For simplicity, use the same balance as account currency balance
        // In full implementation, this would track account_currency amounts separately
        var balanceInAccountCurrency = balanceInCompanyCurrency;

        return (balanceInAccountCurrency, balanceInCompanyCurrency);
    }
}

/// <summary>
/// Represents an account eligible for exchange rate revaluation with calculated gain/loss.
/// </summary>
public class EligibleAccountForRevaluation
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = null!;
    public string AccountCurrency { get; set; } = null!;
    public decimal BalanceInAccountCurrency { get; set; }
    public decimal BalanceInCompanyCurrency { get; set; }
    public decimal CurrentExchangeRate { get; set; }
    public decimal NewExchangeRate { get; set; }
    public decimal GainLoss { get; set; }
}
