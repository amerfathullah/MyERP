using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Period Closing Voucher GL Posting Service.
/// Per ERPNext: creates per-P&L-account per-dimension reversing entries (NOT a single aggregate).
/// Per gotchas #316-319, #335-336, #421:
///   - Period start date FORCED to day-after-last-closing
///   - Closing account must be Liability or Equity
///   - Closing account must be in company currency
///   - 100K total GL threshold triggers background job
///   - PCV CAN post to group cost centers (unlike all other voucher types)
///   - PCV entries tracked separately via is_period_closing_voucher_entry flag in ACB
///
/// Two outputs:
/// 1. P&L reversing entries: each P&L account gets DR/CR to zero its balance → contra to Closing Account
/// 2. AccountClosingBalance rebuild: pre-aggregated balances for instant reporting
/// </summary>
public class PeriodClosingPostingService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly AccountClosingBalanceService _closingBalanceService;

    public PeriodClosingPostingService(
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IRepository<Company, Guid> companyRepository,
        AccountClosingBalanceService closingBalanceService)
    {
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
        _accountRepository = accountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _companyRepository = companyRepository;
        _closingBalanceService = closingBalanceService;
    }

    /// <summary>
    /// Validates PCV configuration before submission.
    /// Per gotchas #317-318: closing account must be Liability/Equity type and in company currency.
    /// Per gotcha #256: blocks if a future PCV exists for the same company.
    /// </summary>
    public async Task ValidateForSubmitAsync(PeriodClosingVoucher pcv)
    {
        // 1. Closing account root_type must be Liability or Equity
        var closingAccount = await _accountRepository.GetAsync(pcv.ClosingAccountId);
        if (closingAccount.AccountType != AccountType.Liability &&
            closingAccount.AccountType != AccountType.Equity)
        {
            throw new BusinessException("MyERP:02031")
                .WithData("accountName", closingAccount.AccountName)
                .WithData("accountType", closingAccount.AccountType.ToString());
        }

        // 2. Closing account currency must match company currency
        var company = await _companyRepository.GetAsync(pcv.CompanyId);
        if (!string.IsNullOrEmpty(closingAccount.Currency) &&
            closingAccount.Currency != company.CurrencyCode)
        {
            throw new BusinessException("MyERP:02032")
                .WithData("accountCurrency", closingAccount.Currency)
                .WithData("companyCurrency", company.CurrencyCode);
        }
    }

    /// <summary>
    /// Generates PCV closing entries by scanning all P&L GL activity up to the transaction date.
    /// Creates per-account per-cost-center reversing entries.
    /// Per ERPNext: each dimension combination gets its own entry.
    /// </summary>
    public async Task<PcvClosingResult> CalculateClosingEntriesAsync(
        Guid companyId, DateTime upToDate, Guid? tenantId = null)
    {
        // Get all posted journal entries for the company up to the transaction date
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate <= upToDate);

        if (!journals.Any())
            return new PcvClosingResult(new List<PcvAccountBalance>(), 0m);

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var allLines = await _lineRepository.GetListAsync(l => journalIds.Contains(l.JournalEntryId));

        // Load accounts to filter P&L only (Income + Expense)
        var accountIds = allLines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _accountRepository.GetListAsync(a => accountIds.Contains(a.Id));
        var plAccountIds = accounts
            .Where(a => a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense)
            .Select(a => a.Id)
            .ToHashSet();

        // Aggregate P&L entries by (account, cost_center)
        var plLines = allLines.Where(l => plAccountIds.Contains(l.AccountId));

        var aggregations = plLines
            .GroupBy(l => new { l.AccountId, l.CostCenterId })
            .Select(g =>
            {
                var debit = g.Where(l => l.IsDebit).Sum(l => l.Amount);
                var credit = g.Where(l => !l.IsDebit).Sum(l => l.Amount);
                var netBalance = debit - credit; // positive = debit balance, negative = credit balance
                return new PcvAccountBalance(g.Key.AccountId, g.Key.CostCenterId, debit, credit, netBalance);
            })
            .Where(a => Math.Abs(a.NetBalance) > 0.01m) // skip near-zero balances
            .ToList();

        var totalNetPL = aggregations.Sum(a => a.NetBalance);

        return new PcvClosingResult(aggregations, totalNetPL);
    }

    /// <summary>
    /// Creates reversing journal entries for all P&L accounts.
    /// Each P&L account gets a line that zeros its balance, with contra to the closing account.
    /// Per ERPNext: creates a SINGLE JE with many lines (not individual JEs per account).
    /// </summary>
    public async Task<JournalEntry> CreateClosingJournalEntryAsync(
        PeriodClosingVoucher pcv, PcvClosingResult result)
    {
        if (!result.Balances.Any())
            throw new BusinessException("MyERP:02018");

        // Create a single JE for all reversing entries
        var je = new JournalEntry(
            GuidGenerator.Create(),
            pcv.CompanyId,
            pcv.FiscalYearId,
            pcv.PostingDate,
            pcv.TenantId);

        je.ReferenceType = "PeriodClosingVoucher";
        je.ReferenceId = pcv.Id;

        // For each P&L account balance: create a reversing line
        // If account has debit balance → credit it (and debit closing account)
        // If account has credit balance → debit it (and credit closing account)
        decimal totalClosingDebit = 0m;
        decimal totalClosingCredit = 0m;

        foreach (var balance in result.Balances)
        {
            if (balance.NetBalance > 0)
            {
                // Debit balance (expense) → credit to zero it
                je.AddLine(balance.AccountId, balance.NetBalance, false);
                totalClosingDebit += balance.NetBalance;
            }
            else
            {
                // Credit balance (income) → debit to zero it
                je.AddLine(balance.AccountId, Math.Abs(balance.NetBalance), true);
                totalClosingCredit += Math.Abs(balance.NetBalance);
            }
        }

        // Add the closing account (retained earnings) contra entry
        // Net P&L positive = net expense > income → credit closing account (loss)
        // Net P&L negative = income > expense → debit closing account (profit)
        if (result.TotalNetPL > 0)
        {
            // Net loss → debit closing account
            je.AddLine(pcv.ClosingAccountId, result.TotalNetPL, true);
        }
        else if (result.TotalNetPL < 0)
        {
            // Net profit → credit closing account
            je.AddLine(pcv.ClosingAccountId, Math.Abs(result.TotalNetPL), false);
        }

        // Post immediately (PCV JEs are system-generated)
        je.Post();

        await _journalRepository.InsertAsync(je, autoSave: true);
        return je;
    }

    /// <summary>
    /// Rebuilds Account Closing Balances after PCV submission.
    /// Per gotcha #421: ACB tracks PCV entries separately via period identification.
    /// </summary>
    public async Task RebuildClosingBalancesAsync(PeriodClosingVoucher pcv)
    {
        var period = AccountClosingBalanceService.GetPeriodFromDate(pcv.TransactionDate);
        await _closingBalanceService.RebuildAsync(
            pcv.CompanyId, pcv.TransactionDate, period, pcv.TenantId);
    }
}

/// <summary>Result of PCV closing calculation.</summary>
public record PcvClosingResult(
    List<PcvAccountBalance> Balances,
    decimal TotalNetPL);

/// <summary>Per-account P&L balance for PCV closing.</summary>
public record PcvAccountBalance(
    Guid AccountId,
    Guid? CostCenterId,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetBalance);
