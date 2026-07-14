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
public class CashFlowStatementAppService : ApplicationService
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

        // Get period balances
        var periodBalances = await GetPeriodBalancesAsync(input.CompanyId, input.FromDate, input.ToDate, accounts);

        // === Operating Activities (Indirect Method) ===
        var operatingItems = new List<CashFlowLineItem>();

        // Net Profit = Revenue - Expenses for the period
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

        // Adjustments for non-cash items
        // Depreciation (add back — expense but not cash outflow)
        var depreciationAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccumulatedDepreciation);
        var depreciation = depreciationAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (depreciation != 0)
            operatingItems.Add(new CashFlowLineItem("Depreciation", depreciation));

        // Working capital changes
        // Increase in Receivables = cash used (negative)
        var receivableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccountsReceivable);
        var receivableChange = receivableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (receivableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Accounts Receivable", -receivableChange));

        // Increase in Inventory = cash used (negative)
        var inventoryAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.Stock);
        var inventoryChange = inventoryAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit);
        if (inventoryChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Inventory", -inventoryChange));

        // Increase in Payables = cash generated (positive)
        var payableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.AccountsPayable);
        var payableChange = payableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (payableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Accounts Payable", payableChange));

        // Tax payables change
        var taxPayableAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.TaxPayable);
        var taxPayableChange = taxPayableAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (taxPayableChange != 0)
            operatingItems.Add(new CashFlowLineItem("Change in Tax Payables", taxPayableChange));

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

        // Long-term loan changes
        var loanAccounts = accounts.Where(a => a.AccountSubType == AccountSubType.LongTermLiability);
        var loanChange = loanAccounts.Sum(a =>
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Credit -
            periodBalances.GetValueOrDefault(a.Id, (0, 0)).Debit);
        if (loanChange != 0)
            financingItems.Add(new CashFlowLineItem("Net Borrowings / (Repayments)", loanChange));

        var financingTotal = financingItems.Sum(i => i.Amount);

        // === Cash Balances ===
        var netCashChange = operatingTotal + investingTotal + financingTotal;

        // Opening cash = cash account balance at start of period
        decimal openingCash = 0;
        foreach (var cashAccount in cashAccounts)
        {
            var balance = await _balanceService.GetBalanceOnAsync(input.CompanyId, cashAccount.Id, input.FromDate.AddDays(-1));
            openingCash += balance.Balance;
        }

        var closingCash = openingCash + netCashChange;

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
            ClosingCashBalance = closingCash
        };
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

#region DTOs

public class CashFlowRequestDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class CashFlowStatementDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public List<CashFlowLineItem> OperatingActivities { get; set; } = new();
    public decimal OperatingTotal { get; set; }

    public List<CashFlowLineItem> InvestingActivities { get; set; } = new();
    public decimal InvestingTotal { get; set; }

    public List<CashFlowLineItem> FinancingActivities { get; set; } = new();
    public decimal FinancingTotal { get; set; }

    public decimal NetCashChange { get; set; }
    public decimal OpeningCashBalance { get; set; }
    public decimal ClosingCashBalance { get; set; }
}

public class CashFlowLineItem
{
    public string Label { get; set; }
    public decimal Amount { get; set; }

    public CashFlowLineItem(string label, decimal amount)
    {
        Label = label;
        Amount = amount;
    }
}

#endregion
