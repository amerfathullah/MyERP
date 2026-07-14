using System;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class EvaluateRulesDto
{
    public Guid CompanyId { get; set; }
    public bool ForceReEvaluate { get; set; }
}

/// <summary>
/// AppService for Bank Transaction Rules — configurable auto-match rules for bank reconciliation.
/// Delegates to BankTransactionRuleService for priority-based rule evaluation.
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankTransactionRuleAppService : ApplicationService
{
    private readonly BankTransactionRuleService _service;

    public BankTransactionRuleAppService(BankTransactionRuleService service)
    {
        _service = service;
    }

    /// <summary>
    /// Evaluate all rules against unmatched bank transactions for a company.
    /// </summary>
    public async Task<AutoMatchResultDto> EvaluateRulesAsync(EvaluateRulesDto input)
    {
        var result = await _service.EvaluateRulesAsync(input.CompanyId, input.ForceReEvaluate);
        return new AutoMatchResultDto
        {
            MatchedCount = result.Matched,
            UnmatchedCount = result.Unmatched,
        };
    }

    /// <summary>
    /// Get the next available priority number for a new rule.
    /// </summary>
    public async Task<int> GetNextPriorityAsync(Guid companyId)
    {
        return await _service.GetNextPriorityAsync(companyId);
    }

    /// <summary>
    /// Reorder rule priorities to close gaps after deletion.
    /// </summary>
    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task ReorderPrioritiesAsync(Guid companyId)
    {
        await _service.ReorderPrioritiesAsync(companyId);
    }
}
