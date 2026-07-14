using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Evaluates bank transaction rules against unreconciled transactions.
/// Per ERPNext: rules are evaluated in priority order (ascending), first match wins.
/// Runs either on-demand or as a scheduled background job.
/// 
/// Source: erpnext/accounts/doctype/bank_transaction_rule/bank_transaction_rule.py
/// </summary>
public class BankTransactionRuleService : DomainService
{
    private readonly IRepository<BankTransactionRule, Guid> _ruleRepository;
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;

    public BankTransactionRuleService(
        IRepository<BankTransactionRule, Guid> ruleRepository,
        IRepository<BankTransaction, Guid> transactionRepository)
    {
        _ruleRepository = ruleRepository;
        _transactionRepository = transactionRepository;
    }

    /// <summary>
    /// Evaluate all active rules against unreconciled bank transactions for a company.
    /// First matching rule wins (priority order, ascending).
    /// Sets IsRuleEvaluated=true and MatchedTransactionRuleId on each transaction.
    /// </summary>
    /// <param name="companyId">Company to evaluate rules for</param>
    /// <param name="forceReEvaluate">If true, re-evaluates even previously-evaluated transactions</param>
    /// <returns>Result with count of matched and unmatched transactions</returns>
    public async Task<RuleEvaluationResult> EvaluateRulesAsync(Guid companyId, bool forceReEvaluate = false)
    {
        // Load rules ordered by priority ascending (first match wins)
        var ruleQuery = await _ruleRepository.GetQueryableAsync();
        var rules = ruleQuery
            .Where(r => r.CompanyId == companyId && r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToList();

        if (!rules.Any())
            return new RuleEvaluationResult();

        // Load unreconciled, unevaluated transactions
        var txQuery = await _transactionRepository.GetQueryableAsync();
        var transactions = txQuery
            .Where(t => t.CompanyId == companyId && !t.IsReconciled)
            .Where(t => forceReEvaluate || !t.IsRuleEvaluated)
            .ToList();

        if (!transactions.Any())
            return new RuleEvaluationResult();

        int matched = 0;
        int unmatched = 0;

        foreach (var tx in transactions)
        {
            var matchingRule = rules.FirstOrDefault(r => r.Matches(tx));

            tx.IsRuleEvaluated = true;
            tx.MatchedTransactionRuleId = matchingRule?.Id;

            if (matchingRule != null)
                matched++;
            else
                unmatched++;

            await _transactionRepository.UpdateAsync(tx);
        }

        return new RuleEvaluationResult
        {
            TransactionsEvaluated = matched + unmatched,
            Matched = matched,
            Unmatched = unmatched
        };
    }

    /// <summary>
    /// Evaluate a single transaction against rules. Used for real-time evaluation on import.
    /// </summary>
    public async Task<BankTransactionRule?> EvaluateSingleAsync(BankTransaction transaction)
    {
        var ruleQuery = await _ruleRepository.GetQueryableAsync();
        var rules = ruleQuery
            .Where(r => r.CompanyId == transaction.CompanyId && r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToList();

        var match = rules.FirstOrDefault(r => r.Matches(transaction));

        transaction.IsRuleEvaluated = true;
        transaction.MatchedTransactionRuleId = match?.Id;

        return match;
    }

    /// <summary>
    /// Get the next available priority for a new rule in a company.
    /// </summary>
    public async Task<int> GetNextPriorityAsync(Guid companyId)
    {
        var ruleQuery = await _ruleRepository.GetQueryableAsync();
        var maxPriority = ruleQuery
            .Where(r => r.CompanyId == companyId)
            .Select(r => (int?)r.Priority)
            .Max();

        return (maxPriority ?? 0) + 1;
    }

    /// <summary>
    /// Reorder priorities sequentially after a rule is deleted.
    /// Ensures no gaps in priority numbers (1, 2, 3, ...).
    /// </summary>
    public async Task ReorderPrioritiesAsync(Guid companyId)
    {
        var ruleQuery = await _ruleRepository.GetQueryableAsync();
        var rules = ruleQuery
            .Where(r => r.CompanyId == companyId)
            .OrderBy(r => r.Priority)
            .ToList();

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Priority != i + 1)
            {
                rules[i].Priority = i + 1;
                await _ruleRepository.UpdateAsync(rules[i]);
            }
        }
    }
}

/// <summary>
/// Result of evaluating bank transaction rules.
/// </summary>
public class RuleEvaluationResult
{
    public int TransactionsEvaluated { get; set; }
    public int Matched { get; set; }
    public int Unmatched { get; set; }
}
