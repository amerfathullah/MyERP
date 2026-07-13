using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Budget account line — per-account budget amount within a Budget.
/// </summary>
public class BudgetAccount : FullAuditedEntity<Guid>
{
    public Guid BudgetId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal BudgetAmount { get; set; }

    protected BudgetAccount() { }

    public BudgetAccount(Guid id, Guid budgetId, Guid accountId,
        decimal budgetAmount, string? accountName = null)
        : base(id)
    {
        BudgetId = budgetId;
        AccountId = accountId;
        BudgetAmount = budgetAmount;
        AccountName = accountName;
    }
}
