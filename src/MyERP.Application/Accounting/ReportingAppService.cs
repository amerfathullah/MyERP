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
        var rootCompany = await _companyRepository.FindAsync(input.CompanyId);
        var reportingCurrency = !string.IsNullOrWhiteSpace(rootCompany?.ReportingCurrency)
            ? rootCompany!.ReportingCurrency!
            : (rootCompany?.CurrencyCode ?? "MYR");

        if (input.IncludeSubsidiaries && rootCompany != null)
        {
            return await BuildConsolidatedTrialBalanceAsync(rootCompany, reportingCurrency, input.AsOfDate);
        }

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
            CurrencyCode = reportingCurrency,
            IsConsolidated = false,
            ConsolidatedCompanyCount = 1,
            Rows = rows,
            TotalDebit = Math.Round(rows.Sum(r => r.Debit), 2),
            TotalCredit = Math.Round(rows.Sum(r => r.Credit), 2),
        };
    }

    private static (List<Company> CompaniesInScope, Dictionary<Guid, HashSet<Guid>> SubtreeIdsByCompany) ExpandCompanyHierarchy(
        Company rootCompany,
        List<Company> allCompanies)
    {
        var companiesByParent = allCompanies
            .Where(c => c.ParentCompanyId.HasValue)
            .GroupBy(c => c.ParentCompanyId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var companiesInScope = new List<Company>();
        var visitedIds = new HashSet<Guid>();
        var queue = new Queue<Company>();
        queue.Enqueue(rootCompany);
        visitedIds.Add(rootCompany.Id);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            companiesInScope.Add(current);

            if (companiesByParent.TryGetValue(current.Id, out var children))
            {
                foreach (var child in children.OrderBy(c => c.Name))
                {
                    if (visitedIds.Add(child.Id))
                    {
                        queue.Enqueue(child);
                    }
                }
            }
        }

        // Precompute full descendant scope for each company in scope (including itself)
        var subtreeByCompany = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var company in companiesInScope)
        {
            var subtree = new HashSet<Guid>();
            var subQueue = new Queue<Guid>();
            subQueue.Enqueue(company.Id);
            subtree.Add(company.Id);

            while (subQueue.Count > 0)
            {
                var cid = subQueue.Dequeue();
                if (companiesByParent.TryGetValue(cid, out var kids))
                {
                    foreach (var kid in kids)
                    {
                        if (subtree.Add(kid.Id))
                            subQueue.Enqueue(kid.Id);
                    }
                }
            }

            subtreeByCompany[company.Id] = subtree;
        }

        return (companiesInScope, subtreeByCompany);
    }

    private async Task<TrialBalanceReportDto> BuildConsolidatedTrialBalanceAsync(
        Company rootCompany,
        string reportingCurrency,
        DateTime asOfDate)
    {
        var allCompanies = await _companyRepository.GetListAsync();
        var (companiesInScope, _) = ExpandCompanyHierarchy(rootCompany, allCompanies);

        var scopeIds = companiesInScope.Select(c => c.Id).ToHashSet();
        var allAccounts = await _accountRepository.GetListAsync(a => scopeIds.Contains(a.CompanyId) && !a.IsGroup);
        var accountsByCompany = allAccounts
            .GroupBy(a => a.CompanyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var exchangeRepo = LazyServiceProvider?.LazyGetService<IRepository<CurrencyExchange, Guid>>();
        var exchanges = exchangeRepo != null
            ? await exchangeRepo.GetListAsync(e => e.Date <= asOfDate.Date.AddDays(1))
            : new List<CurrencyExchange>();

        // Merge leaf account balances across parent + subsidiaries by AccountCode (fallback AccountName)
        var consolidated = new Dictionary<string, (Guid AccountId, string Code, string Name, string Type, decimal Debit, decimal Credit)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var company in companiesInScope)
        {
            var rate = ResolveConsolidationExchangeRate(company.CurrencyCode, reportingCurrency, asOfDate, exchanges);
            var companyAccounts = accountsByCompany.GetValueOrDefault(company.Id, new List<Account>());
            var balanceMap = await _balanceService.GetTrialBalanceAsync(company.Id, asOfDate);

            foreach (var account in companyAccounts)
            {
                if (!balanceMap.TryGetValue(account.Id, out var balance))
                    continue;

                if (balance.Debit == 0 && balance.Credit == 0)
                    continue;

                var translatedDebit = Math.Round(balance.Debit * rate, 2, MidpointRounding.AwayFromZero);
                var translatedCredit = Math.Round(balance.Credit * rate, 2, MidpointRounding.AwayFromZero);

                var mergeKey = !string.IsNullOrWhiteSpace(account.AccountCode)
                    ? account.AccountCode.Trim()
                    : account.AccountName.Trim();

                if (consolidated.TryGetValue(mergeKey, out var existing))
                {
                    consolidated[mergeKey] = (
                        company.Id == rootCompany.Id ? account.Id : existing.AccountId,
                        existing.Code,
                        existing.Name,
                        existing.Type,
                        existing.Debit + translatedDebit,
                        existing.Credit + translatedCredit);
                }
                else
                {
                    consolidated[mergeKey] = (
                        account.Id,
                        account.AccountCode,
                        account.AccountName,
                        account.AccountType.ToString(),
                        translatedDebit,
                        translatedCredit);
                }
            }
        }

        var rows = consolidated.Values
            .OrderBy(v => v.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(v =>
            {
                var net = v.Debit - v.Credit;
                return new TrialBalanceRowDto
                {
                    AccountId = v.AccountId,
                    AccountCode = v.Code,
                    AccountName = v.Name,
                    AccountType = v.Type,
                    IsGroup = false,
                    Level = 0,
                    Debit = Math.Round(v.Debit, 2),
                    Credit = Math.Round(v.Credit, 2),
                    ClosingDebit = Math.Round(net > 0 ? net : 0, 2),
                    ClosingCredit = Math.Round(net < 0 ? Math.Abs(net) : 0, 2),
                };
            })
            .ToList();

        // Foreign Currency Translation Reserve (FCTR) per ERPNext consolidated_trial_balance.py:
        // Balance any rounding / FX translation delta under Equity so consolidated Total Debit == Total Credit.
        var totalDebit = Math.Round(rows.Sum(r => r.Debit), 2);
        var totalCredit = Math.Round(rows.Sum(r => r.Credit), 2);
        var fxDiff = Math.Round(totalDebit - totalCredit, 2);
        if (fxDiff != 0m)
        {
            var fctrDebit = fxDiff < 0 ? Math.Abs(fxDiff) : 0m;
            var fctrCredit = fxDiff > 0 ? fxDiff : 0m;
            var fctrName = "Foreign Currency Translation Reserve";
            if (LocalizationResource != null && LazyServiceProvider != null)
            {
                var localized = L["ForeignCurrencyTranslationReserve"];
                if (!localized.ResourceNotFound)
                    fctrName = localized.Value;
            }
            rows.Add(new TrialBalanceRowDto
            {
                AccountId = Guid.Empty,
                AccountCode = "FCTR",
                AccountName = fctrName,
                AccountType = AccountType.Equity.ToString(),
                IsGroup = false,
                Level = 0,
                Debit = fctrDebit,
                Credit = fctrCredit,
                ClosingDebit = fctrDebit,
                ClosingCredit = fctrCredit,
            });
            totalDebit = Math.Round(rows.Sum(r => r.Debit), 2);
            totalCredit = Math.Round(rows.Sum(r => r.Credit), 2);
        }

        return new TrialBalanceReportDto
        {
            AsOfDate = asOfDate,
            CompanyId = rootCompany.Id,
            CurrencyCode = reportingCurrency,
            IsConsolidated = true,
            ConsolidatedCompanyCount = companiesInScope.Count,
            Rows = rows,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
        };
    }

    private static decimal ResolveConsolidationExchangeRate(
        string fromCurrency,
        string toCurrency,
        DateTime asOfDate,
        List<CurrencyExchange> exchanges)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var cutoff = asOfDate.Date;
        var direct = exchanges
            .Where(e => string.Equals(e.FromCurrency, fromCurrency, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(e.ToCurrency, toCurrency, StringComparison.OrdinalIgnoreCase)
                     && e.Date.Date <= cutoff
                     && e.ExchangeRate > 0)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (direct != null)
            return direct.ExchangeRate;

        var inverse = exchanges
            .Where(e => string.Equals(e.FromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(e.ToCurrency, fromCurrency, StringComparison.OrdinalIgnoreCase)
                     && e.Date.Date <= cutoff
                     && e.ExchangeRate > 0)
            .OrderByDescending(e => e.Date)
            .FirstOrDefault();

        if (inverse != null)
            return 1m / inverse.ExchangeRate;

        throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
            .WithData("detail", $"Exchange rate from '{fromCurrency}' to '{toCurrency}' on or before {cutoff:yyyy-MM-dd} is required for consolidated Trial Balance.");
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

    public async Task<GrossAndNetProfitReportDto> GetGrossAndNetProfitAsync(GrossAndNetProfitRequestDto input)
    {
        if (input.FromDate.Date > input.ToDate.Date)
        {
            throw new UserFriendlyException("From Date cannot be after To Date.");
        }

        var company = await _companyRepository.FindAsync(input.CompanyId);
        var currencyCode = company?.CurrencyCode ?? "MYR";
        var periods = BuildReportPeriods(input.FromDate.Date, input.ToDate.Date, input.Periodicity);
        var periodCount = periods.Count;

        var accounts = await _accountRepository.GetListAsync(a =>
            a.CompanyId == input.CompanyId
            && (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense));

        var endExclusive = input.ToDate.Date.AddDays(1);
        var journalEntries = await _journalEntryRepository.GetListAsync(je =>
            je.CompanyId == input.CompanyId
            && je.Status == DocumentStatus.Posted
            && je.PostingDate >= input.FromDate.Date
            && je.PostingDate < endExclusive
            && je.ReferenceType != "PeriodClosingVoucher");

        var leafAmounts = new Dictionary<Guid, decimal[]>();
        foreach (var acc in accounts.Where(a => !a.IsGroup))
        {
            leafAmounts[acc.Id] = new decimal[periodCount];
        }

        var accountById = accounts.ToDictionary(a => a.Id);

        foreach (var je in journalEntries)
        {
            var periodIdx = FindPeriodIndex(periods, je.PostingDate.Date);
            if (periodIdx < 0) continue;

            foreach (var line in je.Lines)
            {
                if (!leafAmounts.TryGetValue(line.AccountId, out var arr)) continue;
                if (input.CostCenterId.HasValue && line.CostCenterId != input.CostCenterId.Value) continue;
                if (input.ProjectId.HasValue && line.ProjectId != input.ProjectId.Value) continue;

                var acc = accountById[line.AccountId];
                var signed = acc.AccountType == AccountType.Revenue
                    ? (line.IsDebit ? -line.Amount : line.Amount)
                    : (line.IsDebit ? line.Amount : -line.Amount);
                arr[periodIdx] += signed;
            }
        }

        if (input.AccumulatedValues && periodCount > 1)
        {
            foreach (var arr in leafAmounts.Values)
            {
                for (int i = 1; i < periodCount; i++)
                {
                    arr[i] += arr[i - 1];
                }
            }
        }

        var incomeAccounts = accounts.Where(a => a.AccountType == AccountType.Revenue).ToList();
        var expenseAccounts = accounts.Where(a => a.AccountType == AccountType.Expense).ToList();

        var grossIncome = BuildFilteredAccountTreeRows(incomeAccounts, leafAmounts, periodCount, includeInGross: true, input.AccumulatedValues);
        var grossExpense = BuildFilteredAccountTreeRows(expenseAccounts, leafAmounts, periodCount, includeInGross: true, input.AccumulatedValues);
        var nonGrossIncome = BuildFilteredAccountTreeRows(incomeAccounts, leafAmounts, periodCount, includeInGross: false, input.AccumulatedValues);
        var nonGrossExpense = BuildFilteredAccountTreeRows(expenseAccounts, leafAmounts, periodCount, includeInGross: false, input.AccumulatedValues);

        var rows = new List<GrossAndNetProfitRowDto>();
        var nothingIncludedInGross = grossIncome.Count == 0 && grossExpense.Count == 0;

        var (grossIncomePeriodTotals, grossIncomeRootTotal) = SumRootTotals(grossIncome, periodCount);
        var (grossExpensePeriodTotals, grossExpenseRootTotal) = SumRootTotals(grossExpense, periodCount);
        var (nonGrossIncomePeriodTotals, nonGrossIncomeRootTotal) = SumRootTotals(nonGrossIncome, periodCount);
        var (nonGrossExpensePeriodTotals, nonGrossExpenseRootTotal) = SumRootTotals(nonGrossExpense, periodCount);

        if (!nothingIncludedInGross)
        {
            rows.Add(new GrossAndNetProfitRowDto
            {
                AccountName = "Included in Gross Profit",
                RowKind = "SectionHeader",
                PeriodAmounts = new decimal[periodCount],
            });
            rows.AddRange(grossIncome);
            rows.AddRange(grossExpense);

            var gpPeriods = new decimal[periodCount];
            for (int i = 0; i < periodCount; i++)
            {
                gpPeriods[i] = Math.Round(grossIncomePeriodTotals[i] - grossExpensePeriodTotals[i], 2);
            }
            rows.Add(new GrossAndNetProfitRowDto
            {
                AccountName = "Gross Profit",
                RowKind = "GrossProfit",
                PeriodAmounts = gpPeriods,
                Total = Math.Round(grossIncomeRootTotal - grossExpenseRootTotal, 2),
            });
        }
        else
        {
            rows.Add(new GrossAndNetProfitRowDto
            {
                AccountName = "Nothing is included in gross",
                RowKind = "EmptyNote",
                PeriodAmounts = new decimal[periodCount],
            });
        }

        rows.AddRange(nonGrossIncome);
        rows.AddRange(nonGrossExpense);

        var npPeriods = new decimal[periodCount];
        for (int i = 0; i < periodCount; i++)
        {
            var totIncome = grossIncomePeriodTotals[i] + nonGrossIncomePeriodTotals[i];
            var totExpense = grossExpensePeriodTotals[i] + nonGrossExpensePeriodTotals[i];
            npPeriods[i] = Math.Round(totIncome - totExpense, 2);
        }
        var netProfitTotal = Math.Round(
            (grossIncomeRootTotal + nonGrossIncomeRootTotal) - (grossExpenseRootTotal + nonGrossExpenseRootTotal),
            2);

        if (rows.Any(r => r.RowKind == "Account") || netProfitTotal != 0m)
        {
            rows.Add(new GrossAndNetProfitRowDto
            {
                AccountName = "Net Profit",
                RowKind = "NetProfit",
                PeriodAmounts = npPeriods,
                Total = netProfitTotal,
            });
        }

        return new GrossAndNetProfitReportDto
        {
            CompanyId = input.CompanyId,
            CurrencyCode = currencyCode,
            FromDate = input.FromDate.Date,
            ToDate = input.ToDate.Date,
            PeriodLabels = periods.Select(p => p.Label).ToArray(),
            NothingIncludedInGross = nothingIncludedInGross,
            GrossIncomeTotal = Math.Round(grossIncomeRootTotal, 2),
            GrossExpenseTotal = Math.Round(grossExpenseRootTotal, 2),
            GrossProfit = Math.Round(grossIncomeRootTotal - grossExpenseRootTotal, 2),
            NonGrossIncomeTotal = Math.Round(nonGrossIncomeRootTotal, 2),
            NonGrossExpenseTotal = Math.Round(nonGrossExpenseRootTotal, 2),
            NetProfit = netProfitTotal,
            Rows = rows,
        };
    }

    private sealed record ReportPeriodBucket(DateTime Start, DateTime End, string Label);

    private static List<ReportPeriodBucket> BuildReportPeriods(DateTime fromDate, DateTime toDate, string? periodicity)
    {
        var norm = (periodicity ?? "Yearly").Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (norm is not ("MONTHLY" or "QUARTERLY" or "HALFYEARLY"))
        {
            return new List<ReportPeriodBucket>
            {
                new(fromDate, toDate, $"{fromDate:yyyy-MM-dd} - {toDate:yyyy-MM-dd}")
            };
        }

        var stepMonths = norm switch
        {
            "MONTHLY" => 1,
            "QUARTERLY" => 3,
            "HALFYEARLY" => 6,
            _ => 12,
        };

        var result = new List<ReportPeriodBucket>();
        var cursor = fromDate;
        while (cursor <= toDate)
        {
            var nextStart = cursor.AddMonths(stepMonths);
            var end = nextStart.AddDays(-1);
            if (end > toDate) end = toDate;
            var label = stepMonths == 1
                ? cursor.ToString("MMM yyyy")
                : $"{cursor:MMM yy}-{end:MMM yy}";
            result.Add(new ReportPeriodBucket(cursor, end, label));
            cursor = nextStart;
        }
        return result;
    }

    private static int FindPeriodIndex(List<ReportPeriodBucket> periods, DateTime postingDate)
    {
        for (int i = 0; i < periods.Count; i++)
        {
            if (postingDate >= periods[i].Start && postingDate <= periods[i].End)
                return i;
        }
        return -1;
    }

    private static List<GrossAndNetProfitRowDto> BuildFilteredAccountTreeRows(
        List<Account> typeAccounts,
        Dictionary<Guid, decimal[]> leafAmounts,
        int periodCount,
        bool includeInGross,
        bool accumulatedValues)
    {
        var kept = typeAccounts
            .Where(a => a.IsGroup || a.IncludeInGross == includeInGross)
            .OrderBy(a => a.AccountCode)
            .ThenBy(a => a.AccountName)
            .ToList();

        // Prune group nodes that have no remaining children (ERPNext remove_parent_with_no_child)
        var removedAny = true;
        while (removedAny)
        {
            removedAny = false;
            var parentIdsWithChildren = kept
                .Where(a => a.ParentAccountId.HasValue)
                .Select(a => a.ParentAccountId!.Value)
                .ToHashSet();

            for (int i = kept.Count - 1; i >= 0; i--)
            {
                if (kept[i].IsGroup && !parentIdsWithChildren.Contains(kept[i].Id))
                {
                    kept.RemoveAt(i);
                    removedAny = true;
                }
            }
        }

        if (kept.Count == 0) return new List<GrossAndNetProfitRowDto>();

        var keptIds = kept.Select(a => a.Id).ToHashSet();
        var byId = kept.ToDictionary(a => a.Id);

        // Compute period arrays for every node; group totals reflect only kept children (adjust_account_totals)
        var nodePeriods = new Dictionary<Guid, decimal[]>();
        foreach (var a in kept)
        {
            nodePeriods[a.Id] = a.IsGroup
                ? new decimal[periodCount]
                : (leafAmounts.TryGetValue(a.Id, out var arr) ? (decimal[])arr.Clone() : new decimal[periodCount]);
        }

        foreach (var leaf in kept.Where(a => !a.IsGroup))
        {
            var leafArr = nodePeriods[leaf.Id];
            var curParentId = leaf.ParentAccountId;
            var guard = 0;
            while (curParentId.HasValue && guard++ < 64 && byId.TryGetValue(curParentId.Value, out var parentAcc))
            {
                var pArr = nodePeriods[parentAcc.Id];
                for (int p = 0; p < periodCount; p++)
                {
                    pArr[p] += leafArr[p];
                }
                curParentId = parentAcc.ParentAccountId;
            }
        }

        // Remove zero subtrees matching financial_statements.filter_out_zero_value_rows
        var nonZeroIds = kept
            .Where(a => nodePeriods[a.Id].Any(v => Math.Abs(v) >= 0.005m))
            .Select(a => a.Id)
            .ToHashSet();
        kept = kept.Where(a => nonZeroIds.Contains(a.Id)).ToList();
        keptIds = kept.Select(a => a.Id).ToHashSet();

        if (kept.Count == 0) return new List<GrossAndNetProfitRowDto>();

        var childrenLookup = kept
            .Where(a => a.ParentAccountId.HasValue && keptIds.Contains(a.ParentAccountId.Value))
            .OrderBy(a => a.AccountCode)
            .ToLookup(a => a.ParentAccountId!.Value);

        var roots = kept
            .Where(a => !a.ParentAccountId.HasValue || !keptIds.Contains(a.ParentAccountId.Value))
            .OrderBy(a => a.AccountCode)
            .ToList();

        var ordered = new List<GrossAndNetProfitRowDto>();
        void Visit(Account acc, int indent)
        {
            var pArr = nodePeriods[acc.Id].Select(v => Math.Round(v, 2)).ToArray();
            ordered.Add(new GrossAndNetProfitRowDto
            {
                AccountId = acc.Id,
                AccountCode = acc.AccountCode,
                AccountName = acc.AccountName,
                AccountType = acc.AccountType.ToString(),
                ParentAccountId = acc.ParentAccountId.HasValue && keptIds.Contains(acc.ParentAccountId.Value) ? acc.ParentAccountId : null,
                IsGroup = acc.IsGroup,
                IncludeInGross = acc.IncludeInGross,
                Indent = indent,
                RowKind = "Account",
                PeriodAmounts = pArr,
                Total = Math.Round(accumulatedValues && pArr.Length > 0 ? pArr[^1] : pArr.Sum(), 2),
            });

            foreach (var child in childrenLookup[acc.Id])
            {
                Visit(child, indent + 1);
            }
        }

        foreach (var root in roots)
        {
            Visit(root, 0);
        }

        return ordered;
    }

    private static (decimal[] PeriodTotals, decimal RootTotal) SumRootTotals(
        List<GrossAndNetProfitRowDto> sectionRows,
        int periodCount)
    {
        var periodTotals = new decimal[periodCount];
        decimal total = 0m;
        foreach (var row in sectionRows.Where(r => r.Indent == 0))
        {
            for (int i = 0; i < periodCount && i < row.PeriodAmounts.Length; i++)
            {
                periodTotals[i] += row.PeriodAmounts[i];
            }
            total += row.Total;
        }
        return (periodTotals, total);
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

    /// <summary>
    /// Consolidated Financial Statement across a holding company and all subsidiaries.
    /// Ports ERPNext consolidated_financial_statement.py (Balance Sheet, P&L, Cash Flow)
    /// with per-company columns, accumulated-in-group-company rollup, and transaction/closing FX translation + FCTR.
    /// </summary>
    public async Task<ConsolidatedFinancialStatementDto> GetConsolidatedFinancialStatementAsync(
        ConsolidatedFinancialStatementRequestDto input)
    {
        if (input.ToDate.Date < input.FromDate.Date)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To Date must be on or after From Date.");
        }

        var rootCompany = await _companyRepository.GetAsync(input.CompanyId);
        var allCompanies = await _companyRepository.GetListAsync();
        var (companiesInScope, subtreeByCompany) = ExpandCompanyHierarchy(rootCompany, allCompanies);

        var normalizedReport = NormalizeConsolidatedReportType(input.ReportType);
        var explicitPresentationCurrency = !string.IsNullOrWhiteSpace(input.PresentationCurrency)
            ? input.PresentationCurrency.Trim().ToUpperInvariant()
            : null;
        var rootPresentationCurrency = explicitPresentationCurrency
            ?? (!string.IsNullOrWhiteSpace(rootCompany.ReportingCurrency)
                ? rootCompany.ReportingCurrency!.Trim().ToUpperInvariant()
                : rootCompany.CurrencyCode.ToUpperInvariant());

        var columnCurrencies = companiesInScope.ToDictionary(
            c => c.Id,
            c => explicitPresentationCurrency ?? c.CurrencyCode.ToUpperInvariant());

        var companyColumns = companiesInScope.Select(c => new ConsolidatedCompanyColumnDto
        {
            CompanyId = c.Id,
            CompanyName = c.Name,
            CurrencyCode = columnCurrencies[c.Id],
            IsGroup = c.IsGroup,
        }).ToList();

        var scopeIds = companiesInScope.Select(c => c.Id).ToHashSet();
        var allAccounts = await _accountRepository.GetListAsync(a => scopeIds.Contains(a.CompanyId));
        var accountById = allAccounts.ToDictionary(a => a.Id);

        var toExclusive = input.ToDate.Date.AddDays(1);
        var allJournals = await _journalEntryRepository.GetListAsync(je =>
            scopeIds.Contains(je.CompanyId)
            && je.Status == DocumentStatus.Posted
            && je.PostingDate < toExclusive);

        var allowedBooksByCompany = new Dictionary<Guid, HashSet<string>>();
        foreach (var comp in companiesInScope)
        {
            allowedBooksByCompany[comp.Id] = await ResolveAllowedFinanceBooksAsync(
                comp.Id, input.FinanceBook, input.IncludeDefaultBookEntries);
        }

        var exchangeRepo = LazyServiceProvider?.LazyGetService<IRepository<CurrencyExchange, Guid>>();
        var exchanges = exchangeRepo != null
            ? await exchangeRepo.GetListAsync(e => e.Date <= toExclusive)
            : new List<CurrencyExchange>();

        var context = new ConsolidatedStatementContext(
            rootCompany.Id,
            rootPresentationCurrency,
            columnCurrencies,
            companiesInScope,
            subtreeByCompany,
            allAccounts,
            accountById,
            allJournals,
            allowedBooksByCompany,
            exchanges,
            input.FromDate.Date,
            input.ToDate.Date,
            input.AccumulatedInGroupCompany,
            input.ShowZeroValues);

        var (rows, summary) = normalizedReport switch
        {
            "ProfitAndLoss" => BuildConsolidatedProfitLoss(context),
            "CashFlow" => BuildConsolidatedCashFlow(context),
            _ => BuildConsolidatedBalanceSheet(context),
        };

        return new ConsolidatedFinancialStatementDto
        {
            RootCompanyId = rootCompany.Id,
            ReportType = normalizedReport,
            PresentationCurrency = rootPresentationCurrency,
            FromDate = input.FromDate.Date,
            ToDate = input.ToDate.Date,
            AccumulatedInGroupCompany = input.AccumulatedInGroupCompany,
            CompanyColumns = companyColumns,
            Rows = rows,
            Summary = summary,
        };
    }

    private static string NormalizeConsolidatedReportType(string? reportType)
    {
        var key = (reportType ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        return key switch
        {
            "profitandloss" or "profitandlossstatement" or "pl" => "ProfitAndLoss",
            "cashflow" or "cashflowstatement" => "CashFlow",
            _ => "BalanceSheet",
        };
    }

    private static string ResolveFctrLabel() => "Foreign Currency Translation Reserve";

    private static decimal TranslateAmount(
        decimal rawAmount,
        string sourceCurrency,
        string targetCurrency,
        DateTime rateDate,
        DateTime closingDate,
        bool useTransactionRate,
        List<CurrencyExchange> exchanges,
        out decimal reserveDelta)
    {
        if (string.Equals(sourceCurrency, targetCurrency, StringComparison.OrdinalIgnoreCase))
        {
            reserveDelta = 0m;
            return rawAmount;
        }

        var closingRate = ResolveConsolidationExchangeRate(sourceCurrency, targetCurrency, closingDate, exchanges);
        var closingConverted = rawAmount * closingRate;
        if (!useTransactionRate)
        {
            reserveDelta = 0m;
            return closingConverted;
        }

        var txRate = ResolveConsolidationExchangeRate(sourceCurrency, targetCurrency, rateDate, exchanges);
        var txConverted = rawAmount * txRate;
        reserveDelta = closingConverted - txConverted;
        return txConverted;
    }

    private static bool UsesTransactionRate(AccountType accountType)
        => accountType == AccountType.Revenue || accountType == AccountType.Expense || accountType == AccountType.Equity;

    private static List<Guid> GetColumnTargetCompanies(
        Guid sourceCompanyId,
        ConsolidatedStatementContext ctx)
    {
        if (!ctx.AccumulatedInGroupCompany)
            return new List<Guid> { sourceCompanyId };

        return ctx.CompaniesInScope
            .Where(col => ctx.SubtreeIdsByCompany[col.Id].Contains(sourceCompanyId))
            .Select(col => col.Id)
            .ToList();
    }

    private static decimal ComputeTotalCell(
        ConsolidatedStatementContext ctx,
        Dictionary<Guid, decimal> rawCompanyValuesInColumnCurrency,
        Dictionary<Guid, decimal> rootValuesBySourceCompany)
    {
        if (ctx.AccumulatedInGroupCompany)
        {
            return Math.Round(rawCompanyValuesInColumnCurrency.GetValueOrDefault(ctx.RootCompanyId, 0m), 2);
        }

        return Math.Round(rootValuesBySourceCompany.Values.Sum(), 2);
    }

    private (List<ConsolidatedStatementRowDto> Rows, List<ConsolidatedStatementSummaryItemDto> Summary) BuildConsolidatedBalanceSheet(
        ConsolidatedStatementContext ctx)
    {
        var rows = new List<ConsolidatedStatementRowDto>();
        var reserveByColumn = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);
        var reserveInRootBySource = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);

        var bsSections = new[]
        {
            (AccountType.Asset, "Asset", true, "Total Assets"),
            (AccountType.Liability, "Liability", false, "Total Liabilities"),
            (AccountType.Equity, "Equity", false, "Total Equity"),
        };

        // Per ERPNext add_profit_and_loss_difference(): P&L historical-vs-closing translation delta affects BS reserve
        foreach (var je in ctx.AllJournals.Where(j => j.VoucherType != JournalEntryVoucherType.PeriodClosing && j.PostingDate >= ctx.FromDate))
        {
            if (!ctx.AllowedBooksByCompany.TryGetValue(je.CompanyId, out var allowedBooks)) continue;
            var sourceCompany = ctx.CompaniesById[je.CompanyId];

            foreach (var line in je.Lines)
            {
                if (!allowedBooks.Contains(line.FinanceBook ?? string.Empty)) continue;
                if (!ctx.AccountById.TryGetValue(line.AccountId, out var acc)) continue;
                if (acc.AccountType != AccountType.Revenue && acc.AccountType != AccountType.Expense) continue;

                // Equity/Credits signed as credit - debit for reserve contribution matching ERPNext debit - credit sign
                var creditMinusDebit = line.IsDebit ? -line.Amount : line.Amount;
                foreach (var colCompanyId in GetColumnTargetCompanies(sourceCompany.Id, ctx))
                {
                    TranslateAmount(
                        creditMinusDebit,
                        sourceCompany.CurrencyCode,
                        ctx.ColumnCurrencies[colCompanyId],
                        je.PostingDate,
                        ctx.ToDate,
                        useTransactionRate: true,
                        ctx.Exchanges,
                        out var delta);
                    reserveByColumn[colCompanyId] += delta;
                }

                TranslateAmount(
                    creditMinusDebit,
                    sourceCompany.CurrencyCode,
                    ctx.RootPresentationCurrency,
                    je.PostingDate,
                    ctx.ToDate,
                    useTransactionRate: true,
                    ctx.Exchanges,
                    out var rootDelta);
                reserveInRootBySource[sourceCompany.Id] += rootDelta;
            }
        }

        var sectionTotals = new Dictionary<string, Dictionary<Guid, decimal>>();
        var sectionRootTotals = new Dictionary<string, decimal>();

        foreach (var (accType, sectionName, debitNormal, totalLabel) in bsSections)
        {
            var leafColumnValues = new Dictionary<string, Dictionary<Guid, decimal>>(StringComparer.OrdinalIgnoreCase);
            var leafRootValues = new Dictionary<string, Dictionary<Guid, decimal>>(StringComparer.OrdinalIgnoreCase);

            foreach (var je in ctx.AllJournals)
            {
                if (!ctx.AllowedBooksByCompany.TryGetValue(je.CompanyId, out var allowedBooks)) continue;
                var sourceCompany = ctx.CompaniesById[je.CompanyId];

                foreach (var line in je.Lines)
                {
                    if (!allowedBooks.Contains(line.FinanceBook ?? string.Empty)) continue;
                    if (!ctx.AccountById.TryGetValue(line.AccountId, out var acc) || acc.AccountType != accType) continue;

                    var key = GetConsolidatedAccountKey(acc);
                    var rawAmount = debitNormal
                        ? (line.IsDebit ? line.Amount : -line.Amount)
                        : (line.IsDebit ? -line.Amount : line.Amount);

                    if (!leafColumnValues.TryGetValue(key, out var colDict))
                        leafColumnValues[key] = colDict = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);
                    if (!leafRootValues.TryGetValue(key, out var rootDict))
                        leafRootValues[key] = rootDict = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);

                    var useTxRate = UsesTransactionRate(accType);
                    foreach (var colId in GetColumnTargetCompanies(sourceCompany.Id, ctx))
                    {
                        var converted = TranslateAmount(
                            rawAmount,
                            sourceCompany.CurrencyCode,
                            ctx.ColumnCurrencies[colId],
                            je.PostingDate,
                            ctx.ToDate,
                            useTxRate,
                            ctx.Exchanges,
                            out var delta);
                        colDict[colId] += converted;
                        if (accType == AccountType.Equity)
                            reserveByColumn[colId] += delta;
                    }

                    var rootConverted = TranslateAmount(
                        rawAmount,
                        sourceCompany.CurrencyCode,
                        ctx.RootPresentationCurrency,
                        je.PostingDate,
                        ctx.ToDate,
                        useTxRate,
                        ctx.Exchanges,
                        out var rootDelta);
                    rootDict[sourceCompany.Id] += rootConverted;
                    if (accType == AccountType.Equity)
                        reserveInRootBySource[sourceCompany.Id] += rootDelta;
                }
            }

            // On Equity section, inject Foreign Currency Translation Reserve when FX reserve != 0
            ConsolidatedStatementRowDto? fctrRow = null;
            if (accType == AccountType.Equity &&
                (reserveByColumn.Values.Any(v => Math.Abs(v) >= 0.005m) ||
                 Math.Abs(reserveInRootBySource.Values.Sum()) >= 0.005m))
            {
                var fctrCompanyAmounts = ctx.CompaniesInScope.ToDictionary(
                    c => c.Id,
                    c => Math.Round(reserveByColumn[c.Id], 2));
                var fctrTotal = ctx.AccumulatedInGroupCompany
                    ? Math.Round(reserveByColumn[ctx.RootCompanyId], 2)
                    : Math.Round(reserveInRootBySource.Values.Sum(), 2);

                fctrRow = new ConsolidatedStatementRowDto
                {
                    AccountKey = "FCTR",
                    AccountCode = "FCTR",
                    AccountName = ResolveFctrLabel(),
                    Section = sectionName,
                    IsGroup = false,
                    IsTotalRow = false,
                    Level = 0,
                    CompanyAmounts = fctrCompanyAmounts,
                    Total = fctrTotal,
                };
            }

            var sectionRows = BuildConsolidatedSectionTree(ctx, accType, sectionName, leafColumnValues, leafRootValues);
            if (fctrRow != null)
            {
                sectionRows.Insert(0, fctrRow);
            }
            rows.AddRange(sectionRows);

            var colTotals = ctx.CompaniesInScope.ToDictionary(
                c => c.Id,
                c => Math.Round(
                    sectionRows.Where(r => r.Level == 0 && !r.IsTotalRow)
                               .Sum(r => r.CompanyAmounts.GetValueOrDefault(c.Id, 0m)), 2));
            var secRootTotal = Math.Round(
                sectionRows.Where(r => r.Level == 0 && !r.IsTotalRow).Sum(r => r.Total), 2);

            sectionTotals[sectionName] = colTotals;
            sectionRootTotals[sectionName] = secRootTotal;

            rows.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = $"TOTAL_{sectionName.ToUpperInvariant()}",
                AccountCode = string.Empty,
                AccountName = totalLabel,
                Section = sectionName,
                IsGroup = true,
                IsTotalRow = true,
                Level = 0,
                CompanyAmounts = colTotals,
                Total = secRootTotal,
            });
        }

        // Provisional Profit / Loss = Assets - Liabilities - Equity (including FCTR)
        var provAmounts = ctx.CompaniesInScope.ToDictionary(
            c => c.Id,
            c => Math.Round(
                sectionTotals["Asset"][c.Id] - sectionTotals["Liability"][c.Id] - sectionTotals["Equity"][c.Id], 2));
        var provTotal = Math.Round(
            sectionRootTotals["Asset"] - sectionRootTotals["Liability"] - sectionRootTotals["Equity"], 2);

        if (provAmounts.Values.Any(v => v != 0m) || provTotal != 0m || ctx.ShowZeroValues)
        {
            rows.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = "PROVISIONAL_PL",
                AccountCode = string.Empty,
                AccountName = "Provisional Profit / Loss",
                Section = "Equity",
                IsGroup = true,
                IsTotalRow = true,
                Level = 0,
                CompanyAmounts = provAmounts,
                Total = provTotal,
            });
        }

        var summary = new List<ConsolidatedStatementSummaryItemDto>
        {
            new() { Label = "Total Assets", Value = sectionRootTotals["Asset"], CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Total Liabilities", Value = sectionRootTotals["Liability"], CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Total Equity", Value = sectionRootTotals["Equity"], CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Provisional Profit / Loss", Value = provTotal, CurrencyCode = ctx.RootPresentationCurrency },
        };

        return (rows, summary);
    }

    private (List<ConsolidatedStatementRowDto> Rows, List<ConsolidatedStatementSummaryItemDto> Summary) BuildConsolidatedProfitLoss(
        ConsolidatedStatementContext ctx)
    {
        var rows = new List<ConsolidatedStatementRowDto>();
        var plSections = new[]
        {
            (AccountType.Revenue, "Revenue", false, "Total Income"),
            (AccountType.Expense, "Expense", true, "Total Expense"),
        };

        var sectionTotals = new Dictionary<string, Dictionary<Guid, decimal>>();
        var sectionRootTotals = new Dictionary<string, decimal>();

        foreach (var (accType, sectionName, debitNormal, totalLabel) in plSections)
        {
            var leafColumnValues = new Dictionary<string, Dictionary<Guid, decimal>>(StringComparer.OrdinalIgnoreCase);
            var leafRootValues = new Dictionary<string, Dictionary<Guid, decimal>>(StringComparer.OrdinalIgnoreCase);

            foreach (var je in ctx.AllJournals.Where(j =>
                         j.PostingDate >= ctx.FromDate && j.VoucherType != JournalEntryVoucherType.PeriodClosing))
            {
                if (!ctx.AllowedBooksByCompany.TryGetValue(je.CompanyId, out var allowedBooks)) continue;
                var sourceCompany = ctx.CompaniesById[je.CompanyId];

                foreach (var line in je.Lines)
                {
                    if (!allowedBooks.Contains(line.FinanceBook ?? string.Empty)) continue;
                    if (!ctx.AccountById.TryGetValue(line.AccountId, out var acc) || acc.AccountType != accType) continue;

                    var key = GetConsolidatedAccountKey(acc);
                    var rawAmount = debitNormal
                        ? (line.IsDebit ? line.Amount : -line.Amount)
                        : (line.IsDebit ? -line.Amount : line.Amount);

                    if (!leafColumnValues.TryGetValue(key, out var colDict))
                        leafColumnValues[key] = colDict = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);
                    if (!leafRootValues.TryGetValue(key, out var rootDict))
                        leafRootValues[key] = rootDict = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);

                    foreach (var colId in GetColumnTargetCompanies(sourceCompany.Id, ctx))
                    {
                        colDict[colId] += TranslateAmount(
                            rawAmount, sourceCompany.CurrencyCode, ctx.ColumnCurrencies[colId],
                            je.PostingDate, ctx.ToDate, useTransactionRate: true, ctx.Exchanges, out _);
                    }

                    rootDict[sourceCompany.Id] += TranslateAmount(
                        rawAmount, sourceCompany.CurrencyCode, ctx.RootPresentationCurrency,
                        je.PostingDate, ctx.ToDate, useTransactionRate: true, ctx.Exchanges, out _);
                }
            }

            var sectionRows = BuildConsolidatedSectionTree(ctx, accType, sectionName, leafColumnValues, leafRootValues);
            rows.AddRange(sectionRows);

            var colTotals = ctx.CompaniesInScope.ToDictionary(
                c => c.Id,
                c => Math.Round(
                    sectionRows.Where(r => r.Level == 0 && !r.IsTotalRow)
                               .Sum(r => r.CompanyAmounts.GetValueOrDefault(c.Id, 0m)), 2));
            var secRootTotal = Math.Round(
                sectionRows.Where(r => r.Level == 0 && !r.IsTotalRow).Sum(r => r.Total), 2);

            sectionTotals[sectionName] = colTotals;
            sectionRootTotals[sectionName] = secRootTotal;

            rows.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = $"TOTAL_{sectionName.ToUpperInvariant()}",
                AccountCode = string.Empty,
                AccountName = totalLabel,
                Section = sectionName,
                IsGroup = true,
                IsTotalRow = true,
                Level = 0,
                CompanyAmounts = colTotals,
                Total = secRootTotal,
            });
        }

        var netAmounts = ctx.CompaniesInScope.ToDictionary(
            c => c.Id,
            c => Math.Round(sectionTotals["Revenue"][c.Id] - sectionTotals["Expense"][c.Id], 2));
        var netTotal = Math.Round(sectionRootTotals["Revenue"] - sectionRootTotals["Expense"], 2);

        rows.Add(new ConsolidatedStatementRowDto
        {
            AccountKey = "NET_PROFIT",
            AccountCode = string.Empty,
            AccountName = "Net Profit / Loss",
            Section = "Summary",
            IsGroup = true,
            IsTotalRow = true,
            Level = 0,
            CompanyAmounts = netAmounts,
            Total = netTotal,
        });

        var summary = new List<ConsolidatedStatementSummaryItemDto>
        {
            new() { Label = "Total Income", Value = sectionRootTotals["Revenue"], CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Total Expense", Value = sectionRootTotals["Expense"], CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Net Profit / Loss", Value = netTotal, CurrencyCode = ctx.RootPresentationCurrency },
        };

        return (rows, summary);
    }

    private (List<ConsolidatedStatementRowDto> Rows, List<ConsolidatedStatementSummaryItemDto> Summary) BuildConsolidatedCashFlow(
        ConsolidatedStatementContext ctx)
    {
        var periodJournals = ctx.AllJournals
            .Where(j => j.PostingDate >= ctx.FromDate && j.VoucherType != JournalEntryVoucherType.PeriodClosing)
            .ToList();

        // Buckets keyed by (Section, LineKey, Label) -> CompanyId -> Amount & Root-currency Source amounts
        var buckets = new Dictionary<(string Section, string Label), (Dictionary<Guid, decimal> Cols, Dictionary<Guid, decimal> Roots)>();
        var reserveCols = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);
        var reserveRoots = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);

        void AddBucketAmount(string section, string label, Company sourceComp, decimal rawAmount, DateTime postingDate, bool useTxRate)
        {
            var bucketKey = (section, label);
            if (!buckets.TryGetValue(bucketKey, out var entry))
            {
                entry = (
                    ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m),
                    ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m));
                buckets[bucketKey] = entry;
            }

            foreach (var colId in GetColumnTargetCompanies(sourceComp.Id, ctx))
            {
                var converted = TranslateAmount(
                    rawAmount, sourceComp.CurrencyCode, ctx.ColumnCurrencies[colId],
                    postingDate, ctx.ToDate, useTxRate, ctx.Exchanges, out var delta);
                entry.Cols[colId] += converted;
                if (useTxRate) reserveCols[colId] += delta;
            }

            var rootConverted = TranslateAmount(
                rawAmount, sourceComp.CurrencyCode, ctx.RootPresentationCurrency,
                postingDate, ctx.ToDate, useTxRate, ctx.Exchanges, out var rDelta);
            entry.Roots[sourceComp.Id] += rootConverted;
            if (useTxRate) reserveRoots[sourceComp.Id] += rDelta;
        }

        foreach (var je in periodJournals)
        {
            if (!ctx.AllowedBooksByCompany.TryGetValue(je.CompanyId, out var allowedBooks)) continue;
            var sourceComp = ctx.CompaniesById[je.CompanyId];

            foreach (var line in je.Lines)
            {
                if (!allowedBooks.Contains(line.FinanceBook ?? string.Empty)) continue;
                if (!ctx.AccountById.TryGetValue(line.AccountId, out var acc) || acc.IsGroup) continue;

                var creditMinusDebit = line.IsDebit ? -line.Amount : line.Amount;
                var debitMinusCredit = line.IsDebit ? line.Amount : -line.Amount;

                if (acc.AccountType == AccountType.Revenue || acc.AccountType == AccountType.Expense)
                {
                    AddBucketAmount("Operating", "Net Profit", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: true);
                }
                else if (acc.AccountSubType == AccountSubType.CashAccount || acc.AccountSubType == AccountSubType.BankAccount)
                {
                    continue; // Cash movements are what the statement explains
                }
                else if (acc.AccountSubType == AccountSubType.AccumulatedDepreciation)
                {
                    AddBucketAmount("Operating", "Depreciation", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.AccountsReceivable)
                {
                    AddBucketAmount("Operating", "Change in Accounts Receivable", sourceComp, -debitMinusCredit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.Stock)
                {
                    AddBucketAmount("Operating", "Change in Inventory", sourceComp, -debitMinusCredit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.AccountsPayable)
                {
                    AddBucketAmount("Operating", "Change in Accounts Payable", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.TaxPayable)
                {
                    AddBucketAmount("Operating", "Change in Tax Payables", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.FixedAsset)
                {
                    AddBucketAmount("Investing", "Purchase of Fixed Assets", sourceComp, -debitMinusCredit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountSubType == AccountSubType.CapitalWorkInProgress)
                {
                    AddBucketAmount("Investing", "Capital Work in Progress", sourceComp, -debitMinusCredit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountType == AccountType.Equity)
                {
                    AddBucketAmount("Financing", "Change in Equity", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: true);
                }
                else if (acc.AccountSubType == AccountSubType.LongTermLiability)
                {
                    AddBucketAmount("Financing", "Net Borrowings / (Repayments)", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: false);
                }
                else if (acc.AccountType == AccountType.Asset || acc.AccountType == AccountType.Liability)
                {
                    AddBucketAmount("Operating", "Net Change in Other Balance Sheet Accounts", sourceComp, creditMinusDebit, je.PostingDate, useTxRate: false);
                }
            }
        }

        // Reconcile FX profit/equity transaction rate vs cash flow closing rate
        if (reserveCols.Values.Any(v => Math.Abs(v) >= 0.005m) || Math.Abs(reserveRoots.Values.Sum()) >= 0.005m)
        {
            buckets[("Operating", ResolveFctrLabel())] = (
                ctx.CompaniesInScope.ToDictionary(c => c.Id, c => reserveCols[c.Id]),
                ctx.CompaniesInScope.ToDictionary(c => c.Id, c => reserveRoots[c.Id]));
        }

        var rows = new List<ConsolidatedStatementRowDto>();
        var sectionTotals = new Dictionary<string, (Dictionary<Guid, decimal> Cols, decimal RootTotal)>();
        var cfSections = new[]
        {
            ("Operating", "Cash Flow from Operating Activities", "Net Cash from Operations"),
            ("Investing", "Cash Flow from Investing Activities", "Net Cash from Investing"),
            ("Financing", "Cash Flow from Financing Activities", "Net Cash from Financing"),
        };

        foreach (var (sec, headerLabel, totalLabel) in cfSections)
        {
            rows.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = $"CF_HDR_{sec.ToUpperInvariant()}",
                AccountCode = string.Empty,
                AccountName = headerLabel,
                Section = sec,
                IsGroup = true,
                IsTotalRow = false,
                Level = 0,
                CompanyAmounts = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m),
                Total = 0m,
            });

            var secColTotals = ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m);
            var secRootTotal = 0m;

            foreach (var kv in buckets.Where(k => k.Key.Section == sec))
            {
                var colAmounts = ctx.CompaniesInScope.ToDictionary(c => c.Id, c => Math.Round(kv.Value.Cols[c.Id], 2));
                var rowTotal = ComputeTotalCell(ctx, colAmounts, kv.Value.Roots);
                if (!ctx.ShowZeroValues && colAmounts.Values.All(v => v == 0m) && rowTotal == 0m)
                    continue;

                rows.Add(new ConsolidatedStatementRowDto
                {
                    AccountKey = $"CF_{sec}_{kv.Key.Label}",
                    AccountCode = string.Empty,
                    AccountName = kv.Key.Label,
                    Section = sec,
                    IsGroup = false,
                    IsTotalRow = false,
                    Level = 1,
                    CompanyAmounts = colAmounts,
                    Total = rowTotal,
                });

                foreach (var c in ctx.CompaniesInScope)
                    secColTotals[c.Id] = Math.Round(secColTotals[c.Id] + colAmounts[c.Id], 2);
                secRootTotal = Math.Round(secRootTotal + rowTotal, 2);
            }

            sectionTotals[sec] = (secColTotals, secRootTotal);
            rows.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = $"CF_TOTAL_{sec.ToUpperInvariant()}",
                AccountCode = string.Empty,
                AccountName = totalLabel,
                Section = sec,
                IsGroup = true,
                IsTotalRow = true,
                Level = 0,
                CompanyAmounts = secColTotals,
                Total = secRootTotal,
            });
        }

        var netCashCols = ctx.CompaniesInScope.ToDictionary(
            c => c.Id,
            c => Math.Round(
                sectionTotals["Operating"].Cols[c.Id]
                + sectionTotals["Investing"].Cols[c.Id]
                + sectionTotals["Financing"].Cols[c.Id], 2));
        var netCashTotal = Math.Round(
            sectionTotals["Operating"].RootTotal
            + sectionTotals["Investing"].RootTotal
            + sectionTotals["Financing"].RootTotal, 2);

        rows.Add(new ConsolidatedStatementRowDto
        {
            AccountKey = "NET_CHANGE_IN_CASH",
            AccountCode = string.Empty,
            AccountName = "Net Change in Cash",
            Section = "Summary",
            IsGroup = true,
            IsTotalRow = true,
            Level = 0,
            CompanyAmounts = netCashCols,
            Total = netCashTotal,
        });

        var summary = new List<ConsolidatedStatementSummaryItemDto>
        {
            new() { Label = "Net Cash from Operations", Value = sectionTotals["Operating"].RootTotal, CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Net Cash from Investing", Value = sectionTotals["Investing"].RootTotal, CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Net Cash from Financing", Value = sectionTotals["Financing"].RootTotal, CurrencyCode = ctx.RootPresentationCurrency },
            new() { Label = "Net Change in Cash", Value = netCashTotal, CurrencyCode = ctx.RootPresentationCurrency },
        };

        return (rows, summary);
    }

    private List<ConsolidatedStatementRowDto> BuildConsolidatedSectionTree(
        ConsolidatedStatementContext ctx,
        AccountType accType,
        string sectionName,
        Dictionary<string, Dictionary<Guid, decimal>> leafColumnValues,
        Dictionary<string, Dictionary<Guid, decimal>> leafRootValues)
    {
        var typedAccounts = ctx.AllAccounts.Where(a => a.AccountType == accType).ToList();
        if (typedAccounts.Count == 0)
            return new List<ConsolidatedStatementRowDto>();

        // Canonical node catalog merged across companies by AccountKey
        var nodeMeta = new Dictionary<string, (string Code, string Name, bool IsGroup, string? ParentKey)>(StringComparer.OrdinalIgnoreCase);
        foreach (var acc in typedAccounts.OrderBy(a => a.CompanyId == ctx.RootCompanyId ? 0 : 1).ThenBy(a => a.AccountCode))
        {
            var key = GetConsolidatedAccountKey(acc);
            string? parentKey = null;
            if (acc.ParentAccountId.HasValue && ctx.AccountById.TryGetValue(acc.ParentAccountId.Value, out var parentAcc) && parentAcc.AccountType == accType)
            {
                var candidateParent = GetConsolidatedAccountKey(parentAcc);
                if (!string.Equals(candidateParent, key, StringComparison.OrdinalIgnoreCase))
                    parentKey = candidateParent;
            }

            if (!nodeMeta.TryGetValue(key, out var existing))
            {
                nodeMeta[key] = (acc.AccountCode, acc.AccountName, acc.IsGroup, parentKey);
            }
            else if (existing.ParentKey == null && parentKey != null)
            {
                nodeMeta[key] = (existing.Code, existing.Name, existing.IsGroup || acc.IsGroup, parentKey);
            }
        }

        // Accumulate leaf amounts upward into parent keys per company column & per root-currency source company
        var nodeCols = nodeMeta.Keys.ToDictionary(
            k => k,
            _ => ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m),
            StringComparer.OrdinalIgnoreCase);
        var nodeRoots = nodeMeta.Keys.ToDictionary(
            k => k,
            _ => ctx.CompaniesInScope.ToDictionary(c => c.Id, _ => 0m),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (leafKey, colDict) in leafColumnValues)
        {
            var rootDict = leafRootValues.GetValueOrDefault(leafKey);
            var cursor = (string?)leafKey;
            var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (cursor != null && guard.Add(cursor) && nodeMeta.TryGetValue(cursor, out var meta))
            {
                foreach (var col in ctx.CompaniesInScope)
                    nodeCols[cursor][col.Id] += colDict.GetValueOrDefault(col.Id, 0m);

                if (rootDict != null)
                {
                    foreach (var src in ctx.CompaniesInScope)
                        nodeRoots[cursor][src.Id] += rootDict.GetValueOrDefault(src.Id, 0m);
                }

                cursor = meta.ParentKey;
            }
        }

        var childrenByParent = nodeMeta
            .Where(kv => kv.Value.ParentKey != null && nodeMeta.ContainsKey(kv.Value.ParentKey))
            .OrderBy(kv => kv.Value.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase)
            .ToLookup(kv => kv.Value.ParentKey!, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        var rootKeys = nodeMeta
            .Where(kv => kv.Value.ParentKey == null || !nodeMeta.ContainsKey(kv.Value.ParentKey))
            .OrderBy(kv => kv.Value.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(kv => kv.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(kv => kv.Key)
            .ToList();

        var ordered = new List<ConsolidatedStatementRowDto>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(string key, int level)
        {
            if (!visited.Add(key)) return;
            var meta = nodeMeta[key];
            var colAmounts = ctx.CompaniesInScope.ToDictionary(
                c => c.Id,
                c => Math.Round(nodeCols[key][c.Id], 2));
            var total = ComputeTotalCell(ctx, colAmounts, nodeRoots[key]);

            if (!ctx.ShowZeroValues && colAmounts.Values.All(v => Math.Abs(v) < 0.005m) && Math.Abs(total) < 0.005m)
                return;

            ordered.Add(new ConsolidatedStatementRowDto
            {
                AccountKey = key,
                AccountCode = meta.Code,
                AccountName = meta.Name,
                Section = sectionName,
                IsGroup = meta.IsGroup,
                IsTotalRow = false,
                Level = level,
                CompanyAmounts = colAmounts,
                Total = total,
            });

            foreach (var childKey in childrenByParent[key])
            {
                Visit(childKey, level + 1);
            }
        }

        foreach (var rk in rootKeys)
        {
            Visit(rk, 0);
        }

        return ordered;
    }

    private static string GetConsolidatedAccountKey(Account account)
        => !string.IsNullOrWhiteSpace(account.AccountCode)
            ? account.AccountCode.Trim().ToUpperInvariant()
            : account.AccountName.Trim().ToUpperInvariant();

    private sealed class ConsolidatedStatementContext
    {
        public ConsolidatedStatementContext(
            Guid rootCompanyId,
            string rootPresentationCurrency,
            Dictionary<Guid, string> columnCurrencies,
            List<Company> companiesInScope,
            Dictionary<Guid, HashSet<Guid>> subtreeIdsByCompany,
            List<Account> allAccounts,
            Dictionary<Guid, Account> accountById,
            List<JournalEntry> allJournals,
            Dictionary<Guid, HashSet<string>> allowedBooksByCompany,
            List<CurrencyExchange> exchanges,
            DateTime fromDate,
            DateTime toDate,
            bool accumulatedInGroupCompany,
            bool showZeroValues)
        {
            RootCompanyId = rootCompanyId;
            RootPresentationCurrency = rootPresentationCurrency;
            ColumnCurrencies = columnCurrencies;
            CompaniesInScope = companiesInScope;
            CompaniesById = companiesInScope.ToDictionary(c => c.Id);
            SubtreeIdsByCompany = subtreeIdsByCompany;
            AllAccounts = allAccounts;
            AccountById = accountById;
            AllJournals = allJournals;
            AllowedBooksByCompany = allowedBooksByCompany;
            Exchanges = exchanges;
            FromDate = fromDate;
            ToDate = toDate;
            AccumulatedInGroupCompany = accumulatedInGroupCompany;
            ShowZeroValues = showZeroValues;
        }

        public Guid RootCompanyId { get; }
        public string RootPresentationCurrency { get; }
        public Dictionary<Guid, string> ColumnCurrencies { get; }
        public List<Company> CompaniesInScope { get; }
        public Dictionary<Guid, Company> CompaniesById { get; }
        public Dictionary<Guid, HashSet<Guid>> SubtreeIdsByCompany { get; }
        public List<Account> AllAccounts { get; }
        public Dictionary<Guid, Account> AccountById { get; }
        public List<JournalEntry> AllJournals { get; }
        public Dictionary<Guid, HashSet<string>> AllowedBooksByCompany { get; }
        public List<CurrencyExchange> Exchanges { get; }
        public DateTime FromDate { get; }
        public DateTime ToDate { get; }
        public bool AccumulatedInGroupCompany { get; }
        public bool ShowZeroValues { get; }
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


