using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Accounting.Entities;

/// <summary>
/// Configurable rule for auto-matching/categorizing bank transactions.
/// Rules are evaluated in priority order (ascending) — first match wins.
/// Supports description matching (contains/starts with/ends with/regex),
/// amount range filtering, and transaction type filtering.
/// 
/// Migrated from ERPNext: erpnext/accounts/doctype/bank_transaction_rule/bank_transaction_rule.py
/// </summary>
public class BankTransactionRule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Display name for the rule.</summary>
    public string RuleName { get; set; } = null!;

    /// <summary>Priority (lower = evaluated first). Auto-assigned sequentially per company.</summary>
    public int Priority { get; set; }

    /// <summary>Whether this rule is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Transaction type filter (Any/Withdrawal/Deposit).</summary>
    public BankTransactionType TransactionType { get; set; } = BankTransactionType.Any;

    /// <summary>Minimum transaction amount (null = no minimum).</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Maximum transaction amount (null = no maximum).</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>What to classify matching transactions as.</summary>
    public BankRuleClassifyAs ClassifyAs { get; set; } = BankRuleClassifyAs.BankEntry;

    /// <summary>Bank entry mode (Single/Multiple accounts). Only used when ClassifyAs = BankEntry.</summary>
    public BankEntryMode BankEntryMode { get; set; } = BankEntryMode.SingleAccount;

    /// <summary>Target account (for SingleAccount mode or PaymentEntry).</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Party type for Payment Entry matching (Customer/Supplier).</summary>
    public string? PartyType { get; set; }

    /// <summary>Party ID for Payment Entry matching.</summary>
    public Guid? PartyId { get; set; }

    /// <summary>Description matching conditions (OR logic — any match = rule matches).</summary>
    public ICollection<BankTransactionRuleCondition> Conditions { get; private set; }
        = new List<BankTransactionRuleCondition>();

    /// <summary>Multiple account rows (for MultipleAccounts mode). Last row = remainder.</summary>
    public ICollection<BankTransactionRuleAccount> Accounts { get; private set; }
        = new List<BankTransactionRuleAccount>();

    protected BankTransactionRule() { }

    public BankTransactionRule(Guid id, Guid companyId, string ruleName, int priority, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        RuleName = Check.NotNullOrWhiteSpace(ruleName, nameof(ruleName));
        Priority = priority;
        TenantId = tenantId;
    }

    /// <summary>
    /// Add a description-matching condition to this rule.
    /// Conditions use OR logic — any matching condition makes the rule eligible.
    /// </summary>
    public void AddCondition(BankRuleMatchType matchType, string value)
    {
        Check.NotNullOrWhiteSpace(value, nameof(value));

        // Validate regex patterns at configuration time
        if (matchType == BankRuleMatchType.Regex)
        {
            try { _ = new Regex(value); }
            catch (ArgumentException ex)
            {
                throw new BusinessException("MyERP:02013")
                    .WithData("pattern", value)
                    .WithData("error", ex.Message);
            }
        }

        Conditions.Add(new BankTransactionRuleCondition(Guid.NewGuid(), Id, matchType, value));
    }

    /// <summary>
    /// Add a target account for Multiple Accounts mode.
    /// </summary>
    public void AddAccount(Guid accountId, string? debitFormula, string? creditFormula)
    {
        Accounts.Add(new BankTransactionRuleAccount(
            Guid.NewGuid(), Id, accountId, debitFormula, creditFormula, Accounts.Count));
    }

    /// <summary>
    /// Validate rule configuration. Call before save.
    /// </summary>
    public void Validate()
    {
        if (MinAmount.HasValue && MaxAmount.HasValue && MinAmount > MaxAmount)
        {
            throw new BusinessException("MyERP:02014")
                .WithData("min", MinAmount)
                .WithData("max", MaxAmount);
        }

        if (ClassifyAs == BankRuleClassifyAs.PaymentEntry)
        {
            if (string.IsNullOrEmpty(PartyType) || !PartyId.HasValue || !AccountId.HasValue)
            {
                throw new BusinessException("MyERP:02015")
                    .WithData("rule", RuleName);
            }
        }

        if (ClassifyAs == BankRuleClassifyAs.BankEntry)
        {
            if (BankEntryMode == BankEntryMode.SingleAccount && !AccountId.HasValue)
            {
                throw new BusinessException("MyERP:02015")
                    .WithData("rule", RuleName);
            }

            if (BankEntryMode == BankEntryMode.MultipleAccounts)
            {
                if (!Accounts.Any())
                {
                    throw new BusinessException("MyERP:02015")
                        .WithData("rule", RuleName);
                }

                // Last row must NOT have formulas set (it's the remainder/balancing row)
                var lastAccount = Accounts.OrderBy(a => a.SortOrder).Last();
                if (!string.IsNullOrEmpty(lastAccount.DebitFormula) || !string.IsNullOrEmpty(lastAccount.CreditFormula))
                {
                    throw new BusinessException("MyERP:02016")
                        .WithData("rule", RuleName);
                }
            }
        }
    }

    /// <summary>
    /// Evaluate whether this rule matches a bank transaction.
    /// Returns true if transaction type, amount range, AND at least one description condition match.
    /// </summary>
    public bool Matches(BankTransaction transaction)
    {
        if (!IsEnabled) return false;

        // Transaction type filter
        var txAmount = Math.Abs(transaction.Amount);
        if (TransactionType == BankTransactionType.Withdrawal && transaction.Amount >= 0) return false;
        if (TransactionType == BankTransactionType.Deposit && transaction.Amount <= 0) return false;

        // Amount range filter
        if (MinAmount.HasValue && txAmount < MinAmount.Value) return false;
        if (MaxAmount.HasValue && txAmount > MaxAmount.Value) return false;

        // Description conditions (OR logic — any match = rule matches)
        if (!Conditions.Any()) return false; // No conditions = never matches

        var description = transaction.Description ?? string.Empty;
        return Conditions.Any(c => c.Matches(description));
    }
}

/// <summary>
/// A description-matching condition within a BankTransactionRule.
/// Uses OR logic with other conditions in the same rule.
/// </summary>
public class BankTransactionRuleCondition : Entity<Guid>
{
    public Guid BankTransactionRuleId { get; set; }
    public BankRuleMatchType MatchType { get; set; }
    public string Value { get; set; } = null!;

    protected BankTransactionRuleCondition() { }

    public BankTransactionRuleCondition(Guid id, Guid ruleId, BankRuleMatchType matchType, string value)
        : base(id)
    {
        BankTransactionRuleId = ruleId;
        MatchType = matchType;
        Value = value;
    }

    /// <summary>
    /// Evaluate whether this condition matches a transaction description.
    /// All comparisons are case-insensitive.
    /// </summary>
    public bool Matches(string description)
    {
        return MatchType switch
        {
            BankRuleMatchType.Contains => description.Contains(Value, StringComparison.OrdinalIgnoreCase),
            BankRuleMatchType.StartsWith => description.StartsWith(Value, StringComparison.OrdinalIgnoreCase),
            BankRuleMatchType.EndsWith => description.EndsWith(Value, StringComparison.OrdinalIgnoreCase),
            BankRuleMatchType.Regex => Regex.IsMatch(description, Value, RegexOptions.IgnoreCase),
            _ => false
        };
    }
}

/// <summary>
/// A target account row for Multiple Accounts mode in a BankTransactionRule.
/// The last row (highest SortOrder) must have empty formulas — it receives the remainder.
/// </summary>
public class BankTransactionRuleAccount : Entity<Guid>
{
    public Guid BankTransactionRuleId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Debit formula (uses `transaction_amount` variable). Null for remainder row.</summary>
    public string? DebitFormula { get; set; }

    /// <summary>Credit formula (uses `transaction_amount` variable). Null for remainder row.</summary>
    public string? CreditFormula { get; set; }

    /// <summary>Sort order within the rule (last row = remainder).</summary>
    public int SortOrder { get; set; }

    protected BankTransactionRuleAccount() { }

    public BankTransactionRuleAccount(Guid id, Guid ruleId, Guid accountId,
        string? debitFormula, string? creditFormula, int sortOrder)
        : base(id)
    {
        BankTransactionRuleId = ruleId;
        AccountId = accountId;
        DebitFormula = debitFormula;
        CreditFormula = creditFormula;
        SortOrder = sortOrder;
    }
}
