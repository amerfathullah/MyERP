using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

public class EvaluateRulesDto
{
    public Guid CompanyId { get; set; }
    public bool ForceReEvaluate { get; set; }
}

public class BankTransactionRuleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string RuleName { get; set; } = null!;
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public int TransactionType { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int ClassifyAs { get; set; }
    public string? DescriptionContains { get; set; }
}

public class CreateBankTransactionRuleDto
{
    public Guid CompanyId { get; set; }
    public string RuleName { get; set; } = null!;
    public int TransactionType { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public int ClassifyAs { get; set; }
    public string? DescriptionContains { get; set; }
}

/// <summary>
/// AppService for Bank Transaction Rules — configurable auto-match rules for bank reconciliation.
/// Delegates to BankTransactionRuleService for priority-based rule evaluation.
/// </summary>
[Authorize(MyERPPermissions.PaymentEntries.Default)]
public class BankTransactionRuleAppService : ApplicationService
{
    private readonly BankTransactionRuleService _service;
    private readonly IRepository<BankTransactionRule, Guid> _repository;

    public BankTransactionRuleAppService(
        BankTransactionRuleService service,
        IRepository<BankTransactionRule, Guid> repository)
    {
        _service = service;
        _repository = repository;
    }

    public async Task<PagedResultDto<BankTransactionRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _repository.GetQueryableAsync())
            .OrderBy(r => r.Priority);
        var totalCount = query.Count();
        var items = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<BankTransactionRuleDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.PaymentEntries.Create)]
    public async Task<BankTransactionRuleDto> CreateAsync(CreateBankTransactionRuleDto input)
    {
        var priority = await _service.GetNextPriorityAsync(input.CompanyId);
        var rule = new BankTransactionRule(
            GuidGenerator.Create(), input.CompanyId, input.RuleName, priority, CurrentTenant.Id)
        {
            TransactionType = (BankTransactionType)input.TransactionType,
            MinAmount = input.MinAmount,
            MaxAmount = input.MaxAmount,
            ClassifyAs = (BankRuleClassifyAs)input.ClassifyAs,
        };
        if (!string.IsNullOrWhiteSpace(input.DescriptionContains))
        {
            rule.AddCondition(BankRuleMatchType.Contains, input.DescriptionContains);
        }
        await _repository.InsertAsync(rule);
        return MapToDto(rule);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        rule.IsEnabled = false;
        await _repository.UpdateAsync(rule);
    }

    [Authorize(MyERPPermissions.PaymentEntries.Edit)]
    public async Task EnableAsync(Guid id)
    {
        var rule = await _repository.GetAsync(id);
        rule.IsEnabled = true;
        await _repository.UpdateAsync(rule);
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

    private static BankTransactionRuleDto MapToDto(BankTransactionRule r) => new()
    {
        Id = r.Id,
        CompanyId = r.CompanyId,
        RuleName = r.RuleName,
        Priority = r.Priority,
        IsEnabled = r.IsEnabled,
        TransactionType = (int)r.TransactionType,
        MinAmount = r.MinAmount,
        MaxAmount = r.MaxAmount,
        ClassifyAs = (int)r.ClassifyAs,
        DescriptionContains = r.Conditions.FirstOrDefault()?.Value,
    };
}
