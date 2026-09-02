using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Validates transactions against submitted budgets.
/// Enforcement hierarchy: MR (Level 1) → PO (Level 2) → Actual GL (Level 3).
/// Per ERPNext: budget validation fires on document submit when budget action is "Stop".
/// </summary>
public class BudgetValidationService : DomainService
{
    private readonly IRepository<Budget, Guid> _budgetRepository;
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;

    public BudgetValidationService(
        IRepository<Budget, Guid> budgetRepository,
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository)
    {
        _budgetRepository = budgetRepository;
        _journalEntryRepository = journalEntryRepository;
        _fiscalYearRepository = fiscalYearRepository;
    }

    /// <summary>
    /// Validates a Purchase Order's amount against budget at Level 2.
    /// Checks each PO item's expense account against budget limits.
    /// </summary>
    public async Task ValidateForPurchaseOrderAsync(
        Guid companyId, Guid fiscalYearId, DateTime postingDate,
        IEnumerable<BudgetCheckItem> items, Guid? tenantId)
    {
        await ValidateAsync(companyId, fiscalYearId, postingDate, items,
            BudgetLevel.PurchaseOrder, tenantId);
    }

    /// <summary>
    /// Validates a Material Request's amount against budget at Level 1.
    /// </summary>
    public async Task ValidateForMaterialRequestAsync(
        Guid companyId, Guid fiscalYearId, DateTime postingDate,
        IEnumerable<BudgetCheckItem> items, Guid? tenantId)
    {
        await ValidateAsync(companyId, fiscalYearId, postingDate, items,
            BudgetLevel.MaterialRequest, tenantId);
    }

    /// <summary>
    /// Validates actual GL posting against budget at Level 3.
    /// </summary>
    public async Task ValidateForActualExpenseAsync(
        Guid companyId, Guid fiscalYearId, DateTime postingDate,
        IEnumerable<BudgetCheckItem> items, Guid? tenantId)
    {
        await ValidateAsync(companyId, fiscalYearId, postingDate, items,
            BudgetLevel.Actual, tenantId);
    }

    private async Task ValidateAsync(
        Guid companyId, Guid fiscalYearId, DateTime postingDate,
        IEnumerable<BudgetCheckItem> items, BudgetLevel level, Guid? tenantId)
    {
        // Get all submitted budgets for this company and fiscal year
        var budgetQueryable = await _budgetRepository.GetQueryableAsync();
        var budgets = budgetQueryable
            .Where(b => b.CompanyId == companyId
                     && b.FiscalYearId == fiscalYearId
                     && b.Status == Core.DocumentStatus.Submitted)
            .ToList();

        if (!budgets.Any()) return;

        // Get actual GL spend for comparison
        var actualSpend = await GetActualSpendAsync(companyId, fiscalYearId, tenantId);

        foreach (var item in items)
        {
            // Per ERPNext commit fa34ebea94: skip budget validation when cancelling GL entries or reversing spend
            if (item.Amount <= 0) continue;

            foreach (var budget in budgets)
            {
                var budgetAccount = budget.Accounts
                    .FirstOrDefault(a => a.AccountId == item.AccountId);

                if (budgetAccount == null) continue;

                var action = GetActionForLevel(budget, level);
                if (action == BudgetAction.Ignore) continue;

                // Calculate total spend: actual + this transaction
                var currentSpend = actualSpend.GetValueOrDefault(item.AccountId, 0m);
                var totalAfter = currentSpend + item.Amount;

                if (totalAfter > budgetAccount.BudgetAmount)
                {
                    if (action == BudgetAction.Stop)
                    {
                        throw new BusinessException(MyERPDomainErrorCodes.BudgetExceeded)
                            .WithData("account", budgetAccount.AccountName ?? item.AccountId.ToString())
                            .WithData("budget", budgetAccount.BudgetAmount)
                            .WithData("spent", currentSpend)
                            .WithData("requested", item.Amount);
                    }
                    // Warn action: logged but not blocking (ABP audit log captures it)
                }
            }
        }
    }

    private async Task<Dictionary<Guid, decimal>> GetActualSpendAsync(
        Guid companyId, Guid fiscalYearId, Guid? tenantId)
    {
        var fiscalYear = await _fiscalYearRepository.FindAsync(fiscalYearId);
        var result = new Dictionary<Guid, decimal>();
        if (fiscalYear == null) return result;

        // Query posted JEs within the fiscal year's date range to get actual spend per account
        // (per ERPNext budget.get_actual_expense: GL entries are scoped to budget_start_date..budget_end_date)
        var jeQueryable = await _journalEntryRepository.GetQueryableAsync();
        var postedEntries = jeQueryable
            .Where(je => je.CompanyId == companyId
                      && je.Status == Core.DocumentStatus.Posted
                      && je.PostingDate >= fiscalYear.StartDate
                      && je.PostingDate <= fiscalYear.EndDate);

        foreach (var je in postedEntries)
        {
            foreach (var line in je.Lines)
            {
                // Debit = expense (add to spend)
                if (line.IsDebit && line.Amount > 0 && line.AccountId != Guid.Empty)
                {
                    if (!result.ContainsKey(line.AccountId))
                        result[line.AccountId] = 0;
                    result[line.AccountId] += line.Amount;
                }
            }
        }

        return result;
    }

    private static BudgetAction GetActionForLevel(Budget budget, BudgetLevel level)
    {
        return level switch
        {
            BudgetLevel.MaterialRequest => budget.ActionIfAnnualBudgetExceededOnMr,
            BudgetLevel.PurchaseOrder => budget.ActionIfAnnualBudgetExceededOnPo,
            BudgetLevel.Actual => budget.ActionIfAnnualBudgetExceeded,
            _ => BudgetAction.Ignore
        };
    }
}

public enum BudgetLevel
{
    MaterialRequest = 1,
    PurchaseOrder = 2,
    Actual = 3
}

/// <summary>
/// Input model for budget validation: maps a transaction line to its expense account.
/// </summary>
public class BudgetCheckItem
{
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }

    public BudgetCheckItem(Guid accountId, decimal amount)
    {
        AccountId = accountId;
        Amount = amount;
    }
}
