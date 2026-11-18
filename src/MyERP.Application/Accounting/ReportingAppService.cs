using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Projects.Entities;
using MyERP.Purchasing.Entities;
using MyERP.Sales.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
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
    private readonly IRepository<FiscalYear, Guid>? _fiscalYearRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid>? _pleRepository;
    private readonly IRepository<FinanceBook, Guid>? _financeBookRepository;
    private readonly IRepository<CostCenter, Guid>? _costCenterRepository;
    private readonly IRepository<Project, Guid>? _projectRepository;

    public ReportingAppService(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntryLine, Guid> journalLineRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        AccountBalanceService balanceService,
        IRepository<Customer, Guid> customerRepository,
        IRepository<Supplier, Guid> supplierRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FiscalYear, Guid>? fiscalYearRepository = null,
        IRepository<PaymentLedgerEntry, Guid>? pleRepository = null,
        IRepository<FinanceBook, Guid>? financeBookRepository = null,
        IRepository<CostCenter, Guid>? costCenterRepository = null,
        IRepository<Project, Guid>? projectRepository = null)
    {
        _accountRepository = accountRepository;
        _journalLineRepository = journalLineRepository;
        _journalEntryRepository = journalEntryRepository;
        _balanceService = balanceService;
        _customerRepository = customerRepository;
        _supplierRepository = supplierRepository;
        _companyRepository = companyRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _pleRepository = pleRepository;
        _financeBookRepository = financeBookRepository;
        _costCenterRepository = costCenterRepository;
        _projectRepository = projectRepository;
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
        var result = await BuildProfitLossForPeriodAsync(
            input.CompanyId, input.FromDate, input.ToDate, input.FinanceBook, input.IncludeDefaultBookEntries);

        if (input.IncludeComparison)
        {
            // Calculate previous period with same duration immediately before
            var duration = input.ToDate - input.FromDate;
            var prevTo = input.FromDate.AddDays(-1);
            var prevFrom = prevTo - duration;

            var prevResult = await BuildProfitLossForPeriodAsync(
                input.CompanyId, prevFrom, prevTo, input.FinanceBook, input.IncludeDefaultBookEntries);

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

    private async Task<ProfitLossReportDto> BuildProfitLossForPeriodAsync(
        Guid companyId,
        DateTime fromDate,
        DateTime toDate,
        string? financeBook = null,
        bool includeDefaultBookEntries = false)
    {
        var allowedFinanceBooks = await ResolveAllowedFinanceBooksAsync(
            companyId, financeBook, includeDefaultBookEntries);

        var accounts = await _accountRepository.GetListAsync(
            a => a.CompanyId == companyId && !a.IsGroup
                && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var toDateExclusive = toDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate >= fromDate
                && je.PostingDate < toDateExclusive);

        var allLines = journalEntries.SelectMany(je => je.Lines)
            .Where(l =>
            {
                var book = l.FinanceBook ?? string.Empty;
                return allowedFinanceBooks.Contains(book);
            })
            .ToList();

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

        var totalRevenue = Math.Round(revenueRows.Sum(r => r.Amount), 2);
        var totalExpense = Math.Round(expenseRows.Sum(r => r.Amount), 2);
        var netProfit = totalRevenue - totalExpense;

        // Base the margin view on the income total row per ERPNext PR #60012 / commits f180489d02, 63040a87d9
        decimal? totalExpenseMargin = null;
        decimal? netProfitMargin = null;
        if (totalRevenue != 0)
        {
            foreach (var r in revenueRows)
            {
                r.MarginPercentage = Math.Round(r.Amount / totalRevenue * 100m, 2);
            }
            foreach (var r in expenseRows)
            {
                r.MarginPercentage = Math.Round(r.Amount / totalRevenue * 100m, 2);
            }
            totalExpenseMargin = Math.Round(totalExpense / totalRevenue * 100m, 2);
            netProfitMargin = Math.Round(netProfit / totalRevenue * 100m, 2);
        }

        return new ProfitLossReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            CompanyId = companyId,
            RevenueRows = revenueRows,
            ExpenseRows = expenseRows,
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetProfitOrLoss = netProfit,
            TotalExpenseMarginPercentage = totalExpenseMargin,
            NetProfitMarginPercentage = netProfitMargin,
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

        var asOfExclusive = input.AsOfDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate < asOfExclusive);

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

        var totalAssets = Math.Round(assetRows.Sum(r => r.Amount), 2);
        var totalLiabilities = Math.Round(liabilityRows.Sum(r => r.Amount), 2);
        var initialEquity = Math.Round(equityRows.Sum(r => r.Amount), 2);

        // Fundamental accounting equation: Assets = Liabilities + Equity + Net Profit
        // Net Profit = Assets - Liabilities - Equity
        var totalNetProfit = Math.Round(totalAssets - totalLiabilities - initialEquity, 2);

        // Split into unclosed prior fiscal years vs current fiscal year provisional profit/loss
        // Per ERPNext PR #60004 / commits 89012ba339, 509e2f1581:
        // "show profit or loss of unclosed earlier years"
        decimal unclosedProfit = 0m;
        decimal currentYearProvisionalProfit = totalNetProfit;

        var fyRepo = _fiscalYearRepository ?? LazyServiceProvider.LazyGetService<IRepository<FiscalYear, Guid>>();
        if (fyRepo != null)
        {
            var fiscalYears = await fyRepo.GetListAsync(f => f.CompanyId == input.CompanyId);
            var currentFy = fiscalYears.FirstOrDefault(f => f.StartDate <= input.AsOfDate && f.EndDate >= input.AsOfDate);

            if (currentFy != null)
            {
                var priorEntries = journalEntries.Where(je => je.PostingDate < currentFy.StartDate).ToList();
                if (priorEntries.Count > 0)
                {
                    var priorLines = priorEntries.SelectMany(je => je.Lines).ToList();
                    var priorLinesByAccount = priorLines.GroupBy(l => l.AccountId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    decimal priorAssets = 0m;
                    decimal priorLiabilities = 0m;
                    decimal priorEquity = 0m;

                    foreach (var account in accounts)
                    {
                        var lines = priorLinesByAccount.GetValueOrDefault(account.Id) ?? new List<JournalEntryLine>();
                        var debit = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
                        var credit = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
                        var amt = account.AccountType == AccountType.Asset ? debit - credit : credit - debit;

                        switch (account.AccountType)
                        {
                            case AccountType.Asset: priorAssets += amt; break;
                            case AccountType.Liability: priorLiabilities += amt; break;
                            case AccountType.Equity: priorEquity += amt; break;
                        }
                    }

                    unclosedProfit = Math.Round(priorAssets - priorLiabilities - priorEquity, 2);
                    currentYearProvisionalProfit = Math.Round(totalNetProfit - unclosedProfit, 2);
                }
            }
        }

        // Add unclosed earlier years profit/loss and provisional profit/loss to equity rows with labels per PR #60004
        if (unclosedProfit != 0)
        {
            equityRows.Add(new BalanceSheetRowDto
            {
                AccountId = Guid.Empty,
                AccountCode = "",
                AccountName = "Unclosed Fiscal Years Profit / Loss (Credit)",
                AccountType = AccountType.Equity.ToString(),
                Amount = unclosedProfit,
                Level = 0,
                IsGroup = false
            });
        }

        if (currentYearProvisionalProfit != 0 || unclosedProfit != 0)
        {
            equityRows.Add(new BalanceSheetRowDto
            {
                AccountId = Guid.Empty,
                AccountCode = "",
                AccountName = "Provisional Profit / Loss (Credit)",
                AccountType = AccountType.Equity.ToString(),
                Amount = currentYearProvisionalProfit,
                Level = 0,
                IsGroup = false
            });
        }

        return new BalanceSheetReportDto
        {
            AsOfDate = input.AsOfDate,
            CompanyId = input.CompanyId,
            AssetRows = assetRows,
            LiabilityRows = liabilityRows,
            EquityRows = equityRows,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = Math.Round(equityRows.Sum(r => r.Amount), 2),
            ProvisionalProfitLoss = currentYearProvisionalProfit,
            UnclosedFiscalYearsProfitLoss = unclosedProfit,
        };
    }

    public async Task<MonthlyProfitLossReportDto> GetMonthlyProfitLossAsync(MonthlyProfitLossRequestDto input)
    {
        var yearStart = new DateTime(input.Year, input.StartMonth, 1);
        var yearEnd = yearStart.AddMonths(12).AddDays(-1);

        var accounts = await _accountRepository.GetListAsync(
            a => a.CompanyId == input.CompanyId && !a.IsGroup
                && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var yearEndExclusive = yearEnd.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == input.CompanyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate >= yearStart
                && je.PostingDate < yearEndExclusive);

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

        // Query posted journal entries for the company up to end of ToDate
        var toDateExclusive = input.ToDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate < toDateExclusive);

        // Opening: PostingDate < FromDate || (IsOpening && PostingDate < toDateExclusive)
        var openingLines = journalEntries
            .Where(je => je.PostingDate < input.FromDate || (je.IsOpening && je.PostingDate < toDateExclusive))
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

        // Balances within period: PostingDate >= FromDate && PostingDate < toDateExclusive && !IsOpening
        var periodLines = journalEntries
            .Where(je => je.PostingDate >= input.FromDate && je.PostingDate < toDateExclusive && !je.IsOpening)
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
        var allCompanyAccounts = await _accountRepository.GetListAsync(a => a.CompanyId == companyId);
        var accounts = allCompanyAccounts.Where(a => !a.IsGroup).ToList();
        var accountById = allCompanyAccounts.ToDictionary(a => a.Id);

        var categoryNamesById = new Dictionary<Guid, string>();
        if (allCompanyAccounts.Any(a => a.AccountCategoryId.HasValue))
        {
            var catRepo = LazyServiceProvider?.LazyGetService<IRepository<AccountCategory, Guid>>();
            if (catRepo != null)
            {
                var categories = await catRepo.GetListAsync();
                categoryNamesById = categories.ToDictionary(c => c.Id, c => c.Name);
            }
        }

        // Inherit AccountCategory and AccountSubType from nearest parent group (ERPNext PR #61490 / commit 0ee7a7b67f)
        string? GetEffectiveCategoryName(Account a)
        {
            var cur = a;
            var visited = new HashSet<Guid>();
            while (cur != null && visited.Add(cur.Id))
            {
                if (cur.AccountCategoryId.HasValue && categoryNamesById.TryGetValue(cur.AccountCategoryId.Value, out var catName) && !string.IsNullOrWhiteSpace(catName))
                    return catName;
                if (!cur.ParentAccountId.HasValue || !accountById.TryGetValue(cur.ParentAccountId.Value, out cur))
                    break;
            }
            return null;
        }

        AccountSubType? GetEffectiveSubType(Account a)
        {
            var cur = a;
            var visited = new HashSet<Guid>();
            while (cur != null && visited.Add(cur.Id))
            {
                if (cur.AccountSubType.HasValue)
                    return cur.AccountSubType.Value;
                if (!cur.ParentAccountId.HasValue || !accountById.TryGetValue(cur.ParentAccountId.Value, out cur))
                    break;
            }
            return null;
        }

        var toDateExclusive = toDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(
            je => je.CompanyId == companyId
                && je.Status == DocumentStatus.Posted
                && je.PostingDate < toDateExclusive);

        var allLines = journalEntries.SelectMany(je => je.Lines).ToList();

        // Lines grouped by account as of toDate
        var linesUpToClosing = allLines.GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Lines within period [fromDate, toDate]
        var fromDateStart = fromDate.Date;
        var periodEntries = journalEntries.Where(je => je.PostingDate >= fromDateStart).ToList();
        var linesInPeriod = periodEntries.SelectMany(je => je.Lines)
            .GroupBy(l => l.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Opening lines strictly before fromDateStart so intra-day entries on (fromDate - 1) aren't skipped
        var linesUpToOpening = journalEntries.Where(je => je.PostingDate < fromDateStart)
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

        // Account classification — per ERPNext PR #61490 (commit 0ee7a7b67f), AccountCategory wins over AccountSubType when present
        var currentAssetCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cash and Cash Equivalents", "Trade Receivables", "Other Receivables",
            "Stock Assets", "Short-term Investments", "Other Current Assets"
        };
        var fixedAssetCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Tangible Assets", "Intangible Assets"
        };
        var currentLiabilityCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Trade Payables", "Other Payables", "Short-term Borrowings",
            "Current Tax Liabilities", "Short-term Provisions", "Other Current Liabilities"
        };
        var directExpenseCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cost of Goods Sold", "Other Direct Costs"
        };

        var currentAssetAccounts = accounts.Where(a =>
        {
            if (a.AccountType != AccountType.Asset) return false;
            var cat = GetEffectiveCategoryName(a);
            if (cat != null) return currentAssetCategories.Contains(cat);
            var sub = GetEffectiveSubType(a);
            return sub is AccountSubType.CurrentAsset or AccountSubType.BankAccount or AccountSubType.CashAccount
                or AccountSubType.AccountsReceivable or AccountSubType.Stock;
        }).ToList();

        var quickAssetAccounts = accounts.Where(a =>
        {
            if (a.AccountType != AccountType.Asset) return false;
            var cat = GetEffectiveCategoryName(a);
            if (cat != null)
            {
                return string.Equals(cat, "Cash and Cash Equivalents", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cat, "Trade Receivables", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cat, "Other Receivables", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(cat, "Short-term Investments", StringComparison.OrdinalIgnoreCase);
            }
            var sub = GetEffectiveSubType(a);
            return sub is AccountSubType.BankAccount or AccountSubType.CashAccount or AccountSubType.AccountsReceivable;
        }).ToList();

        var fixedAssetAccounts = accounts.Where(a =>
        {
            if (a.AccountType != AccountType.Asset) return false;
            var cat = GetEffectiveCategoryName(a);
            if (cat != null) return fixedAssetCategories.Contains(cat);
            var sub = GetEffectiveSubType(a);
            return sub is AccountSubType.FixedAsset or AccountSubType.AccumulatedDepreciation or AccountSubType.CapitalWorkInProgress;
        }).ToList();

        var allAssetAccounts = accounts.Where(a => a.AccountType == AccountType.Asset).ToList();

        var currentLiabilityAccounts = accounts.Where(a =>
        {
            if (a.AccountType != AccountType.Liability) return false;
            var cat = GetEffectiveCategoryName(a);
            if (cat != null) return currentLiabilityCategories.Contains(cat);
            var sub = GetEffectiveSubType(a);
            return sub is AccountSubType.CurrentLiability or AccountSubType.AccountsPayable or AccountSubType.TaxPayable;
        }).ToList();

        var allLiabilityAccounts = accounts.Where(a => a.AccountType == AccountType.Liability).ToList();

        var receivableAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            GetEffectiveSubType(a) == AccountSubType.AccountsReceivable).ToList();

        var payableAccounts = accounts.Where(a => a.AccountType == AccountType.Liability &&
            GetEffectiveSubType(a) == AccountSubType.AccountsPayable).ToList();

        var stockAccounts = accounts.Where(a => a.AccountType == AccountType.Asset &&
            GetEffectiveSubType(a) == AccountSubType.Stock).ToList();

        var revenueAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
        var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense).ToList();
        var cogsAccounts = expenseAccounts.Where(a =>
        {
            var cat = GetEffectiveCategoryName(a);
            if (cat != null) return directExpenseCategories.Contains(cat);
            var sub = GetEffectiveSubType(a);
            return sub == AccountSubType.CostOfGoodsSold ||
                a.AccountName.Contains("Cost of Goods", StringComparison.OrdinalIgnoreCase) ||
                a.AccountName.Contains("COGS", StringComparison.OrdinalIgnoreCase);
        }).ToList();

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

    public async Task<GeneralPaymentLedgerComparisonReportDto> GetGeneralAndPaymentLedgerComparisonAsync(
        GeneralPaymentLedgerComparisonRequestDto input)
    {
        var accounts = await _accountRepository.GetListAsync(a =>
            a.CompanyId == input.CompanyId &&
            !a.IsGroup &&
            (a.AccountSubType == AccountSubType.AccountsReceivable || a.AccountSubType == AccountSubType.AccountsPayable));

        if (input.AccountId.HasValue)
        {
            accounts = accounts.Where(a => a.Id == input.AccountId.Value).ToList();
        }

        if (accounts.Count == 0)
        {
            return new GeneralPaymentLedgerComparisonReportDto
            {
                CompanyId = input.CompanyId,
                PeriodStartDate = input.PeriodStartDate,
                PeriodEndDate = input.PeriodEndDate
            };
        }

        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var accountMap = accounts.ToDictionary(a => a.Id);

        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var journalEntries = jeQuery
            .Where(je => je.CompanyId == input.CompanyId && je.Status == DocumentStatus.Posted)
            .ToList();

        if (input.PeriodStartDate.HasValue)
        {
            var periodStart = DateRangeHelper.StartOfDay(input.PeriodStartDate.Value);
            journalEntries = journalEntries.Where(je => je.PostingDate >= periodStart).ToList();
        }
        if (input.PeriodEndDate.HasValue)
        {
            var periodEndExclusive = DateRangeHelper.ExclusiveNextDay(input.PeriodEndDate.Value);
            journalEntries = journalEntries.Where(je => je.PostingDate < periodEndExclusive).ToList();
        }

        var glLines = journalEntries
            .SelectMany(je => je.Lines.Select(line => (JournalEntry: je, Line: line)))
            .Where(x => accountIds.Contains(x.Line.AccountId) && x.Line.PartyId.HasValue && !string.IsNullOrEmpty(x.Line.PartyType))
            .ToList();

        if (!string.IsNullOrWhiteSpace(input.PartyType))
            glLines = glLines.Where(x => string.Equals(x.Line.PartyType, input.PartyType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (input.PartyId.HasValue)
            glLines = glLines.Where(x => x.Line.PartyId == input.PartyId.Value).ToList();

        var glGroups = new Dictionary<ComparisonKey, decimal>();
        var voucherNoLookup = new Dictionary<(string VoucherType, Guid VoucherId), string>();

        foreach (var (je, line) in glLines)
        {
            var vType = string.IsNullOrEmpty(je.ReferenceType) ? "JournalEntry" : je.ReferenceType;
            var vId = string.IsNullOrEmpty(je.ReferenceType) ? je.Id : (je.ReferenceId ?? je.Id);
            var vNo = je.ReferenceNumber ?? je.EntryNumber ?? je.Id.ToString();

            voucherNoLookup[(vType, vId)] = vNo;

            var againstVType = line.AgainstVoucherType ?? vType;
            var againstVId = line.AgainstVoucherId ?? vId;

            if (againstVType == vType && againstVId == vId)
            {
                voucherNoLookup[(againstVType, againstVId)] = vNo;
            }

            if (!string.IsNullOrWhiteSpace(input.VoucherType) && !string.Equals(vType, input.VoucherType, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(input.VoucherNo) && !string.Equals(vNo, input.VoucherNo, StringComparison.OrdinalIgnoreCase))
                continue;

            var netBalance = line.IsDebit ? line.Amount : -line.Amount;

            var key = new ComparisonKey(
                je.CompanyId,
                line.AccountId,
                vType,
                vId,
                line.PartyType!,
                line.PartyId!.Value,
                againstVType,
                againstVId);

            glGroups[key] = glGroups.GetValueOrDefault(key, 0m) + netBalance;
        }

        var pleRepo = _pleRepository ?? LazyServiceProvider.LazyGetService<IRepository<PaymentLedgerEntry, Guid>>();
        var pleEntries = pleRepo != null
            ? await pleRepo.GetListAsync(p => p.CompanyId == input.CompanyId && !p.Delinked && accountIds.Contains(p.AccountId))
            : new List<PaymentLedgerEntry>();

        if (input.PeriodStartDate.HasValue)
        {
            var periodStart = DateRangeHelper.StartOfDay(input.PeriodStartDate.Value);
            pleEntries = pleEntries.Where(p => p.PostingDate >= periodStart).ToList();
        }
        if (input.PeriodEndDate.HasValue)
        {
            var periodEndExclusive = DateRangeHelper.ExclusiveNextDay(input.PeriodEndDate.Value);
            pleEntries = pleEntries.Where(p => p.PostingDate < periodEndExclusive).ToList();
        }
        if (!string.IsNullOrWhiteSpace(input.PartyType))
            pleEntries = pleEntries.Where(p => string.Equals(p.PartyType, input.PartyType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (input.PartyId.HasValue)
            pleEntries = pleEntries.Where(p => p.PartyId == input.PartyId.Value).ToList();
        if (!string.IsNullOrWhiteSpace(input.VoucherType))
            pleEntries = pleEntries.Where(p => string.Equals(p.VoucherType, input.VoucherType, StringComparison.OrdinalIgnoreCase)).ToList();

        var pleGroups = new Dictionary<ComparisonKey, decimal>();
        foreach (var ple in pleEntries)
        {
            var key = new ComparisonKey(
                ple.CompanyId,
                ple.AccountId,
                ple.VoucherType,
                ple.VoucherId,
                ple.PartyType,
                ple.PartyId,
                ple.AgainstVoucherType,
                ple.AgainstVoucherId);

            pleGroups[key] = pleGroups.GetValueOrDefault(key, 0m) + ple.Amount;
        }

        // Fold reconciled allocations per ERPNext PR #60013 / commit 2727084167:
        // Payment Reconciliation re-points PLE rows to invoices while GL entry remains unallocated against itself.
        // Count PLE rows whose against voucher has no GL counterpart under the voucher's unallocated GL row.
        var glKeys = glGroups.Keys.ToHashSet();
        var foldedPle = new Dictionary<ComparisonKey, decimal>();

        foreach (var (pleKey, pleAmount) in pleGroups)
        {
            var effectiveKey = pleKey;
            var unallocatedKey = pleKey with { AgainstVoucherType = pleKey.VoucherType, AgainstVoucherId = pleKey.VoucherId };

            if (!glKeys.Contains(pleKey) && glKeys.Contains(unallocatedKey))
            {
                effectiveKey = unallocatedKey;
            }

            foldedPle[effectiveKey] = foldedPle.GetValueOrDefault(effectiveKey, 0m) + pleAmount;
        }

        var allKeys = glGroups.Keys.Union(foldedPle.Keys).ToList();

        var customerIds = allKeys.Where(k => k.PartyType == "Customer").Select(k => k.PartyId).Distinct().ToList();
        var supplierIds = allKeys.Where(k => k.PartyType == "Supplier").Select(k => k.PartyId).Distinct().ToList();

        var customerMap = customerIds.Count > 0
            ? (await _customerRepository.GetListAsync(c => customerIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Name)
            : new Dictionary<Guid, string>();
        var supplierMap = supplierIds.Count > 0
            ? (await _supplierRepository.GetListAsync(s => supplierIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name)
            : new Dictionary<Guid, string>();

        var rows = new List<GeneralPaymentLedgerComparisonRowDto>();

        foreach (var key in allKeys)
        {
            var glBalance = Math.Round(glGroups.GetValueOrDefault(key, 0m), 2);
            var plBalance = Math.Round(foldedPle.GetValueOrDefault(key, 0m), 2);

            if (Math.Abs(glBalance - plBalance) <= 0.005m)
                continue;

            var acc = accountMap.GetValueOrDefault(key.AccountId);
            var vNo = voucherNoLookup.GetValueOrDefault((key.VoucherType, key.VoucherId)) ?? key.VoucherId.ToString();
            var againstVNo = voucherNoLookup.GetValueOrDefault((key.AgainstVoucherType, key.AgainstVoucherId)) ??
                (key.AgainstVoucherId == key.VoucherId ? vNo : key.AgainstVoucherId.ToString());

            if (!string.IsNullOrWhiteSpace(input.VoucherNo) && !string.Equals(vNo, input.VoucherNo, StringComparison.OrdinalIgnoreCase))
                continue;

            string? partyName = key.PartyType switch
            {
                "Customer" => customerMap.GetValueOrDefault(key.PartyId),
                "Supplier" => supplierMap.GetValueOrDefault(key.PartyId),
                _ => null
            };

            rows.Add(new GeneralPaymentLedgerComparisonRowDto
            {
                CompanyId = key.CompanyId,
                AccountId = key.AccountId,
                AccountCode = acc?.AccountCode ?? string.Empty,
                AccountName = acc?.AccountName ?? string.Empty,
                VoucherType = key.VoucherType,
                VoucherId = key.VoucherId,
                VoucherNo = vNo,
                PartyType = key.PartyType,
                PartyId = key.PartyId,
                PartyName = partyName,
                AgainstVoucherType = key.AgainstVoucherType,
                AgainstVoucherId = key.AgainstVoucherId,
                AgainstVoucherNo = againstVNo,
                GLBalance = glBalance,
                PLBalance = plBalance
            });
        }

        rows = rows
            .OrderBy(r => r.AccountCode)
            .ThenBy(r => r.VoucherType)
            .ThenBy(r => r.VoucherNo)
            .ThenBy(r => r.AgainstVoucherNo)
            .ToList();

        return new GeneralPaymentLedgerComparisonReportDto
        {
            CompanyId = input.CompanyId,
            PeriodStartDate = input.PeriodStartDate,
            PeriodEndDate = input.PeriodEndDate,
            Rows = rows,
            TotalGLBalance = Math.Round(rows.Sum(r => r.GLBalance), 2),
            TotalPLBalance = Math.Round(rows.Sum(r => r.PLBalance), 2),
            TotalDifference = Math.Round(rows.Sum(r => r.Difference), 2)
        };
    }

    public async Task<ProfitabilityAnalysisReportDto> GetProfitabilityAnalysisAsync(
        ProfitabilityAnalysisRequestDto input)
    {
        var allowedFinanceBooks = await ResolveAllowedFinanceBooksAsync(
            input.CompanyId, input.FinanceBook, input.IncludeDefaultBookEntries);

        var accounts = await _accountRepository.GetListAsync(a =>
            a.CompanyId == input.CompanyId &&
            !a.IsGroup &&
            (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var accountMap = accounts.ToDictionary(a => a.Id);
        var targetAccountIds = accountMap.Keys.ToHashSet();

        var toDateExclusive = input.ToDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate >= input.FromDate &&
            je.PostingDate < toDateExclusive);

        var isProject = string.Equals(input.BasedOn, "Project", StringComparison.OrdinalIgnoreCase);

        var lines = journalEntries
            .SelectMany(je => je.Lines)
            .Where(l => targetAccountIds.Contains(l.AccountId))
            .Where(l =>
            {
                var book = l.FinanceBook ?? string.Empty;
                return allowedFinanceBooks.Contains(book);
            })
            .ToList();

        var directIncomeByDim = new Dictionary<Guid, decimal>();
        var directExpenseByDim = new Dictionary<Guid, decimal>();

        foreach (var line in lines)
        {
            var dimId = isProject ? line.ProjectId : line.CostCenterId;
            if (!dimId.HasValue)
                continue;

            var acc = accountMap[line.AccountId];
            if (acc.AccountType == AccountType.Revenue)
            {
                var inc = !line.IsDebit ? line.Amount : -line.Amount;
                directIncomeByDim[dimId.Value] = directIncomeByDim.GetValueOrDefault(dimId.Value, 0m) + inc;
            }
            else if (acc.AccountType == AccountType.Expense)
            {
                var exp = line.IsDebit ? line.Amount : -line.Amount;
                directExpenseByDim[dimId.Value] = directExpenseByDim.GetValueOrDefault(dimId.Value, 0m) + exp;
            }
        }

        var rows = new List<ProfitabilityAnalysisRowDto>();

        if (isProject)
        {
            var projectRepo = _projectRepository ?? LazyServiceProvider.LazyGetService<IRepository<Project, Guid>>();
            var projects = projectRepo != null
                ? await projectRepo.GetListAsync(p => p.CompanyId == input.CompanyId)
                : new List<Project>();

            foreach (var proj in projects.OrderBy(p => p.ProjectName))
            {
                var inc = Math.Round(directIncomeByDim.GetValueOrDefault(proj.Id, 0m), 2);
                var exp = Math.Round(directExpenseByDim.GetValueOrDefault(proj.Id, 0m), 2);

                if (!input.ShowZeroValues && inc == 0m && exp == 0m)
                    continue;

                rows.Add(new ProfitabilityAnalysisRowDto
                {
                    DimensionId = proj.Id,
                    DimensionName = proj.ProjectName,
                    DimensionCode = proj.ProjectNumber,
                    Income = inc,
                    Expense = exp,
                    GrossProfitOrLoss = inc - exp,
                    IsGroup = false,
                    Level = 0
                });
            }
        }
        else
        {
            var ccRepo = _costCenterRepository ?? LazyServiceProvider.LazyGetService<IRepository<CostCenter, Guid>>();
            var costCenters = ccRepo != null
                ? await ccRepo.GetListAsync(c => c.CompanyId == input.CompanyId && c.IsActive)
                : new List<CostCenter>();

            var ccMap = costCenters.ToDictionary(c => c.Id);
            var childrenMap = costCenters
                .Where(c => c.ParentId.HasValue)
                .GroupBy(c => c.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

            (decimal Income, decimal Expense) Accumulate(Guid ccId)
            {
                var inc = directIncomeByDim.GetValueOrDefault(ccId, 0m);
                var exp = directExpenseByDim.GetValueOrDefault(ccId, 0m);

                if (childrenMap.TryGetValue(ccId, out var childIds))
                {
                    foreach (var childId in childIds)
                    {
                        var childTot = Accumulate(childId);
                        inc += childTot.Income;
                        exp += childTot.Expense;
                    }
                }

                return (inc, exp);
            }

            int GetLevel(Guid ccId)
            {
                int lvl = 0;
                var curr = ccMap.GetValueOrDefault(ccId);
                while (curr?.ParentId != null && ccMap.ContainsKey(curr.ParentId.Value))
                {
                    lvl++;
                    curr = ccMap[curr.ParentId.Value];
                }
                return lvl;
            }

            foreach (var cc in costCenters.OrderBy(c => c.Name))
            {
                var (totInc, totExp) = Accumulate(cc.Id);
                totInc = Math.Round(totInc, 2);
                totExp = Math.Round(totExp, 2);

                if (!input.ShowZeroValues && totInc == 0m && totExp == 0m)
                    continue;

                rows.Add(new ProfitabilityAnalysisRowDto
                {
                    DimensionId = cc.Id,
                    DimensionName = cc.Name,
                    DimensionCode = cc.CostCenterNumber,
                    ParentDimensionId = cc.ParentId,
                    IsGroup = cc.IsGroup,
                    Level = GetLevel(cc.Id),
                    Income = totInc,
                    Expense = totExp,
                    GrossProfitOrLoss = totInc - totExp
                });
            }
        }

        var totalIncome = Math.Round(directIncomeByDim.Values.Sum(), 2);
        var totalExpense = Math.Round(directExpenseByDim.Values.Sum(), 2);
        var totalGrossProfit = totalIncome - totalExpense;

        return new ProfitabilityAnalysisReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            BasedOn = input.BasedOn,
            FinanceBook = input.FinanceBook,
            IncludeDefaultBookEntries = input.IncludeDefaultBookEntries,
            Rows = rows,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            TotalGrossProfitOrLoss = totalGrossProfit
        };
    }

    /// <summary>
    /// Daily register of all posted vouchers with optional account legs (ERPNext PR #62896 / commit be89c1c68d).
    /// </summary>
    public async Task<DayBookReportDto> GetDayBookAsync(DayBookRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.Date > input.ToDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var allowedFinanceBooks = await ResolveAllowedFinanceBooksAsync(
            input.CompanyId, input.FinanceBook, input.IncludeDefaultBookEntries);

        var fromDate = DateRangeHelper.StartOfDay(input.FromDate);
        var toDateExclusive = DateRangeHelper.ExclusiveNextDay(input.ToDate);

        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId &&
            je.Status == DocumentStatus.Posted &&
            je.PostingDate >= fromDate &&
            je.PostingDate < toDateExclusive);

        var matchingVouchers = new List<(JournalEntry Entry, string VoucherType, Guid VoucherId, string VoucherNo)>();
        foreach (var je in journalEntries
            .OrderBy(j => j.PostingDate)
            .ThenBy(j => j.CreationTime)
            .ThenBy(j => j.EntryNumber))
        {
            var effectiveType = !string.IsNullOrWhiteSpace(je.ReferenceType)
                ? je.ReferenceType!
                : je.VoucherType.ToString();

            if (!string.IsNullOrWhiteSpace(input.VoucherType) &&
                !string.Equals(effectiveType, input.VoucherType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasRef = !string.IsNullOrWhiteSpace(je.ReferenceType) && je.ReferenceId.HasValue;
            var effectiveId = hasRef ? je.ReferenceId!.Value : je.Id;
            var voucherNo = !string.IsNullOrWhiteSpace(je.ReferenceNumber)
                ? je.ReferenceNumber!
                : (!string.IsNullOrWhiteSpace(je.EntryNumber) ? je.EntryNumber! : effectiveId.ToString());

            matchingVouchers.Add((je, effectiveType, effectiveId, voucherNo));
        }

        if (matchingVouchers.Count == 0)
        {
            return new DayBookReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                VoucherType = input.VoucherType,
                Detailed = input.Detailed,
                ShowRemarks = input.ShowRemarks,
            };
        }

        var accounts = await _accountRepository.GetListAsync(a => a.CompanyId == input.CompanyId);
        var accountMap = accounts.ToDictionary(a => a.Id);

        var allLines = matchingVouchers.SelectMany(v => v.Entry.Lines).ToList();
        var customerIds = allLines
            .Where(l => string.Equals(l.PartyType, "Customer", StringComparison.OrdinalIgnoreCase) && l.PartyId.HasValue)
            .Select(l => l.PartyId!.Value)
            .Distinct()
            .ToList();
        var supplierIds = allLines
            .Where(l => string.Equals(l.PartyType, "Supplier", StringComparison.OrdinalIgnoreCase) && l.PartyId.HasValue)
            .Select(l => l.PartyId!.Value)
            .Distinct()
            .ToList();

        var customerMap = customerIds.Count > 0
            ? (await _customerRepository.GetListAsync(c => customerIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Name)
            : new Dictionary<Guid, string>();
        var supplierMap = supplierIds.Count > 0
            ? (await _supplierRepository.GetListAsync(s => supplierIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name)
            : new Dictionary<Guid, string>();

        string ResolvePartyName(string? partyType, Guid? partyId)
        {
            if (!partyId.HasValue) return string.Empty;
            if (string.Equals(partyType, "Customer", StringComparison.OrdinalIgnoreCase)
                && customerMap.TryGetValue(partyId.Value, out var cName))
            {
                return cName;
            }
            if (string.Equals(partyType, "Supplier", StringComparison.OrdinalIgnoreCase)
                && supplierMap.TryGetValue(partyId.Value, out var sName))
            {
                return sName;
            }
            return string.Empty;
        }

        string ResolveAccountDisplay(Guid accountId)
        {
            if (!accountMap.TryGetValue(accountId, out var acc))
                return accountId.ToString();
            return string.IsNullOrWhiteSpace(acc.AccountCode)
                ? acc.AccountName
                : $"{acc.AccountCode} - {acc.AccountName}";
        }

        var rows = new List<DayBookRowDto>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        int voucherCount = 0;

        foreach (var voucherGroup in matchingVouchers.GroupBy(v => (v.VoucherType, v.VoucherId)))
        {
            var firstEntry = voucherGroup.First().Entry;
            var voucherType = voucherGroup.Key.VoucherType;
            var voucherId = voucherGroup.Key.VoucherId;
            var voucherNo = voucherGroup.Select(v => v.VoucherNo).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? voucherId.ToString();
            var remarks = voucherGroup
                .Select(v => v.Entry.Narration)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? voucherGroup.SelectMany(v => v.Entry.Lines)
                    .Select(l => l.Description)
                    .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

            var legs = voucherGroup
                .SelectMany(v => v.Entry.Lines)
                .Where(l => allowedFinanceBooks.Contains(l.FinanceBook ?? string.Empty))
                .GroupBy(l => (l.AccountId, PartyType: l.PartyType ?? string.Empty, l.PartyId))
                .Select(g =>
                {
                    var debit = g.Sum(l => l.IsDebit ? l.Amount : 0m);
                    var credit = g.Sum(l => !l.IsDebit ? l.Amount : 0m);
                    var balance = Math.Round(debit - credit, 2);
                    var accountDisplay = ResolveAccountDisplay(g.Key.AccountId);
                    var partyName = ResolvePartyName(g.Key.PartyType, g.Key.PartyId);
                    var particulars = !string.IsNullOrWhiteSpace(partyName) ? partyName : accountDisplay;
                    return new
                    {
                        g.Key.AccountId,
                        g.Key.PartyId,
                        AccountDisplay = accountDisplay,
                        Particulars = particulars,
                        Balance = balance
                    };
                })
                .ToList();

            if (legs.Count == 0)
                continue;

            var withParty = legs.Where(l => l.PartyId.HasValue).ToList();
            var positiveLegs = legs.Where(l => l.Balance > 0m).ToList();
            var candidates = withParty.Count > 0 ? withParty : (positiveLegs.Count > 0 ? positiveLegs : legs);
            var main = candidates.OrderByDescending(l => Math.Abs(l.Balance)).First();

            var voucherKey = $"{voucherType}::{voucherId}";
            var summaryDebit = main.Balance > 0m ? main.Balance : (decimal?)null;
            var summaryCredit = main.Balance < 0m ? -main.Balance : (decimal?)null;

            rows.Add(new DayBookRowDto
            {
                VoucherKey = voucherKey,
                ParentVoucherKey = null,
                Indent = 0,
                PostingDate = firstEntry.PostingDate,
                Particulars = main.Particulars,
                VoucherType = voucherType,
                VoucherId = voucherId,
                VoucherNo = voucherNo,
                Account = null,
                Debit = summaryDebit,
                Credit = summaryCredit,
                Remarks = input.ShowRemarks ? remarks : null,
            });

            voucherCount++;
            totalDebit += summaryDebit ?? 0m;
            totalCredit += summaryCredit ?? 0m;

            if (input.Detailed)
            {
                var idx = 0;
                foreach (var leg in legs)
                {
                    if (leg.Balance == 0m)
                        continue;

                    rows.Add(new DayBookRowDto
                    {
                        VoucherKey = $"{voucherKey}::{idx++}",
                        ParentVoucherKey = voucherKey,
                        Indent = 1,
                        PostingDate = null,
                        Particulars = leg.Particulars,
                        VoucherType = null,
                        VoucherId = null,
                        VoucherNo = null,
                        Account = leg.AccountDisplay,
                        Debit = leg.Balance > 0m ? leg.Balance : (decimal?)null,
                        Credit = leg.Balance < 0m ? -leg.Balance : (decimal?)null,
                        Remarks = null,
                    });
                }
            }
        }

        return new DayBookReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            VoucherType = input.VoucherType,
            Detailed = input.Detailed,
            ShowRemarks = input.ShowRemarks,
            Rows = rows,
            VoucherCount = voucherCount,
            TotalDebit = Math.Round(totalDebit, 2),
            TotalCredit = Math.Round(totalCredit, 2),
        };
    }

    /// <summary>
    /// Voucher-wise Balance GL audit report: lists posted vouchers whose total Debit does not equal total Credit.
    /// Per ERPNext accounts/report/voucher_wise_balance.
    /// </summary>
    public async Task<VoucherWiseBalanceReportDto> GetVoucherWiseBalanceAsync(VoucherWiseBalanceRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currencyCode = company?.CurrencyCode ?? "MYR";

        var fromDate = input.FromDate.HasValue ? DateRangeHelper.StartOfDay(input.FromDate.Value) : (DateTime?)null;
        var toDateExclusive = input.ToDate.HasValue ? DateRangeHelper.ExclusiveNextDay(input.ToDate.Value) : (DateTime?)null;

        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId &&
            je.Status == DocumentStatus.Posted &&
            (!fromDate.HasValue || je.PostingDate >= fromDate.Value) &&
            (!toDateExclusive.HasValue || je.PostingDate < toDateExclusive.Value));

        var filteredEntries = new List<(JournalEntry Entry, string VoucherType, Guid VoucherId, string VoucherNo)>();
        foreach (var je in journalEntries)
        {
            var effectiveType = !string.IsNullOrWhiteSpace(je.ReferenceType)
                ? je.ReferenceType!
                : "JournalEntry";

            if (!string.IsNullOrWhiteSpace(input.VoucherType) &&
                !string.Equals(effectiveType, input.VoucherType.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var hasRef = !string.IsNullOrWhiteSpace(je.ReferenceType) && je.ReferenceId.HasValue;
            var effectiveId = hasRef ? je.ReferenceId!.Value : je.Id;
            var voucherNo = !string.IsNullOrWhiteSpace(je.ReferenceNumber)
                ? je.ReferenceNumber!
                : (!string.IsNullOrWhiteSpace(je.EntryNumber) ? je.EntryNumber! : effectiveId.ToString());

            filteredEntries.Add((je, effectiveType, effectiveId, voucherNo));
        }

        if (filteredEntries.Count == 0)
        {
            return new VoucherWiseBalanceReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                CurrencyCode = currencyCode,
            };
        }

        var jeIds = filteredEntries.Select(v => v.Entry.Id).ToHashSet();
        var dbLines = (await _journalLineRepository.GetQueryableAsync())
            .Where(l => jeIds.Contains(l.JournalEntryId))
            .ToList();
        var linesByJe = dbLines.Count > 0
            ? dbLines.GroupBy(l => l.JournalEntryId).ToDictionary(g => g.Key, g => g.ToList())
            : filteredEntries.ToDictionary(v => v.Entry.Id, v => v.Entry.Lines.ToList());

        var rows = new List<VoucherWiseBalanceRowDto>();
        foreach (var voucherGroup in filteredEntries.GroupBy(v => (v.VoucherType, v.VoucherId)))
        {
            var firstEntry = voucherGroup.OrderBy(v => v.Entry.PostingDate).First().Entry;
            var voucherNo = voucherGroup.Select(v => v.VoucherNo).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? voucherGroup.Key.VoucherId.ToString();

            decimal debit = 0m;
            decimal credit = 0m;
            foreach (var v in voucherGroup)
            {
                if (!linesByJe.TryGetValue(v.Entry.Id, out var jeLines)) continue;
                debit += jeLines.Where(l => l.IsDebit).Sum(l => l.Amount);
                credit += jeLines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            }

            debit = Math.Round(debit, 2);
            credit = Math.Round(credit, 2);
            if (debit == credit)
                continue;

            rows.Add(new VoucherWiseBalanceRowDto
            {
                VoucherType = voucherGroup.Key.VoucherType,
                VoucherId = voucherGroup.Key.VoucherId,
                VoucherNo = voucherNo,
                PostingDate = firstEntry.PostingDate,
                Debit = debit,
                Credit = credit,
            });
        }

        rows = rows
            .OrderByDescending(r => Math.Abs(r.Difference))
            .ThenByDescending(r => r.PostingDate)
            .ThenBy(r => r.VoucherNo)
            .ToList();

        return new VoucherWiseBalanceReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            CurrencyCode = currencyCode,
            Rows = rows,
            UnbalancedCount = rows.Count,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
        };
    }

    /// <summary>
    /// Payment Period Based On Invoice Date report: ages each invoice payment relative to the
    /// underlying invoice posting date and calculates payment delay beyond invoice due date.
    /// Per ERPNext accounts/report/payment_period_based_on_invoice_date.
    /// </summary>
    public async Task<PaymentPeriodBasedOnInvoiceDateReportDto> GetPaymentPeriodBasedOnInvoiceDateAsync(
        PaymentPeriodBasedOnInvoiceDateRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.HasValue && input.ToDate.HasValue && input.FromDate.Value.Date > input.ToDate.Value.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var isOutgoing = string.Equals(input.PaymentType, "Outgoing", StringComparison.OrdinalIgnoreCase);
        var normalizedPaymentType = isOutgoing ? "Outgoing" : "Incoming";

        if (!string.IsNullOrWhiteSpace(input.PartyType))
        {
            if ((!isOutgoing && string.Equals(input.PartyType.Trim(), "Supplier", StringComparison.OrdinalIgnoreCase)) ||
                (isOutgoing && string.Equals(input.PartyType.Trim(), "Customer", StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                    .WithData("detail", $"{normalizedPaymentType} payment entries can not be filtered by {input.PartyType}");
            }
        }

        var expectedPartyType = isOutgoing ? "Supplier" : "Customer";
        var expectedAgainstType = isOutgoing ? "PurchaseInvoice" : "SalesInvoice";

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currencyCode = company?.CurrencyCode ?? "MYR";

        var fromDate = input.FromDate.HasValue ? DateRangeHelper.StartOfDay(input.FromDate.Value) : (DateTime?)null;
        var toDateExclusive = input.ToDate.HasValue ? DateRangeHelper.ExclusiveNextDay(input.ToDate.Value) : (DateTime?)null;

        var pleRepo = _pleRepository ?? LazyServiceProvider?.LazyGetService<IRepository<PaymentLedgerEntry, Guid>>();
        var siRepo = LazyServiceProvider?.LazyGetService<IRepository<SalesInvoice, Guid>>();
        var piRepo = LazyServiceProvider?.LazyGetService<IRepository<PurchaseInvoice, Guid>>();
        var peRepo = LazyServiceProvider?.LazyGetService<IRepository<PaymentEntry, Guid>>();

        var rawEntries = new List<(string PaymentDocType, Guid PaymentDocId, string? PaymentDocNo, Guid PartyId, DateTime PostingDate, decimal Amount, string? Remarks, Guid? AgainstId)>();

        if (pleRepo != null)
        {
            var ples = (await pleRepo.GetQueryableAsync())
                .Where(p => p.CompanyId == input.CompanyId
                    && !p.Delinked
                    && (p.VoucherType == "PaymentEntry" || p.VoucherType == "JournalEntry")
                    && p.AgainstVoucherType == expectedAgainstType
                    && p.PartyType == expectedPartyType
                    && (!input.PartyId.HasValue || p.PartyId == input.PartyId.Value)
                    && (!fromDate.HasValue || p.PostingDate >= fromDate.Value)
                    && (!toDateExclusive.HasValue || p.PostingDate < toDateExclusive.Value))
                .ToList();

            foreach (var p in ples)
            {
                var amount = Math.Round(-p.Amount, 2);
                if (amount == 0m) continue;
                rawEntries.Add((p.VoucherType, p.VoucherId, null, p.PartyId, p.PostingDate, amount, p.Remarks,
                    p.AgainstVoucherId != Guid.Empty ? p.AgainstVoucherId : null));
            }
        }

        var siMap = new Dictionary<Guid, SalesInvoice>();
        var piMap = new Dictionary<Guid, PurchaseInvoice>();

        if (!isOutgoing && siRepo != null)
        {
            var invoices = await siRepo.GetListAsync(s => s.CompanyId == input.CompanyId && s.Status == DocumentStatus.Posted);
            siMap = invoices.ToDictionary(s => s.Id);

            foreach (var si in invoices.Where(s => s.IsPos
                && s.AmountPaid != 0m
                && (!input.PartyId.HasValue || s.CustomerId == input.PartyId.Value)
                && (!fromDate.HasValue || s.IssueDate >= fromDate.Value)
                && (!toDateExclusive.HasValue || s.IssueDate < toDateExclusive.Value)))
            {
                var amount = Math.Round(si.AmountPaid * si.ExchangeRate, 2);
                var againstId = si.IsReturn && si.ReturnAgainstId.HasValue ? si.ReturnAgainstId.Value : si.Id;
                rawEntries.Add(("SalesInvoice", si.Id, si.InvoiceNumber, si.CustomerId, si.IssueDate, amount, si.Notes, againstId));
            }
        }
        else if (isOutgoing && piRepo != null)
        {
            var invoices = await piRepo.GetListAsync(p => p.CompanyId == input.CompanyId && p.Status == DocumentStatus.Posted);
            piMap = invoices.ToDictionary(p => p.Id);

            var againstIdsCoveredByPle = rawEntries
                .Where(e => e.AgainstId.HasValue)
                .Select(e => e.AgainstId!.Value)
                .ToHashSet();

            foreach (var pi in invoices.Where(p => p.AmountPaid != 0m
                && !againstIdsCoveredByPle.Contains(p.Id)
                && (!input.PartyId.HasValue || p.SupplierId == input.PartyId.Value)
                && (!fromDate.HasValue || p.IssueDate >= fromDate.Value)
                && (!toDateExclusive.HasValue || p.IssueDate < toDateExclusive.Value)))
            {
                var amount = Math.Round(pi.AmountPaid * pi.ExchangeRate, 2);
                var againstId = pi.IsReturn && pi.ReturnAgainstId.HasValue ? pi.ReturnAgainstId.Value : pi.Id;
                rawEntries.Add(("PurchaseInvoice", pi.Id, pi.InvoiceNumber, pi.SupplierId, pi.IssueDate, amount, pi.Notes, againstId));
            }
        }

        if (rawEntries.Count == 0)
        {
            return new PaymentPeriodBasedOnInvoiceDateReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                PaymentType = normalizedPaymentType,
                CurrencyCode = currencyCode,
            };
        }

        var peIds = rawEntries.Where(r => r.PaymentDocType == "PaymentEntry").Select(r => r.PaymentDocId).Distinct().ToList();
        var peNumbers = new Dictionary<Guid, (string Number, string? Remarks)>();
        if (peRepo != null && peIds.Count > 0)
        {
            peNumbers = (await peRepo.GetListAsync(pe => peIds.Contains(pe.Id)))
                .ToDictionary(pe => pe.Id, pe => (pe.PaymentNumber ?? pe.Id.ToString()[..8], pe.Remarks));
        }

        var jeIds = rawEntries.Where(r => r.PaymentDocType == "JournalEntry").Select(r => r.PaymentDocId).Distinct().ToList();
        var jeNumbers = new Dictionary<Guid, (string Number, string? Remarks)>();
        if (jeIds.Count > 0)
        {
            jeNumbers = (await _journalEntryRepository.GetListAsync(je => jeIds.Contains(je.Id)))
                .ToDictionary(je => je.Id, je => (je.EntryNumber ?? je.Id.ToString()[..8], je.Narration));
        }

        var partyIds = rawEntries.Select(r => r.PartyId).Distinct().ToList();
        var partyNames = isOutgoing
            ? (await _supplierRepository.GetListAsync(s => partyIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name)
            : (await _customerRepository.GetListAsync(c => partyIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Name);

        var rows = new List<PaymentPeriodBasedOnInvoiceDateRowDto>();
        foreach (var entry in rawEntries.OrderBy(r => r.PostingDate).ThenBy(r => r.PaymentDocId))
        {
            var docNo = entry.PaymentDocNo;
            var remarks = entry.Remarks;
            if (entry.PaymentDocType == "PaymentEntry" && peNumbers.TryGetValue(entry.PaymentDocId, out var peInfo))
            {
                docNo ??= peInfo.Number;
                remarks ??= peInfo.Remarks;
            }
            else if (entry.PaymentDocType == "JournalEntry" && jeNumbers.TryGetValue(entry.PaymentDocId, out var jeInfo))
            {
                docNo ??= jeInfo.Number;
                remarks ??= jeInfo.Remarks;
            }
            docNo ??= entry.PaymentDocId.ToString()[..8];

            string? invoiceNo = null;
            DateTime? invoicePostingDate = null;
            DateTime? dueDate = null;
            if (entry.AgainstId.HasValue)
            {
                if (!isOutgoing && siMap.TryGetValue(entry.AgainstId.Value, out var si))
                {
                    invoiceNo = si.InvoiceNumber;
                    invoicePostingDate = si.IssueDate;
                    dueDate = si.DueDate;
                }
                else if (isOutgoing && piMap.TryGetValue(entry.AgainstId.Value, out var pi))
                {
                    invoiceNo = pi.InvoiceNumber;
                    invoicePostingDate = pi.IssueDate;
                    dueDate = pi.DueDate;
                }
            }

            var age = invoicePostingDate.HasValue
                ? Math.Max(0, (entry.PostingDate.Date - invoicePostingDate.Value.Date).Days)
                : 0;
            var delayInDays = dueDate.HasValue
                ? (entry.PostingDate.Date - dueDate.Value.Date).Days
                : 0;

            decimal r0_30 = 0m, r31_60 = 0m, r61_90 = 0m, r90Plus = 0m;
            if (invoicePostingDate.HasValue)
            {
                if (age <= 30) r0_30 = entry.Amount;
                else if (age <= 60) r31_60 = entry.Amount;
                else if (age <= 90) r61_90 = entry.Amount;
                else r90Plus = entry.Amount;
            }

            partyNames.TryGetValue(entry.PartyId, out var partyName);

            rows.Add(new PaymentPeriodBasedOnInvoiceDateRowDto
            {
                PaymentDocumentType = entry.PaymentDocType,
                PaymentDocumentId = entry.PaymentDocId,
                PaymentDocumentNo = docNo,
                PartyType = expectedPartyType,
                PartyId = entry.PartyId,
                PartyName = partyName ?? entry.PartyId.ToString()[..8],
                PostingDate = entry.PostingDate,
                InvoiceType = entry.AgainstId.HasValue ? expectedAgainstType : null,
                InvoiceId = entry.AgainstId,
                InvoiceNo = invoiceNo,
                InvoicePostingDate = invoicePostingDate,
                DueDate = dueDate,
                Amount = entry.Amount,
                Remarks = remarks,
                Age = age,
                Range0To30 = r0_30,
                Range31To60 = r31_60,
                Range61To90 = r61_90,
                Range90Above = r90Plus,
                DelayInPaymentDays = delayInDays,
            });
        }

        return new PaymentPeriodBasedOnInvoiceDateReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            PaymentType = normalizedPaymentType,
            CurrencyCode = currencyCode,
            Rows = rows,
            TotalAmount = rows.Sum(r => r.Amount),
            TotalRange0To30 = rows.Sum(r => r.Range0To30),
            TotalRange31To60 = rows.Sum(r => r.Range31To60),
            TotalRange61To90 = rows.Sum(r => r.Range61To90),
            TotalRange90Above = rows.Sum(r => r.Range90Above),
        };
    }

    /// <summary>
    /// Dimension-wise Accounts Balance Report.
    /// Maps to ERPNext accounts/report/dimension_wise_accounts_balance_report:
    /// - Cross-tabulates account net balances (Debit - Credit) across Cost Centers or Projects
    /// - Excludes Period Closing vouchers and respects Finance Book filtering
    /// - Accumulates leaf balances into parent group accounts and prunes zero-value rows
    /// </summary>
    public async Task<DimensionWiseAccountsBalanceReportDto> GetDimensionWiseAccountsBalanceAsync(
        DimensionWiseAccountsBalanceRequestDto input)
    {
        if (input.CompanyId == Guid.Empty)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "Company is required.");
        }

        if (input.FromDate.Date > input.ToDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var dimension = string.Equals(input.Dimension, "Project", StringComparison.OrdinalIgnoreCase)
            ? "Project"
            : "Cost Center";

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currencyCode = company?.CurrencyCode ?? "MYR";

        var dimensions = new List<DimensionColumnDto>();
        if (dimension == "Project")
        {
            var projectRepo = _projectRepository ?? LazyServiceProvider?.LazyGetService<IRepository<Project, Guid>>();
            if (projectRepo != null)
            {
                var projects = await projectRepo.GetListAsync(p => p.CompanyId == input.CompanyId);
                dimensions.AddRange(projects
                    .OrderBy(p => p.ProjectName)
                    .Select(p => new DimensionColumnDto { DimensionId = p.Id, DimensionName = p.ProjectName }));
            }
        }
        else
        {
            var ccRepo = _costCenterRepository ?? LazyServiceProvider?.LazyGetService<IRepository<CostCenter, Guid>>();
            if (ccRepo != null)
            {
                var costCenters = await ccRepo.GetListAsync(c => c.CompanyId == input.CompanyId && c.IsActive);
                dimensions.AddRange(costCenters
                    .OrderBy(c => c.Name)
                    .Select(c => new DimensionColumnDto { DimensionId = c.Id, DimensionName = c.Name }));
            }
        }

        if (dimensions.Count == 0)
        {
            return new DimensionWiseAccountsBalanceReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Dimension = dimension,
                CurrencyCode = currencyCode,
            };
        }

        var dimIndexMap = dimensions
            .Select((d, idx) => (d.DimensionId, idx))
            .ToDictionary(x => x.DimensionId, x => x.idx);

        var accounts = (await _accountRepository.GetListAsync(a => a.CompanyId == input.CompanyId))
            .OrderBy(a => a.AccountCode)
            .ToList();
        if (accounts.Count == 0)
        {
            return new DimensionWiseAccountsBalanceReportDto
            {
                CompanyId = input.CompanyId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                Dimension = dimension,
                CurrencyCode = currencyCode,
                Dimensions = dimensions,
                DimensionTotals = new decimal[dimensions.Count],
            };
        }

        var allowedFinanceBooks = await ResolveAllowedFinanceBooksAsync(
            input.CompanyId, input.FinanceBook, input.IncludeDefaultBookEntries);

        var fromDate = DateRangeHelper.StartOfDay(input.FromDate);
        var toDateExclusive = DateRangeHelper.ExclusiveNextDay(input.ToDate);

        List<JournalEntry> journalEntries;
        try
        {
            var withDetails = await _journalEntryRepository.WithDetailsAsync(je => je.Lines);
            journalEntries = withDetails
                .Where(je =>
                    je.CompanyId == input.CompanyId &&
                    je.Status == DocumentStatus.Posted &&
                    je.VoucherType != JournalEntryVoucherType.PeriodClosing &&
                    je.PostingDate >= fromDate &&
                    je.PostingDate < toDateExclusive)
                .ToList();
        }
        catch
        {
            journalEntries = await _journalEntryRepository.GetListAsync(je =>
                je.CompanyId == input.CompanyId &&
                je.Status == DocumentStatus.Posted &&
                je.VoucherType != JournalEntryVoucherType.PeriodClosing &&
                je.PostingDate >= fromDate &&
                je.PostingDate < toDateExclusive);
        }

        var accountDimBalances = accounts.ToDictionary(a => a.Id, _ => new decimal[dimensions.Count]);

        foreach (var line in journalEntries.SelectMany(je => je.Lines))
        {
            var book = line.FinanceBook ?? string.Empty;
            if (!allowedFinanceBooks.Contains(book))
                continue;

            var dimId = dimension == "Project" ? line.ProjectId : line.CostCenterId;
            if (!dimId.HasValue || !dimIndexMap.TryGetValue(dimId.Value, out int colIdx))
                continue;

            if (accountDimBalances.TryGetValue(line.AccountId, out var balances))
            {
                balances[colIdx] += line.Debit - line.Credit;
            }
        }

        var childrenMap = accounts
            .Where(a => a.ParentAccountId.HasValue)
            .GroupBy(a => a.ParentAccountId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.AccountCode).ToList());
        var accountById = accounts.ToDictionary(a => a.Id);
        var roots = accounts.Where(a => !a.ParentAccountId.HasValue || !accountById.ContainsKey(a.ParentAccountId.Value))
            .OrderBy(a => a.AccountCode)
            .ToList();

        // Accumulate leaf balances into parents (post-order traversal)
        var aggregatedBalances = new Dictionary<Guid, decimal[]>();
        decimal[] Accumulate(Account acc)
        {
            var sum = (decimal[])accountDimBalances[acc.Id].Clone();
            if (childrenMap.TryGetValue(acc.Id, out var kids))
            {
                foreach (var child in kids)
                {
                    var childSum = Accumulate(child);
                    for (int i = 0; i < dimensions.Count; i++)
                    {
                        sum[i] += childSum[i];
                    }
                }
            }
            aggregatedBalances[acc.Id] = sum;
            return sum;
        }

        foreach (var root in roots)
        {
            Accumulate(root);
        }

        var rows = new List<DimensionWiseAccountBalanceRowDto>();
        void Flatten(Account acc, int level)
        {
            var vals = aggregatedBalances[acc.Id].Select(v => Math.Round(v, 2)).ToArray();
            var rowTotal = Math.Round(vals.Sum(), 2);
            bool hasNonZero = vals.Any(v => Math.Abs(v) >= 0.01m);

            var childRows = new List<DimensionWiseAccountBalanceRowDto>();
            if (childrenMap.TryGetValue(acc.Id, out var kids))
            {
                int beforeCount = rows.Count;
                foreach (var child in kids)
                {
                    Flatten(child, level + 1);
                }
                if (rows.Count > beforeCount)
                {
                    // Keep parent group when any descendant row was kept
                    hasNonZero = true;
                    // Reorder so parent precedes its descendants
                    childRows = rows.GetRange(beforeCount, rows.Count - beforeCount);
                    rows.RemoveRange(beforeCount, rows.Count - beforeCount);
                }
            }

            if (input.ShowZeroValues || hasNonZero)
            {
                rows.Add(new DimensionWiseAccountBalanceRowDto
                {
                    AccountId = acc.Id,
                    AccountCode = acc.AccountCode,
                    AccountName = string.IsNullOrWhiteSpace(acc.AccountCode)
                        ? acc.AccountName
                        : $"{acc.AccountCode} - {acc.AccountName}",
                    ParentAccountId = acc.ParentAccountId,
                    IsGroup = acc.IsGroup,
                    Level = level,
                    DimensionBalances = vals,
                    Total = rowTotal,
                });
                rows.AddRange(childRows);
            }
        }

        foreach (var root in roots)
        {
            Flatten(root, 0);
        }

        // Totals across leaf accounts only to avoid double-counting parent groups
        var dimensionTotals = new decimal[dimensions.Count];
        foreach (var acc in accounts.Where(a => !a.IsGroup))
        {
            var leafVals = accountDimBalances[acc.Id];
            for (int i = 0; i < dimensions.Count; i++)
            {
                dimensionTotals[i] += leafVals[i];
            }
        }
        dimensionTotals = dimensionTotals.Select(v => Math.Round(v, 2)).ToArray();

        return new DimensionWiseAccountsBalanceReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Dimension = dimension,
            CurrencyCode = currencyCode,
            Dimensions = dimensions,
            Rows = rows,
            DimensionTotals = dimensionTotals,
            GrandTotal = Math.Round(dimensionTotals.Sum(), 2),
        };
    }

    private async Task<HashSet<string>> ResolveAllowedFinanceBooksAsync(
        Guid companyId,
        string? financeBook,
        bool includeDefaultBookEntries)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

        if (!string.IsNullOrWhiteSpace(financeBook))
        {
            allowed.Add(financeBook.Trim());
        }

        if (includeDefaultBookEntries)
        {
            var defaultBook = await GetDefaultFinanceBookNameAsync(companyId);
            if (!string.IsNullOrWhiteSpace(defaultBook))
            {
                if (!string.IsNullOrWhiteSpace(financeBook) &&
                    !string.Equals(financeBook.Trim(), defaultBook, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UserFriendlyException("To use a different finance book, please uncheck 'Include Default FB Entries'");
                }
                allowed.Add(defaultBook);
            }
        }

        return allowed;
    }

    private async Task<string?> GetDefaultFinanceBookNameAsync(Guid companyId)
    {
        var repo = _financeBookRepository ?? LazyServiceProvider.LazyGetService<IRepository<FinanceBook, Guid>>();
        if (repo == null) return null;

        var list = await repo.GetListAsync(fb => fb.CompanyId == companyId && fb.IsDefault);
        return list.FirstOrDefault()?.Name;
    }

    private sealed record ComparisonKey(
        Guid CompanyId,
        Guid AccountId,
        string VoucherType,
        Guid VoucherId,
        string PartyType,
        Guid PartyId,
        string AgainstVoucherType,
        Guid AgainstVoucherId);
}


