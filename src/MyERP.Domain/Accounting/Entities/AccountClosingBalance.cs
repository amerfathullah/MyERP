using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Account Closing Balance — pre-aggregated period-end account balance for fast reporting.
/// ERPNext equivalent: accounts/doctype/account_closing_balance/account_closing_balance.py
/// 
/// This entity caches the cumulative debit/credit totals for each account at a specific
/// closing date (typically month-end or period-end). This enables:
/// - O(1) Trial Balance queries (read closing balance instead of summing all JE lines)
/// - Fast Balance Sheet generation (opening balance = previous period's closing)
/// - Efficient comparative reporting (current vs prior period)
/// 
/// Rebuilt on:
/// - Period Closing Voucher submission
/// - GL Repost (when stock valuation changes retroactively)
/// - Manual rebuild trigger (admin tool)
/// 
/// Per ERPNext: one row per (account, company, cost_center, finance_book, closing_date).
/// Cost center dimension enables departmental P&L without scanning full GL.
/// </summary>
public class AccountClosingBalance : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>The date this balance is as-of (inclusive). Typically month-end or FY-end.</summary>
    public DateTime ClosingDate { get; set; }

    /// <summary>Period label for grouping (e.g., "2026-06", "2026-Q2", "FY2026").</summary>
    public string Period { get; set; } = null!;

    /// <summary>Cumulative debit total for this account up to and including ClosingDate.</summary>
    public decimal Debit { get; set; }

    /// <summary>Cumulative credit total for this account up to and including ClosingDate.</summary>
    public decimal Credit { get; set; }

    /// <summary>Net balance: Debit - Credit. Positive = debit balance, Negative = credit balance.</summary>
    public decimal Balance => Debit - Credit;

    /// <summary>Optional: cost center scope for departmental P&L closing balances.</summary>
    public Guid? CostCenterId { get; set; }

    /// <summary>Optional: finance book for multi-book closing balances.</summary>
    public string? FinanceBook { get; set; }

    /// <summary>True if this is a closing entry for P&L accounts (reset at FY start).</summary>
    public bool IsPeriodClosingEntry { get; set; }

    /// <summary>Reference to the Period Closing Voucher that generated this entry.</summary>
    public Guid? PeriodClosingVoucherId { get; set; }

    protected AccountClosingBalance() { }

    public AccountClosingBalance(Guid id, Guid companyId, Guid accountId,
        DateTime closingDate, string period, decimal debit, decimal credit,
        Guid? costCenterId = null, string? financeBook = null, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        AccountId = accountId;
        ClosingDate = closingDate;
        Period = period;
        Debit = debit;
        Credit = credit;
        CostCenterId = costCenterId;
        FinanceBook = financeBook;
        TenantId = tenantId;
    }

    /// <summary>Update the balance totals (used during rebuild).</summary>
    public void Update(decimal debit, decimal credit)
    {
        Debit = debit;
        Credit = credit;
    }
}
