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
/// Budget Variance Report — compares budgeted amounts vs actual GL postings per account.
/// ERPNext equivalent: accounts/report/budget_variance_report/budget_variance_report.py
/// 
/// Shows per account:
/// - Budget amount (from submitted Budget documents)
/// - Actual amount (from posted GL entries in the period)
/// - Variance = Budget - Actual (positive = under budget, negative = over budget)
/// - Variance % = (Variance / Budget) × 100
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class BudgetVarianceReportAppService : ApplicationService
{
    private readonly IRepository<Budget, Guid> _budgetRepository;
    private readonly IRepository<Account, Guid> _accountRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly AccountBalanceService _balanceService;

    public BudgetVarianceReportAppService(
        IRepository<Budget, Guid> budgetRepository,
        IRepository<Account, Guid> accountRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        AccountBalanceService balanceService)
    {
        _budgetRepository = budgetRepository;
        _accountRepository = accountRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _balanceService = balanceService;
    }

    public async Task<BudgetVarianceReportDto> GetReportAsync(BudgetVarianceRequestDto input)
    {
        // Get submitted budgets for the fiscal year
        var budgets = await _budgetRepository.GetListAsync(b =>
            b.CompanyId == input.CompanyId &&
            b.FiscalYearId == input.FiscalYearId &&
            b.Status == DocumentStatus.Submitted);

        if (!budgets.Any())
            return new BudgetVarianceReportDto { CompanyId = input.CompanyId, Rows = new() };

        // Get fiscal year dates for period balance query
        var fiscalYear = await _fiscalYearRepository.GetAsync(input.FiscalYearId);

        // Use input date range or full FY
        var fromDate = input.FromDate ?? fiscalYear.StartDate;
        var toDate = input.ToDate ?? fiscalYear.EndDate;

        // Build budget map: accountId → total budget amount
        var budgetMap = new Dictionary<Guid, decimal>();
        foreach (var budget in budgets)
        {
            foreach (var account in budget.Accounts)
            {
                if (!budgetMap.ContainsKey(account.AccountId))
                    budgetMap[account.AccountId] = 0;
                budgetMap[account.AccountId] += account.BudgetAmount;
            }
        }

        // Get account details
        var accountIds = budgetMap.Keys.ToList();
        var accounts = await _accountRepository.GetListAsync(a => accountIds.Contains(a.Id));
        var accountLookup = accounts.ToDictionary(a => a.Id);

        // Get actual GL totals for the period per account
        var rows = new List<BudgetVarianceRowDto>();
        foreach (var (accountId, budgetAmount) in budgetMap.OrderBy(kvp => accountLookup.GetValueOrDefault(kvp.Key)?.AccountCode))
        {
            if (!accountLookup.TryGetValue(accountId, out var account))
                continue;

            var actualBalance = await _balanceService.GetPeriodBalanceAsync(
                input.CompanyId, accountId, fromDate, toDate);

            // For expense accounts: actual = debit - credit (expenses are debit-normal)
            // For revenue accounts: actual = credit - debit (revenue is credit-normal)
            decimal actual = account.AccountType == AccountType.Expense
                ? actualBalance.Debit - actualBalance.Credit
                : actualBalance.Credit - actualBalance.Debit;

            var variance = budgetAmount - actual;
            var variancePercent = budgetAmount != 0
                ? Math.Round((variance / budgetAmount) * 100, 2)
                : 0;

            rows.Add(new BudgetVarianceRowDto
            {
                AccountId = accountId,
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                BudgetAmount = budgetAmount,
                ActualAmount = actual,
                Variance = variance,
                VariancePercent = variancePercent,
                IsOverBudget = variance < 0
            });
        }

        return new BudgetVarianceReportDto
        {
            CompanyId = input.CompanyId,
            FiscalYearId = input.FiscalYearId,
            FromDate = fromDate,
            ToDate = toDate,
            Rows = rows,
            TotalBudget = rows.Sum(r => r.BudgetAmount),
            TotalActual = rows.Sum(r => r.ActualAmount),
            TotalVariance = rows.Sum(r => r.Variance),
            OverBudgetCount = rows.Count(r => r.IsOverBudget)
        };
    }
}

#region DTOs

public class BudgetVarianceRequestDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class BudgetVarianceReportDto
{
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<BudgetVarianceRowDto> Rows { get; set; } = new();
    public decimal TotalBudget { get; set; }
    public decimal TotalActual { get; set; }
    public decimal TotalVariance { get; set; }
    public int OverBudgetCount { get; set; }
}

public class BudgetVarianceRowDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = null!;
    public string AccountName { get; set; } = null!;
    public string AccountType { get; set; } = null!;
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercent { get; set; }
    public bool IsOverBudget { get; set; }
}

#endregion
