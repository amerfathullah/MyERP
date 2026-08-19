using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Accounting.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that runs rule evaluation for unreconciled bank transactions.
/// Per ERPNext: bank_transaction_rule.scheduler_run_rule_evaluation (scheduled job).
/// Evaluates rules in ascending priority order (first matching rule wins).
/// </summary>
public class BankTransactionAutoMatchJob : AsyncBackgroundJob<BankTransactionAutoMatchJobArgs>, ITransientDependency
{
    private readonly IRepository<BankTransaction, Guid> _transactionRepository;
    private readonly IRepository<BankTransactionRule, Guid> _ruleRepository;
    private readonly ILogger<BankTransactionAutoMatchJob> _logger;

    public BankTransactionAutoMatchJob(
        IRepository<BankTransaction, Guid> transactionRepository,
        IRepository<BankTransactionRule, Guid> ruleRepository,
        ILogger<BankTransactionAutoMatchJob> logger)
    {
        _transactionRepository = transactionRepository;
        _ruleRepository = ruleRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(BankTransactionAutoMatchJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("BankTransactionAutoMatchJob: Evaluating bank transaction rules for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var ruleQuery = await _ruleRepository.WithDetailsAsync(r => r.Conditions, r => r.Accounts);
        var activeRules = ruleQuery
            .Where(r => r.CompanyId == args.CompanyId && r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ToList();

        if (!activeRules.Any())
        {
            _logger.LogInformation("BankTransactionAutoMatchJob: No active rules configured for company {CompanyId}", args.CompanyId);
            return;
        }

        var txQuery = await _transactionRepository.GetQueryableAsync();
        var unreconciledTxs = txQuery
            .Where(t => t.CompanyId == args.CompanyId && !t.IsReconciled && !t.IsRuleEvaluated)
            .ToList();

        var matchedCount = 0;
        foreach (var tx in unreconciledTxs)
        {
            foreach (var rule in activeRules)
            {
                if (rule.Matches(tx))
                {
                    tx.IsRuleEvaluated = true;
                    tx.MatchedTransactionRuleId = rule.Id;
                    await _transactionRepository.UpdateAsync(tx);
                    matchedCount++;
                    break;
                }
            }
        }

        _logger.LogInformation("BankTransactionAutoMatchJob: Matched {Count} of {Total} unreconciled bank transactions for company {CompanyId}",
            matchedCount, unreconciledTxs.Count, args.CompanyId);
    }
}

public class BankTransactionAutoMatchJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
