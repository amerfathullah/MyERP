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
                    var dr = g.Where(l => l.IsDebit).Sum(l => l.Amount);
                    var cr = g.Where(l => !l.IsDebit).Sum(l => l.Amount);
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
            var (opDr, opCr) = openingByParty.GetValueOrDefault(pId, (0m, 0m));
            var (pDr, pCr) = periodByParty.GetValueOrDefault(pId, (0m, 0m));
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
}

