using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class ReportingAppService : ApplicationService, IReportingAppService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly AccountBalanceService _balanceService;
    private readonly IRepository<Customer, Guid> _customerRepository;
    private readonly IRepository<Supplier, Guid> _supplierRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public ReportingAppService(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntryLine, Guid> journalLineRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        AccountBalanceService balanceService,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _accountRepository = accountRepository;
        _journalLineRepository = journalLineRepository;
        _journalEntryRepository = journalEntryRepository;
        _balanceService = balanceService;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
    }

    public async Task<TrialBalanceReportDto> GetTrialBalanceAsync(TrialBalanceRequestDto input)
    {
        var accounts = await _accountRepository.GetListAsync(a => a.CompanyId == input.CompanyId && !a.IsGroup);

        // Use the optimized balance service (leverages closing balance cache + delta GL)
        var balanceMap = await _balanceService.GetTrialBalanceAsync(input.CompanyId, input.AsOfDate);

        var rows = new List<TrialBalanceRowDto>();
        foreach (var account in accounts.OrderBy(a => a.AccountCode))
        {
            if (!balanceMap.TryGetValue(account.Id, out var balance))
                continue; // Skip zero-balance accounts

            if (balance.Debit == 0 && balance.Credit == 0)
                continue;

            var netBalance = balance.Balance;

            rows.Add(new TrialBalanceRowDto
            {
                AccountId = account.Id,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                IsGroup = false,
                Level = 0,
                Debit = Math.Round(balance.Debit, 2),
                Credit = Math.Round(balance.Credit, 2),
                ClosingDebit = Math.Round(netBalance > 0 ? netBalance : 0, 2),
                ClosingCredit = Math.Round(netBalance < 0 ? Math.Abs(netBalance) : 0, 2),
            });
        }

        return new TrialBalanceReportDto
        {
            AsOfDate = input.AsOfDate,
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalDebit = Math.Round(rows.Sum(r => r.Debit), 2),
            TotalCredit = Math.Round(rows.Sum(r => r.Credit), 2),
        };
    }

    public async Task<ProfitLossReportDto> GetProfitLossAsync(ProfitLossRequestDto input)
    {
        var result = await BuildProfitLossForPeriodAsync(input.CompanyId, input.FromDate, input.ToDate);

        if (input.IncludeComparison)
        {
            // Calculate previous period with same duration immediately before
            var duration = input.ToDate - input.FromDate;
            var prevTo = input.FromDate.AddDays(-1);
            var prevFrom = prevTo - duration;

            var prevResult = await BuildProfitLossForPeriodAsync(input.CompanyId, prevFrom, prevTo);

            // Merge previous period data into rows
            var prevRevenueMap = prevResult.RevenueRows.ToDictionary(r => r.AccountId, r => r.Amount);
            var prevExpenseMap = prevResult.ExpenseRows.ToDictionary(r => r.AccountId, r => r.Amount);

            foreach (var row in result.RevenueRows)
            {
                row.PreviousPeriodAmount = prevRevenueMap.GetValueOrDefault(row.AccountId, 0m);
                row.GrowthPercentage = CalculateGrowth(row.Amount, row.PreviousPeriodAmount.Value);
            }
            foreach (var row in result.ExpenseRows)
            {
                row.PreviousPeriodAmount = prevExpenseMap.GetValueOrDefault(row.AccountId, 0m);
                row.GrowthPercentage = CalculateGrowth(row.Amount, row.PreviousPeriodAmount.Value);
            }

            result.PreviousTotalRevenue = prevResult.TotalRevenue;
            result.PreviousTotalExpense = prevResult.TotalExpense;
            result.PreviousNetProfitOrLoss = prevResult.NetProfitOrLoss;
            result.PreviousFromDate = prevFrom;
            result.PreviousToDate = prevTo;
        }

        return result;
    }

    private async Task<ProfitLossReportDto> BuildProfitLossForPeriodAsync(Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var accounts = await _accountRepository.GetListAsync(
            a => a.CompanyId == companyId && !a.IsGroup
                && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate >= fromDate
                && je.PostingDate <= toDate);

        var allLines = journalEntries.SelectMany(je => je.Lines).ToList();

        var linesByAccount = allLines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var revenueRows = new List<ProfitLossRowDto>();
        var expenseRows = new List<ProfitLossRowDto>();

        foreach (var account in accounts.OrderBy(a => a.AccountCode))
        {
            var lines = linesByAccount.GetValueOrDefault(account.Id) ?? new List<JournalEntryLine>();
            var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);

            var amount = account.AccountType == AccountType.Revenue
                ? credit - debit
                : debit - credit;

            if (amount == 0) continue;

            var row = new ProfitLossRowDto
            {
                AccountId = account.Id,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                Amount = amount,
                Level = 0,
                IsGroup = false,
            };

            if (account.AccountType == AccountType.Revenue)
                revenueRows.Add(row);
            else
                expenseRows.Add(row);
        }

        var totalRevenue = revenueRows.Sum(r => r.Amount);
        var totalExpense = expenseRows.Sum(r => r.Amount);

        return new ProfitLossReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            CompanyId = companyId,
            RevenueRows = revenueRows,
            ExpenseRows = expenseRows,
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetProfitOrLoss = totalRevenue - totalExpense,
        };
    }

    private static decimal? CalculateGrowth(decimal current, decimal previous)
    {
        if (previous == 0) return current > 0 ? 100m : current < 0 ? -100m : null;
        return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
    }

    public async Task<BalanceSheetReportDto> GetBalanceSheetAsync(BalanceSheetRequestDto input)
    {
        var accounts = await _accountRepository.GetListAsync(
            a => a.CompanyId == input.CompanyId && !a.IsGroup
                && (a.AccountType == AccountType.Asset
                    || a.AccountType == AccountType.Liability
                    || a.AccountType == AccountType.Equity));

        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate <= input.AsOfDate);

        // Lines are already loaded via AutoInclude — no separate query needed
        var allLines = journalEntries.SelectMany(je => je.Lines).ToList();

        var linesByAccount = allLines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var assetRows = new List<BalanceSheetRowDto>();
        var liabilityRows = new List<BalanceSheetRowDto>();
        var equityRows = new List<BalanceSheetRowDto>();

        foreach (var account in accounts.OrderBy(a => a.AccountCode))
        {
            var lines = linesByAccount.GetValueOrDefault(account.Id) ?? new List<JournalEntryLine>();
            var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);

            // Asset: debit - credit (normal debit balance)
            // Liability & Equity: credit - debit (normal credit balance)
            var amount = account.AccountType == AccountType.Asset
                ? debit - credit
                : credit - debit;

            if (amount == 0) continue;

            var row = new BalanceSheetRowDto
            {
                AccountId = account.Id,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                Amount = amount,
                Level = 0,
                IsGroup = false,
            };

            switch (account.AccountType)
            {
                case AccountType.Asset: assetRows.Add(row); break;
                case AccountType.Liability: liabilityRows.Add(row); break;
                case AccountType.Equity: equityRows.Add(row); break;
            }
        }

        return new BalanceSheetReportDto
        {
            AsOfDate = input.AsOfDate,
            CompanyId = input.CompanyId,
            AssetRows = assetRows,
            LiabilityRows = liabilityRows,
            EquityRows = equityRows,
            TotalAssets = assetRows.Sum(r => r.Amount),
            TotalLiabilities = liabilityRows.Sum(r => r.Amount),
            TotalEquity = equityRows.Sum(r => r.Amount),
        };
    }

    public async Task<MonthlyProfitLossReportDto> GetMonthlyProfitLossAsync(MonthlyProfitLossRequestDto input)
    {
        var yearStart = new DateTime(input.Year, input.StartMonth, 1);
        var yearEnd = yearStart.AddMonths(12).AddDays(-1);

        var accounts = await _accountRepository.GetListAsync(
            a => a.CompanyId == input.CompanyId && !a.IsGroup
                && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate >= yearStart
                && je.PostingDate <= yearEnd);

        // Flatten with posting date from parent JE
        var linesWithDate = journalEntries
            .SelectMany(je => je.Lines.Select(l => new { Line = l, je.PostingDate }))
            .ToList();

        // Group by account + month index
        var linesByAccountMonth = linesWithDate
            .GroupBy(x => new { x.Line.AccountId, MonthIdx = ((x.PostingDate.Year - yearStart.Year) * 12 + x.PostingDate.Month - yearStart.Month) })
            .Where(g => g.Key.MonthIdx >= 0 && g.Key.MonthIdx < 12)
            .ToDictionary(g => (g.Key.AccountId, g.Key.MonthIdx), g => g.ToList());

        var revenueRows = new List<MonthlyProfitLossRowDto>();
        var expenseRows = new List<MonthlyProfitLossRowDto>();

        foreach (var account in accounts.OrderBy(a => a.AccountCode))
        {
            var monthlyAmounts = new decimal[12];
            for (int m = 0; m < 12; m++)
            {
                if (!linesByAccountMonth.TryGetValue((account.Id, m), out var lines)) continue;

                var debit = lines.Where(x => x.Line.IsDebit).Sum(x => x.Line.Amount);
                var credit = lines.Where(x => !x.Line.IsDebit).Sum(x => x.Line.Amount);
                monthlyAmounts[m] = account.AccountType == AccountType.Revenue
                    ? credit - debit
                    : debit - credit;
            }

            var annualTotal = monthlyAmounts.Sum();
            if (annualTotal == 0 && monthlyAmounts.All(a => a == 0)) continue;

            var row = new MonthlyProfitLossRowDto
            {
                AccountId = account.Id,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                MonthlyAmounts = monthlyAmounts,
                AnnualTotal = annualTotal,
            };

            if (account.AccountType == AccountType.Revenue)
                revenueRows.Add(row);
            else
                expenseRows.Add(row);
        }

        var monthlyRevenue = new decimal[12];
        var monthlyExpense = new decimal[12];
        var monthlyNetProfit = new decimal[12];
        var monthLabels = new string[12];

        for (int m = 0; m < 12; m++)
        {
            monthlyRevenue[m] = revenueRows.Sum(r => r.MonthlyAmounts[m]);
            monthlyExpense[m] = expenseRows.Sum(r => r.MonthlyAmounts[m]);
            monthlyNetProfit[m] = monthlyRevenue[m] - monthlyExpense[m];
            var monthDate = yearStart.AddMonths(m);
            monthLabels[m] = monthDate.ToString("MMM yyyy");
        }

        return new MonthlyProfitLossReportDto
        {
            Year = input.Year,
            CompanyId = input.CompanyId,
            MonthLabels = monthLabels,
            RevenueRows = revenueRows,
            ExpenseRows = expenseRows,
            MonthlyRevenue = monthlyRevenue,
            MonthlyExpense = monthlyExpense,
            MonthlyNetProfit = monthlyNetProfit,
            AnnualRevenue = monthlyRevenue.Sum(),
            AnnualExpense = monthlyExpense.Sum(),
            AnnualNetProfit = monthlyNetProfit.Sum(),
        };
    }

    public async Task<PartyTrialBalanceReportDto> GetTrialBalanceForPartyAsync(PartyTrialBalanceRequestDto input)
    {
        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currency = company?.CurrencyCode ?? "MYR";

        var partyType = string.Equals(input.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase)
            ? "Supplier"
            : "Customer";

        Dictionary<Guid, string> partyNames;
        if (partyType == "Customer")
        {
            var custQuery = await _customerRepository.GetQueryableAsync();
            var q = custQuery.Where(c => c.CompanyId == input.CompanyId);
            if (input.PartyId.HasValue)
            {
                q = q.Where(c => c.Id == input.PartyId.Value);
            }
            partyNames = q.Select(c => new { c.Id, c.Name }).ToDictionary(c => c.Id, c => c.Name);
        }
        else
        {
            var suppQuery = await _supplierRepository.GetQueryableAsync();
            var q = suppQuery.Where(s => s.CompanyId == input.CompanyId);
            if (input.PartyId.HasValue)
            {
                q = q.Where(s => s.Id == input.PartyId.Value);
            }
            partyNames = q.Select(s => new { s.Id, s.Name }).ToDictionary(s => s.Id, s => s.Name);
        }

        // Query posted journal entries for the company up to ToDate (or opening entries)
        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId &&
            je.Status == DocumentStatus.Posted &&
            (je.PostingDate <= input.ToDate || (je.IsOpening && je.PostingDate <= input.ToDate)));

        // Opening: PostingDate < FromDate || (IsOpening && PostingDate <= ToDate)
        var openingLines = journalEntries
            .Where(je => je.PostingDate < input.FromDate || (je.IsOpening && je.PostingDate <= input.ToDate))
            .SelectMany(je => je.Lines)
            .Where(l => string.Equals(l.PartyType, partyType, StringComparison.OrdinalIgnoreCase) && l.PartyId.HasValue);

        if (input.PartyId.HasValue)
        {
            openingLines = openingLines.Where(l => l.PartyId == input.PartyId.Value);
        }
        if (input.AccountId.HasValue)
        {
            openingLines = openingLines.Where(l => l.AccountId == input.AccountId.Value);
        }

        var openingByParty = openingLines
            .GroupBy(l => l.PartyId!.Value)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var dr = Math.Round(g.Where(l => l.IsDebit).Sum(l => l.Amount), 2);
                    var cr = Math.Round(g.Where(l => !l.IsDebit).Sum(l => l.Amount), 2);
                    return ToggleDebitCredit(dr, cr);
                });

        // Balances within period: PostingDate >= FromDate && PostingDate <= ToDate && !IsOpening
        var periodLines = journalEntries
            .Where(je => je.PostingDate >= input.FromDate && je.PostingDate <= input.ToDate && !je.IsOpening)
            .SelectMany(je => je.Lines)
            .Where(l => string.Equals(l.PartyType, partyType, StringComparison.OrdinalIgnoreCase) && l.PartyId.HasValue);

        if (input.PartyId.HasValue)
        {
            periodLines = periodLines.Where(l => l.PartyId == input.PartyId.Value);
        }
        if (input.AccountId.HasValue)
        {
            periodLines = periodLines.Where(l => l.AccountId == input.AccountId.Value);
        }

        var periodByParty = periodLines
            .GroupBy(l => l.PartyId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (
                    Debit: Math.Round(g.Where(l => l.IsDebit).Sum(l => l.Amount), 2),
                    Credit: Math.Round(g.Where(l => !l.IsDebit).Sum(l => l.Amount), 2)
                ));

        // Union parties
        var allPartyIds = partyNames.Keys
            .Union(openingByParty.Keys)
            .Union(periodByParty.Keys)
            .Distinct()
            .ToList();

        if (input.PartyId.HasValue)
        {
            allPartyIds = allPartyIds.Where(p => p == input.PartyId.Value).ToList();
        }

        var rows = new List<PartyTrialBalanceRowDto>();

        foreach (var pId in allPartyIds.OrderBy(p => partyNames.GetValueOrDefault(p) ?? p.ToString()))
        {
            var partyName = partyNames.GetValueOrDefault(pId) ?? pId.ToString();
            var (opDrRaw, opCrRaw) = openingByParty.GetValueOrDefault(pId, (0m, 0m));
            var (pDrRaw, pCrRaw) = periodByParty.GetValueOrDefault(pId, (0m, 0m));

            // Per ERPNext PR #58607 / commit b1c7657dfa:
            // Round party balances to currency precision so sub-cent remainders do not bypass zero balance exclusion
            var opDr = Math.Round(opDrRaw, 2);
            var opCr = Math.Round(opCrRaw, 2);
            var pDr = Math.Round(pDrRaw, 2);
            var pCr = Math.Round(pCrRaw, 2);
            var (clDr, clCr) = ToggleDebitCredit(opDr + pDr, opCr + pCr);

            if (input.ExcludeZeroBalanceParties && clDr == 0 && clCr == 0)
            {
                continue;
            }

            var hasValue = opDr != 0 || opCr != 0 || pDr != 0 || pCr != 0 || clDr != 0 || clCr != 0;
            if (!input.ShowZeroValues && !hasValue)
            {
                continue;
            }

            rows.Add(new PartyTrialBalanceRowDto
            {
                PartyId = pId,
                PartyName = partyName,
                PartyType = partyType,
                OpeningDebit = opDr,
                OpeningCredit = opCr,
                Debit = pDr,
                Credit = pCr,
                ClosingDebit = clDr,
                ClosingCredit = clCr,
                Currency = currency,
            });
        }

        return new PartyTrialBalanceReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            PartyType = partyType,
            Currency = currency,
            Rows = rows,
            TotalOpeningDebit = Math.Round(rows.Sum(r => r.OpeningDebit), 2),
            TotalOpeningCredit = Math.Round(rows.Sum(r => r.OpeningCredit), 2),
            TotalDebit = Math.Round(rows.Sum(r => r.Debit), 2),
            TotalCredit = Math.Round(rows.Sum(r => r.Credit), 2),
            TotalClosingDebit = Math.Round(rows.Sum(r => r.ClosingDebit), 2),
            TotalClosingCredit = Math.Round(rows.Sum(r => r.ClosingCredit), 2),
        };
    }

    public static (decimal Debit, decimal Credit) ToggleDebitCredit(decimal debit, decimal credit)
    {
        if (debit > credit)
        {
            return (Math.Round(debit - credit, 2), 0m);
        }
        else
        {
            return (0m, Math.Round(credit - debit, 2));
        }
    }

    // --- Financial Ratios Report (ERPNext accounts/report/financial_ratios) ---

    public async Task<FinancialRatiosReportDto> GetFinancialRatiosAsync(FinancialRatiosRequestDto input)
    {
        var currentRatios = await CalculateFinancialRatiosForPeriodAsync(input.CompanyId, input.FromDate, input.ToDate);

        DateTime? prevFrom = null;
        DateTime? prevTo = null;
        Dictionary<string, decimal?>? prevRatios = null;

        if (input.IncludeComparison)
        {
            var duration = input.ToDate - input.FromDate;
            prevTo = input.FromDate.AddDays(-1);
            prevFrom = prevTo.Value - duration;

            prevRatios = await CalculateFinancialRatiosForPeriodAsync(input.CompanyId, prevFrom.Value, prevTo.Value);
        }

        var rows = BuildFinancialRatioRows(currentRatios, prevRatios);

        return new FinancialRatiosReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            PreviousFromDate = prevFrom,
            PreviousToDate = prevTo,
            Rows = rows,
        };
    }

    private async Task<Dictionary<string, decimal?>> CalculateFinancialRatiosForPeriodAsync(
        Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var accounts = await _accountRepository.GetListAsync(a => a.CompanyId == companyId && !a.IsGroup);
        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate <= toDate);

        var allLines = journalEntries.SelectMany(je => je.Lines).ToList();

        // Lines grouped by account as of toDate
        var linesUpToClosing = allLines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Lines within period [fromDate, toDate]
        var periodEntries = journalEntries.Where(je => je.PostingDate >= fromDate).ToList();
        var linesInPeriod = periodEntries.SelectMany(je => je.Lines)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Lines up to opening date (fromDate - 1 day)
        var openingDate = fromDate.AddDays(-1);
        var linesUpToOpening = journalEntries.Where(je => je.PostingDate <= openingDate)
            .SelectMany(je => je.Lines)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Helper to get closing balance as of toDate
        decimal GetClosingBalance(Account a)
        {
            var lines = linesUpToClosing.GetValueOrDefault(a.Id) ?? new List<JournalEntryLine>();
            var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            return a.AccountType == AccountType.Asset ? debit - credit : credit - debit;
        }

        // Helper to get opening balance as of fromDate - 1
        decimal GetOpeningBalance(Account a)
        {
            var lines = linesUpToOpening.GetValueOrDefault(a.Id) ?? new List<JournalEntryLine>();
            var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            return a.AccountType == AccountType.Asset ? debit - credit : credit - debit;
        }

        // Helper to get period flow (Revenue = credit - debit; Expense = debit - credit)
        decimal GetPeriodAmount(Account a)
        {
            var lines = linesInPeriod.GetValueOrDefault(a.Id) ?? new List<JournalEntryLine>();
            var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            return a.AccountType == AccountType.Revenue ? credit - debit : debit - credit;
        }

        // Account classification
        var currentAssetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            (a.AccountSubType == AccountSubType.CurrentAsset ||
             a.AccountSubType == AccountSubType.BankAccount ||
             a.AccountSubType == AccountSubType.CashAccount ||
             a.AccountSubType == AccountSubType.AccountsReceivable ||
             a.AccountSubType == AccountSubType.Stock)).ToList();

        var quickAssetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            (a.AccountSubType == AccountSubType.BankAccount ||
             a.AccountSubType == AccountSubType.CashAccount ||
             a.AccountSubType == AccountSubType.AccountsReceivable)).ToList();

        var fixedAssetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            (a.AccountSubType == AccountSubType.FixedAsset ||
             a.AccountSubType == AccountSubType.AccumulatedDepreciation ||
             a.AccountSubType == AccountSubType.CapitalWorkInProgress)).ToList();

        var allAssetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset).ToList();

        var currentLiabilityAccounts = accounts.Where(a => a.AccountType == AccountType.Liability &&
            (a.AccountSubType == AccountSubType.CurrentLiability ||
             a.AccountSubType == AccountSubType.AccountsPayable ||
             a.AccountSubType == AccountSubType.TaxPayable)).ToList();

        var allLiabilityAccounts = accounts.Where(a => a.AccountType == AccountType.Liability).ToList();

        var receivableAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            a.AccountSubType == AccountSubType.AccountsReceivable).ToList();

        var payableAccounts = accounts.Where(a => a.AccountType == AccountType.Liability &&
            a.AccountSubType == AccountSubType.AccountsPayable).ToList();

        var stockAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            a.AccountSubType == AccountSubType.Stock).ToList();

        var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
        var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense).ToList();
        var cogsAccounts = expenseAccounts.Where(a => a.AccountSubType == AccountSubType.CostOfGoodsSold ||
            a.AccountName.Contains("Cost of Goods", StringComparison.OrdinalIgnoreCase) ||
            a.AccountName.Contains("COGS", StringComparison.OrdinalIgnoreCase)).ToList();

        // 1. Balance Sheet totals
        var currentAssets = currentAssetAccounts.Sum(GetClosingBalance);
        var quickAssets = quickAssetAccounts.Sum(GetClosingBalance);
        var fixedAssets = fixedAssetAccounts.Sum(GetClosingBalance);
        var totalAssets = allAssetAccounts.Sum(GetClosingBalance);

        var currentLiabilities = currentLiabilityAccounts.Sum(GetClosingBalance);
        var totalLiabilities = allLiabilityAccounts.Sum(GetClosingBalance);
        var shareholderFund = totalAssets - totalLiabilities;

        // 2. P&L totals for the period
        var netSales = revenueAccounts.Sum(GetPeriodAmount);
        var totalExpense = expenseAccounts.Sum(GetPeriodAmount);
        var cogs = cogsAccounts.Sum(GetPeriodAmount);
        var netProfit = netSales - totalExpense;

        // 3. Average balances
        var closingDebtors = receivableAccounts.Sum(GetClosingBalance);
        var openingDebtors = receivableAccounts.Sum(GetOpeningBalance);
        var avgDebtors = (closingDebtors + openingDebtors) / 2m;

        var closingCreditors = payableAccounts.Sum(GetClosingBalance);
        var openingCreditors = payableAccounts.Sum(GetOpeningBalance);
        var avgCreditors = (closingCreditors + openingCreditors) / 2m;

        var closingStock = stockAccounts.Sum(GetClosingBalance);
        var openingStock = stockAccounts.Sum(GetOpeningBalance);
        var avgStock = (closingStock + openingStock) / 2m;

        // 4. Calculate 11 canonical ratios
        var ratios = new Dictionary<string, decimal?>
        {
            // Liquidity
            ["CurrentRatio"] = SafeDivide(currentAssets, currentLiabilities),
            ["QuickRatio"] = SafeDivide(quickAssets, currentLiabilities),

            // Solvency & Profitability
            ["DebtEquityRatio"] = SafeDivide(totalLiabilities, shareholderFund),
            ["GrossProfitMargin"] = SafeDivide((netSales - cogs) * 100m, netSales),
            ["NetProfitMargin"] = SafeDivide(netProfit * 100m, netSales),
            ["ReturnOnAssets"] = SafeDivide(netProfit * 100m, totalAssets),
            ["ReturnOnEquity"] = SafeDivide(netProfit * 100m, shareholderFund),

            // Turnover
            ["FixedAssetTurnover"] = SafeDivide(netSales, fixedAssets > 0 ? fixedAssets : totalAssets),
            ["DebtorTurnover"] = SafeDivide(netSales, avgDebtors > 0 ? avgDebtors : closingDebtors),
            ["CreditorTurnover"] = SafeDivide(totalExpense > 0 ? totalExpense : cogs, avgCreditors > 0 ? avgCreditors : closingCreditors),
            ["InventoryTurnover"] = SafeDivide(cogs > 0 ? cogs : totalExpense, avgStock > 0 ? avgStock : closingStock),
        };

        return ratios;
    }

    private static decimal? SafeDivide(decimal numerator, decimal denominator, int decimals = 2)
    {
        if (denominator == 0) return null;
        return Math.Round(numerator / denominator, decimals);
    }

    private static List<FinancialRatioRowDto> BuildFinancialRatioRows(
        Dictionary<string, decimal?> current,
        Dictionary<string, decimal?>? previous)
    {
        var definitions = new[]
        {
            // Category, Key, Name, Formula, Description, Unit
            ("Liquidity Ratios", "CurrentRatio", "Current Ratio",
             "Current Assets / Current Liabilities",
             "Measures ability to pay short-term debt with short-term assets (ideal > 1.5).", "ratio"),

            ("Liquidity Ratios", "QuickRatio", "Quick Ratio (Acid-Test)",
             "(Bank + Cash + Receivables) / Current Liabilities",
             "Measures instant liquidity excluding inventory and prepayments (ideal > 1.0).", "ratio"),

            ("Solvency & Profitability", "DebtEquityRatio", "Debt to Equity Ratio",
             "Total Liabilities / Total Equity",
             "Proportion of equity and debt used to finance assets.", "ratio"),

            ("Solvency & Profitability", "GrossProfitMargin", "Gross Profit Margin",
             "((Net Sales - COGS) / Net Sales) * 100",
             "Percentage of revenue left after paying cost of goods sold.", "%"),

            ("Solvency & Profitability", "NetProfitMargin", "Net Profit Margin",
             "(Net Profit / Net Sales) * 100",
             "Percentage of revenue remaining as net profit after all expenses.", "%"),

            ("Solvency & Profitability", "ReturnOnAssets", "Return on Assets (ROA)",
             "(Net Profit / Total Assets) * 100",
             "Efficiency at using assets to generate net earnings.", "%"),

            ("Solvency & Profitability", "ReturnOnEquity", "Return on Equity (ROE)",
             "(Net Profit / Shareholder Equity) * 100",
             "Profitability generated on shareholder invested capital.", "%"),

            ("Turnover Ratios", "FixedAssetTurnover", "Fixed Asset Turnover",
             "Net Sales / Fixed Assets",
             "How efficiently sales are generated from fixed asset investments.", "times"),

            ("Turnover Ratios", "DebtorTurnover", "Debtor (Receivables) Turnover",
             "Net Sales / Average Receivables",
             "How quickly customer credit is collected into cash.", "times"),

            ("Turnover Ratios", "CreditorTurnover", "Creditor (Payables) Turnover",
             "Total Expenses / Average Payables",
             "Frequency of paying supplier invoices during the period.", "times"),

            ("Turnover Ratios", "InventoryTurnover", "Inventory Turnover",
             "COGS / Average Inventory",
             "Number of times inventory is sold and replaced over the period.", "times"),
        };

        var result = new List<FinancialRatioRowDto>();

        foreach (var (category, key, name, formula, desc, unit) in definitions)
        {
            current.TryGetValue(key, out var val);
            decimal? prevVal = null;
            if (previous != null)
                previous.TryGetValue(key, out prevVal);

            decimal? change = null;
            if (val.HasValue && prevVal.HasValue && prevVal.Value != 0)
            {
                change = Math.Round((val.Value - prevVal.Value) / Math.Abs(prevVal.Value) * 100m, 1);
            }

            result.Add(new FinancialRatioRowDto
            {
                Category = category,
                RatioName = name,
                Value = val,
                PreviousValue = prevVal,
                ChangePercentage = change,
                Formula = formula,
                Description = desc,
                Unit = unit,
            });
        }

        return result;
    }
}

