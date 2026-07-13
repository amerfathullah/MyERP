using System;
using System.Collections.Generic;
using System.Linq;
using MyERP.Core;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Budget — tracks expense budget limits per account for a fiscal year.
/// Enforcement at 3 levels: MR → PO → Actual Expense (GL).
/// Maps to ERPNext accounts/doctype/budget.
/// </summary>
public class Budget : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FiscalYearId { get; set; }

    /// <summary>Budget dimension: cost_center, project, or accounting dimension.</summary>
    public string BudgetAgainst { get; set; } = null!;

    /// <summary>ID of the cost center, project, or dimension entity.</summary>
    public Guid BudgetAgainstId { get; set; }

    /// <summary>Display name of the budget target (e.g., "Marketing" cost center).</summary>
    public string? BudgetAgainstName { get; set; }

    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    // Level 3: Actual expense (booking) — always enabled
    public BudgetAction ActionIfAnnualBudgetExceeded { get; set; } = BudgetAction.Stop;
    public BudgetAction ActionIfAccumulatedMonthlyBudgetExceeded { get; set; } = BudgetAction.Warn;

    // Level 2: Purchase Order
    public BudgetAction ActionIfAnnualBudgetExceededOnPo { get; set; } = BudgetAction.Ignore;
    public BudgetAction ActionIfAccumulatedMonthlyBudgetExceededOnPo { get; set; } = BudgetAction.Ignore;

    // Level 1: Material Request (requires Level 2 + 3)
    public BudgetAction ActionIfAnnualBudgetExceededOnMr { get; set; } = BudgetAction.Ignore;
    public BudgetAction ActionIfAccumulatedMonthlyBudgetExceededOnMr { get; set; } = BudgetAction.Ignore;

    private readonly List<BudgetAccount> _accounts = new();
    public IReadOnlyList<BudgetAccount> Accounts => _accounts.AsReadOnly();

    protected Budget() { }

    public Budget(Guid id, Guid companyId, Guid fiscalYearId,
        string budgetAgainst, Guid budgetAgainstId, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        BudgetAgainst = budgetAgainst;
        BudgetAgainstId = budgetAgainstId;
        TenantId = tenantId;
    }

    public void AddAccount(Guid accountId, decimal budgetAmount, string? accountName = null)
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (budgetAmount <= 0)
            throw new ArgumentException("Budget amount must be positive.", nameof(budgetAmount));

        _accounts.Add(new BudgetAccount(Guid.NewGuid(), Id, accountId, budgetAmount, accountName));
    }

    public void Submit()
    {
        if (Status != DocumentStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        if (!_accounts.Any())
            throw new BusinessException(MyERPDomainErrorCodes.BudgetHasNoAccounts);

        ValidateEnforcementHierarchy();
        Status = DocumentStatus.Submitted;
    }

    public void Cancel()
    {
        if (Status != DocumentStatus.Submitted)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = DocumentStatus.Cancelled;
    }

    /// <summary>
    /// Level 1 requires Level 2+3 enabled. Level 2 requires Level 3 enabled.
    /// </summary>
    private void ValidateEnforcementHierarchy()
    {
        bool level1Active = ActionIfAnnualBudgetExceededOnMr != BudgetAction.Ignore
                         || ActionIfAccumulatedMonthlyBudgetExceededOnMr != BudgetAction.Ignore;
        bool level2Active = ActionIfAnnualBudgetExceededOnPo != BudgetAction.Ignore
                         || ActionIfAccumulatedMonthlyBudgetExceededOnPo != BudgetAction.Ignore;
        bool level3Active = ActionIfAnnualBudgetExceeded != BudgetAction.Ignore
                         || ActionIfAccumulatedMonthlyBudgetExceeded != BudgetAction.Ignore;

        if (level1Active && !level2Active)
            throw new BusinessException(MyERPDomainErrorCodes.BudgetLevel1RequiresLevel2);
        if (level2Active && !level3Active)
            throw new BusinessException(MyERPDomainErrorCodes.BudgetLevel2RequiresLevel3);
    }
}
