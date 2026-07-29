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

[Authorize(MyERPPermissions.Accounts.Default)]
public class ReportingAppService : ApplicationService, IReportingAppService
{
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<JournalEntryLine, Guid> _journalLineRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly AccountBalanceService _balanceService;

    public ReportingAppService(
        IRepository<Account, Guid> accountRepository,
        IRepository<JournalEntryLine, Guid> journalLineRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        AccountBalanceService balanceService)
    {
        _accountRepository = accountRepository;
        _journalLineRepository = journalLineRepository;
        _journalEntryRepository = journalEntryRepository;
        _balanceService = balanceService;
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
                Debit = balance.Debit,
                Credit = balance.Credit,
                ClosingDebit = netBalance > 0 ? netBalance : 0,
                ClosingCredit = netBalance < 0 ? Math.Abs(netBalance) : 0,
            });
        }

        return new TrialBalanceReportDto
        {
            AsOfDate = input.AsOfDate,
            CompanyId = input.CompanyId,
            Rows = rows,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
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
}
