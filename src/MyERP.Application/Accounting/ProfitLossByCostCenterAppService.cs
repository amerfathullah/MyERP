using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Profit & Loss by Cost Center — departmental P&L report.
/// Groups income and expense GL entries by cost center for management reporting.
/// Shows which departments/projects are profitable vs loss-making.
/// Per ERPNext: financial_statements.py with cost_center filter applied per-dimension.
/// NOTE: Cost Center filtering only applies to P&L accounts (not Balance Sheet).
/// Per DO-NOT: "Apply cost_center filter to Balance Sheet accounts in GL balance queries"
/// </summary>
[Authorize]
public class ProfitLossByCostCenterAppService : ApplicationService
{
    private readonly IRepository<JournalEntry, Guid> _jeRepository;
    private readonly IRepository<CostCenter, Guid> _costCenterRepository;
    private readonly IRepository<Account, Guid> _accountRepository;

    public ProfitLossByCostCenterAppService(
        IRepository<JournalEntry, Guid> jeRepository,
        IRepository<CostCenter, Guid> costCenterRepository,
        IRepository<Account, Guid> accountRepository)
    {
        _jeRepository = jeRepository;
        _costCenterRepository = costCenterRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// Generates P&L by cost center for a given company and period.
    /// Returns revenue, expense, and net profit per cost center.
    /// </summary>
    public async Task<ProfitLossByCostCenterDto> GetReportAsync(
        Guid companyId, DateTime fromDate, DateTime toDate)
    {
        var jeQuery = await _jeRepository.GetQueryableAsync();
        var ccQuery = await _costCenterRepository.GetQueryableAsync();
        var accountQuery = await _accountRepository.GetQueryableAsync();

        // Get all posted JEs in the period
        var journalEntries = jeQuery
            .Where(je => je.CompanyId == companyId
                && je.Status == Core.DocumentStatus.Posted
                && je.PostingDate >= fromDate && je.PostingDate <= toDate)
            .ToList();

        // Get all leaf cost centers for this company
        var costCenters = ccQuery
            .Where(cc => cc.CompanyId == companyId && !cc.IsGroup)
            .ToList();

        // Get all P&L accounts (revenue + expense)
        var accounts = accountQuery
            .Where(a => a.CompanyId == companyId &&
                (a.AccountType == AccountType.Revenue || a.AccountType == AccountType.Expense))
            .ToDictionary(a => a.Id, a => a.AccountType);

        // Aggregate GL lines by cost center
        var costCenterData = new Dictionary<Guid, (decimal revenue, decimal expense)>();
        decimal unallocatedRevenue = 0, unallocatedExpense = 0;

        foreach (var je in journalEntries)
        {
            foreach (var line in je.Lines)
            {
                if (!accounts.ContainsKey(line.AccountId))
                    continue; // Skip balance sheet accounts

                var accountType = accounts[line.AccountId];
                bool isRevenue = accountType == AccountType.Revenue;
                decimal amount = line.IsDebit ? line.Amount : -line.Amount;

                // For revenue accounts: credit = positive revenue
                // For expense accounts: debit = positive expense
                if (isRevenue)
                    amount = -amount; // invert for revenue (credit-normal)

                var ccId = line.CostCenterId;
                if (ccId.HasValue)
                {
                    if (!costCenterData.ContainsKey(ccId.Value))
                        costCenterData[ccId.Value] = (0, 0);

                    var existing = costCenterData[ccId.Value];
                    if (isRevenue)
                        costCenterData[ccId.Value] = (existing.revenue + Math.Abs(amount), existing.expense);
                    else
                        costCenterData[ccId.Value] = (existing.revenue, existing.expense + Math.Abs(amount));
                }
                else
                {
                    // No cost center assigned → unallocated
                    if (isRevenue)
                        unallocatedRevenue += Math.Abs(amount);
                    else
                        unallocatedExpense += Math.Abs(amount);
                }
            }
        }

        // Build result rows
        var rows = costCenters
            .Where(cc => costCenterData.ContainsKey(cc.Id))
            .Select(cc =>
            {
                var (revenue, expense) = costCenterData[cc.Id];
                return new CostCenterPLRowDto
                {
                    CostCenterId = cc.Id,
                    CostCenterName = cc.Name,
                    Revenue = revenue,
                    Expense = expense,
                    NetProfit = revenue - expense,
                    ProfitMargin = revenue > 0 ? Math.Round((revenue - expense) / revenue * 100, 1) : 0
                };
            })
            .OrderByDescending(r => r.NetProfit)
            .ToList();

        // Add unallocated if any
        if (unallocatedRevenue > 0 || unallocatedExpense > 0)
        {
            rows.Add(new CostCenterPLRowDto
            {
                CostCenterId = Guid.Empty,
                CostCenterName = "Unallocated",
                Revenue = unallocatedRevenue,
                Expense = unallocatedExpense,
                NetProfit = unallocatedRevenue - unallocatedExpense,
                ProfitMargin = unallocatedRevenue > 0
                    ? Math.Round((unallocatedRevenue - unallocatedExpense) / unallocatedRevenue * 100, 1) : 0
            });
        }

        var totalRevenue = rows.Sum(r => r.Revenue);
        var totalExpense = rows.Sum(r => r.Expense);

        return new ProfitLossByCostCenterDto
        {
            CompanyId = companyId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = totalRevenue,
            TotalExpense = totalExpense,
            NetProfit = totalRevenue - totalExpense,
            OverallMargin = totalRevenue > 0
                ? Math.Round((totalRevenue - totalExpense) / totalRevenue * 100, 1) : 0,
            CostCenters = rows
        };
    }
}

public class ProfitLossByCostCenterDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal OverallMargin { get; set; }
    public List<CostCenterPLRowDto> CostCenters { get; set; } = new();
}

public class CostCenterPLRowDto
{
    public Guid CostCenterId { get; set; }
    public string CostCenterName { get; set; } = null!;
    public decimal Revenue { get; set; }
    public decimal Expense { get; set; }
    public decimal NetProfit { get; set; }
    public decimal ProfitMargin { get; set; }
}
