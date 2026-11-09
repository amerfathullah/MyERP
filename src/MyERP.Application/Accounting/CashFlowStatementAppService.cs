using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Cash Flow Statement report — the third mandatory financial statement.
/// Uses the indirect method (starts from Net Profit, adjusts for non-cash items).
/// 
/// ERPNext equivalent: accounts/report/cash_flow/cash_flow.py
/// 
/// Structure:
/// - Operating Activities (Net Profit + non-cash adjustments + working capital changes)
/// - Investing Activities (asset purchases/disposals)
/// - Financing Activities (equity, loans, dividends)
/// = Net Change in Cash
/// + Opening Cash Balance
/// = Closing Cash Balance
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class CashFlowStatementAppService : ApplicationService, ICashFlowStatementAppService
{
    private readonly AccountBalanceService _balanceService;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<JournalEntryLine, Guid> _lineRepository;

    public CashFlowStatementAppService(
        AccountBalanceService balanceService,
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<JournalEntryLine, Guid> lineRepository)
    {
        _balanceService = balanceService;
        _accountRepository = accountRepository;
        _journalRepository = journalRepository;
        _lineRepository = lineRepository;
    }

    public async Task<CashFlowStatementDto> GetCashFlowStatementAsync(CashFlowRequestDto input)
    {
        var accounts = await _accountRepository.GetListAsync(a => a.CompanyId == input.CompanyId && !a.IsGroup);

        // Categorize accounts by type for the statement
        var cashAccounts = accounts.Where(a =>
            a.AccountSubType == AccountSubType.CashAccount ||
            a.AccountSubType == AccountSubType.BankAccount).ToList();

        var fixedAssetAccounts = accounts.Where(a =>
            a.AccountSubType == AccountSubType.FixedAsset ||
            a.AccountSubType == AccountSubType.AccumulatedDepreciation ||
            a.AccountSubType == AccountSubType.CapitalWorkInProgress).ToList();

        var equityAccounts = accounts.Where(a =>
            a.AccountType == AccountType.Equity).ToList();

        // Opening cash = cash account balance at start of report range (FromDate - 1 day)
        decimal initialOpeningCash = 0;
        foreach (var cashAccount in cashAccounts)
        {
            var balance = await _balanceService.GetBalanceOnAsync(input.CompanyId, cashAccount.Id, input.FromDate.Date.AddDays(-1));
            initialOpeningCash += balance.Balance;
        }

        var fiscalYears = new List<FiscalYear>();
        var fyRepo = LazyServiceProvider?.LazyGetService<IRepository<FiscalYear, Guid>>();
        if (fyRepo != null)
        {
            var fyQuery = await fyRepo.GetQueryableAsync();
            fiscalYears = fyQuery.Where(fy => fy.CompanyId == input.CompanyId).ToList();
        }

        var periodDefs = BuildPeriods(input.FromDate.Date, input.ToDate.Date, input.Periodicity, fiscalYears, input.AccumulatedValues);
        var periodColumns = new List<CashFlowPeriodColumnDto>();
        var periodSectionResults = new List<(DateTime EffectiveStartDate, List<CashFlowLineItem> Operating, List<CashFlowLineItem> Investing, List<CashFlowLineItem> Financing)>();

        foreach (var p in periodDefs)
        {
            var periodBalances = await GetPeriodBalancesAsync(input.CompanyId, p.EffectiveStartDate, p.ToDate, accounts);
            var (opItems, opTotal, invItems, invTotal, finItems, finTotal) = ComputeSections(accounts, cashAccounts, fixedAssetAccounts, equityAccounts, periodBalances);
            var netChange = opTotal + invTotal + finTotal;

            periodColumns.Add(new CashFlowPeriodColumnDto
            {
                Key = p.Key,
                Label = p.Label,
                FromDate = p.FromDate,
                ToDate = p.ToDate,
                EffectiveStartDate = p.EffectiveStartDate,
                OperatingTotal = opTotal,
                InvestingTotal = invTotal,
                FinancingTotal = finTotal,
                NetCashChange = netChange
            });

            periodSectionResults.Add((p.EffectiveStartDate, opItems, invItems, finItems));
        }

        CalculateOpeningAndClosingBalances(periodColumns, initialOpeningCash);

        List<CashFlowLineItem> operatingItems;
        List<CashFlowLineItem> investingItems;
        List<CashFlowLineItem> financingItems;
        decimal operatingTotal;
        decimal investingTotal;
        decimal financingTotal;
        decimal netCashChange;

        if (periodColumns.Count == 1)
        {
            operatingItems = periodSectionResults[0].Operating;
            investingItems = periodSectionResults[0].Investing;
            financingItems = periodSectionResults[0].Financing;
            operatingTotal = periodColumns[0].OperatingTotal;
            investingTotal = periodColumns[0].InvestingTotal;
            financingTotal = periodColumns[0].FinancingTotal;
            netCashChange = periodColumns[0].NetCashChange;
        }
        else if (input.AccumulatedValues)
        {
            // Per ERPNext commits 461d586d8b & 80ef213995:
            // In accumulated mode, periods sharing the same EffectiveStartDate already include earlier periods
            // of that fiscal year, so the total across the report sums the final period of each distinct EffectiveStartDate.
            var terminalIndices = periodColumns
                .Select((col, idx) => (col, idx))
                .GroupBy(x => x.col.EffectiveStartDate.Date)
                .Select(g => g.Last().idx)
                .ToList();

            operatingItems = AggregateLineItems(terminalIndices.Select(i => periodSectionResults[i].Operating));
            investingItems = AggregateLineItems(terminalIndices.Select(i => periodSectionResults[i].Investing));
            financingItems = AggregateLineItems(terminalIndices.Select(i => periodSectionResults[i].Financing));
            operatingTotal = terminalIndices.Sum(i => periodColumns[i].OperatingTotal);
            investingTotal = terminalIndices.Sum(i => periodColumns[i].InvestingTotal);
            financingTotal = terminalIndices.Sum(i => periodColumns[i].FinancingTotal);
            netCashChange = operatingTotal + investingTotal + financingTotal;
        }
        else
        {
            var fullBalances = await GetPeriodBalancesAsync(input.CompanyId, input.FromDate.Date, input.ToDate.Date, accounts);
            (operatingItems, operatingTotal, investingItems, investingTotal, financingItems, financingTotal) =
                ComputeSections(accounts, cashAccounts, fixedAssetAccounts, equityAccounts, fullBalances);
            netCashChange = operatingTotal + investingTotal + financingTotal;
        }

        var openingCash = periodColumns.Count > 0 ? periodColumns[0].OpeningCashBalance : initialOpeningCash;
        var closingCash = periodColumns.Count > 0 ? periodColumns[^1].ClosingCashBalance : (initialOpeningCash + netCashChange);

        return new CashFlowStatementDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            OperatingActivities = operatingItems,
            OperatingTotal = operatingTotal,
            InvestingActivities = investingItems,
            InvestingTotal = investingTotal,
            FinancingActivities = financingItems,
            FinancingTotal = financingTotal,
            NetCashChange = netCashChange,
            OpeningCashBalance = openingCash,
            ClosingCashBalance = closingCash,
            Periods = periodColumns
        };
    }

    /// <summary>
    /// Computes the effective movement start date for a period column.
    /// Per ERPNext commit 461d586d8b (get_start_date):
    /// - When accumulatedValues is false, returns the period's own fromDate.
    /// - When accumulatedValues is true, returns max(periodYearStartDate, fiscalYearStartDate) so a date range
    ///   that starts mid-fiscal-year counts movements only from the range start date.
    /// </summary>
    public static DateTime GetEffectiveStartDate(
        DateTime periodFromDate,
        DateTime periodYearStartDate,
        DateTime? fiscalYearStartDate,
        bool accumulatedValues)
    {
        if (!accumulatedValues)
        {
            return periodFromDate.Date;
        }

        var startDate = periodYearStartDate.Date;
        if (fiscalYearStartDate.HasValue && fiscalYearStartDate.Value.Date > startDate)
        {
            startDate = fiscalYearStartDate.Value.Date;
        }

        return startDate;
    }

    /// <summary>
    /// Computes Opening and Closing cash balances for each period column.
    /// Per ERPNext commit 80ef213995 (show_opening_and_closing_balance):
    /// - Tracks movement_start_date (EffectiveStartDate).
    /// - Only advances `opening = closing` when `EffectiveStartDate != movementStartDate`,
    ///   because accumulated periods within the same fiscal year already include earlier periods' movements.
    /// </summary>
    public static void CalculateOpeningAndClosingBalances(
        IList<CashFlowPeriodColumnDto> periods,
        decimal initialOpeningBalance)
    {
        var opening = initialOpeningBalance;
        var closing = initialOpeningBalance;
        DateTime? movementStartDate = null;

        foreach (var period in periods)
        {
            var startDate = period.EffectiveStartDate.Date;
            if (!movementStartDate.HasValue || startDate != movementStartDate.Value)
            {
                opening = closing;
                movementStartDate = startDate;
            }

            period.OpeningCashBalance = opening;
            closing = opening + period.NetCashChange;
            period.ClosingCashBalance = closing;
        }
    }

    private static List<(string Key, string Label, DateTime FromDate, DateTime ToDate, DateTime EffectiveStartDate)> BuildPeriods(
        DateTime fromDate,
        DateTime toDate,
        string? periodicity,
        List<FiscalYear> fiscalYears,
        bool accumulatedValues)
    {
        var result = new List<(string Key, string Label, DateTime FromDate, DateTime ToDate, DateTime EffectiveStartDate)>();
        var monthsPerPeriod = periodicity?.Trim().ToLowerInvariant() switch
        {
            "monthly" => 1,
            "quarterly" => 3,
            "half-yearly" => 6,
            "yearly" => 12,
            _ => 0
        };

        DateTime ResolveFiscalYearStart(DateTime date)
        {
            var fy = fiscalYears.FirstOrDefault(f => f.StartDate.Date <= date.Date && f.EndDate.Date >= date.Date);
            return fy?.StartDate.Date ?? new DateTime(date.Year, 1, 1);
        }

        if (monthsPerPeriod <= 0 || fromDate >= toDate)
        {
            var fyStart = ResolveFiscalYearStart(toDate);
            var yearStartDate = fyStart > fromDate ? fyStart : fromDate;
            var effectiveStart = GetEffectiveStartDate(fromDate, yearStartDate, fyStart, accumulatedValues);
            result.Add(("period_1", $"{fromDate:yyyy-MM-dd} - {toDate:yyyy-MM-dd}", fromDate, toDate, effectiveStart));
            return result;
        }

        var cursor = fromDate;
        var idx = 1;
        while (cursor <= toDate)
        {
            var nextStart = cursor.AddMonths(monthsPerPeriod);
            var pEnd = nextStart.AddDays(-1);
            if (pEnd > toDate)
            {
                pEnd = toDate;
            }

            var fyStart = ResolveFiscalYearStart(pEnd);
            var yearStartDate = fyStart > fromDate ? fyStart : fromDate;
            var effectiveStart = GetEffectiveStartDate(cursor, yearStartDate, fyStart, accumulatedValues);
            result.Add(($"period_{idx}", $"{cursor:MMM yyyy} - {pEnd:MMM yyyy}", cursor, pEnd, effectiveStart));

            cursor = pEnd.AddDays(1);
            idx++;
        }

        return result;
    }

    private static List<CashFlowLineItem> AggregateLineItems(IEnumerable<List<CashFlowLineItem>> groups)
    {
        var list = new List<CashFlowLineItem>();
        foreach (var group in groups)
        {
            foreach (var item in group)
            {
                var existing = list.FirstOrDefault(x => x.Label == item.Label);
                if (existing != null)
                {
                    existing.Amount += item.Amount;
                }
                else
                {
                    list.Add(new CashFlowLineItem(item.Label, item.Amount));
                }
            }
        }
        return list.Where(i => i.Label == "Net Profit" || i.Amount != 0).ToList();
    }

    private static (
        List<CashFlowLineItem> OperatingItems,
        decimal OperatingTotal,
        List<CashFlowLineItem> InvestingItems,
        decimal InvestingTotal,
        List<CashFlowLineItem> FinancingItems,
        decimal FinancingTotal) ComputeSections(
        List<Account> accounts,
        List<Account> cashAccounts,
        List<Account> fixedAssetAccounts,
        List<Account> equityAccounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> periodBalances)
    {
        // === Operating Activities (Indirect Method) ===
        var operatingItems = new List<CashFlowLineItem>();

        var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue);
        var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense);

        decimal totalRevenue = revenueAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);

        decimal totalExpenses = expenseAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);

        var netProfit = totalRevenue - totalExpenses;
        operatingItems.Add(new CashFlowLineItem("Net Profit", netProfit));

        var depreciationAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccumulatedDepreciation);
        var depreciation = depreciationAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (depreciation != 0)
            operatingItems.Add(new CashFlowLineItem("Depreciation", depreciation));

        var receivableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccountsReceivable);
        var receivableChange = receivableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (receivableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Accounts Receivable", -receivableChange));

        var inventoryAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.Stock);
        var inventoryChange = inventoryAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (inventoryChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Inventory", -inventoryChange));

        var payableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccountsPayable);
        var payableChange = payableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (payableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Accounts Payable", payableChange));

        var taxPayableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.TaxPayable);
        var taxPayableChange = taxPayableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (taxPayableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Tax Payables", taxPayableChange));

        var categorizedBsIds = cashAccounts
            .Concat(fixedAssetAccounts)
            .Concat(equityAccounts)
            .Concat(receivableAccounts)
            .Concat(inventoryAccounts)
            .Concat(payableAccounts)
            .Concat(taxPayableAccounts)
            .Concat(accounts.Where(a => a.AccountSubType == AccountSubType.LongTermLiability))
            .Select(a => a.Id)
            .ToHashSet();

        var otherBsAccounts = accounts.Where(a =>
            (a.AccountType == AccountType.Asset || a.AccountType == AccountType.Liability) &&
            !categorizedBsIds.Contains(a.Id));
        var otherBsChange = otherBsAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (otherBsChange != 0)
            operatingItems.Add(new CashFlowLineItem("Net Change in Other Balance Sheet Accounts", otherBsChange));

        var operatingTotal = operatingItems.Sum(i => i.Amount);

        // === Investing Activities ===
        var investingItems = new List<CashFlowLineItem>();

        var fixedAssetChange = fixedAssetAccounts
            .Where(a => a.AccountSubType == AccountSubType.FixedAsset)
            .Sum(a => periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
                      periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (fixedAssetChange != 0)
            investingItems.Add(new CashFlowLineItem("Purchase of Fixed Assets", -fixedAssetChange));

        var cwipChange = fixedAssetAccounts
            .Where(a => a.AccountSubType == AccountSubType.CapitalWorkInProgress)
            .Sum(a => periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
                      periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (cwipChange != 0)
            investingItems.Add(new CashFlowLineItem("Capital Work in Progress", -cwipChange));

        var investingTotal = investingItems.Sum(i => i.Amount);

        // === Financing Activities ===
        var financingItems = new List<CashFlowLineItem>();

        var equityChange = equityAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (equityChange != 0)
            financingItems.Add(new CashFlowLineItem("Change in Equity", equityChange));

        var loanAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.LongTermLiability);
        var loanChange = loanAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (loanChange != 0)
            financingItems.Add(new CashFlowLineItem("Net Borrowings / (Repayments)", loanChange));

        var financingTotal = financingItems.Sum(i => i.Amount);

        return (operatingItems, operatingTotal, investingItems, investingTotal, financingItems, financingTotal);
    }

    private async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> GetPeriodBalancesAsync(
        Guid companyId, DateTime fromDate, DateTime toDate, List<Account> accounts)
    {
        var journals = await _journalRepository.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate >= fromDate &&
            je.PostingDate <= toDate);

        if (!journals.Any())
            return new Dictionary<Guid, (decimal, decimal)>();

        var journalIds = journals.Select(j => j.Id).ToHashSet();
        var lines = await _lineRepository.GetListAsync(l => journalIds.Contains(l.JournalEntryId));

        return lines.GroupBy(l => l.AccountId).ToDictionary(
            g => g.Key,
            g => (g.Where(l => l.IsDebit).Sum(l => l.Amount), g.Where(l => !l.IsDebit).Sum(l => l.Amount)));
    }
}

