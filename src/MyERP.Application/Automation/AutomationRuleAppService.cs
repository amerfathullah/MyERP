using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Automation.DTOs;
using MyERP.Automation.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Automation;

[Authorize(MyERPPermissions.AutomationRules.Default)]
public class AutomationRuleAppService : ApplicationService, IAutomationRuleAppService
{
    private readonly IRepository<AutomationRule, Guid> _ruleRepository;
    private readonly IRepository<AutomationExecutionLog, Guid> _logRepository;

    public AutomationRuleAppService(
        IRepository<AutomationRule, Guid> ruleRepository,
        IRepository<AutomationExecutionLog, Guid> logRepository)
    {
        _ruleRepository = ruleRepository;
        _logRepository = logRepository;
    }

    [Authorize(MyERPPermissions.AutomationRules.Create)]
    public async Task<AutomationRuleDto> CreateAsync(CreateAutomationRuleDto input)
    {
        var rule = new AutomationRule(
            GuidGenerator.Create(), input.Name, input.Trigger, input.Action, CurrentTenant.Id)
        {
            Description = input.Description,
            DocumentType = input.DocumentType,
            ConditionExpression = input.ConditionExpression,
            ActionConfig = input.ActionConfig,
            CompanyId = input.CompanyId,
            IsActive = input.IsActive,
            Priority = input.Priority,
        };

        await _ruleRepository.InsertAsync(rule);
        return MapToDto(rule);
    }

    [Authorize(MyERPPermissions.AutomationRules.Edit)]
    public async Task<AutomationRuleDto> UpdateAsync(Guid id, UpdateAutomationRuleDto input)
    {
        var rule = await _ruleRepository.GetAsync(id);
        rule.Name = input.Name;
        rule.Description = input.Description;
        rule.ConditionExpression = input.ConditionExpression;
        rule.Action = input.Action;
        rule.ActionConfig = input.ActionConfig;
        rule.CompanyId = input.CompanyId;
        rule.IsActive = input.IsActive;
        rule.Priority = input.Priority;

        await _ruleRepository.UpdateAsync(rule);
        return MapToDto(rule);
    }

    [Authorize(MyERPPermissions.AutomationRules.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _ruleRepository.DeleteAsync(id);
    }

    public async Task<AutomationRuleDto> GetAsync(Guid id)
    {
        var rule = await _ruleRepository.GetAsync(id);
        return MapToDto(rule);
    }

    public async Task<PagedResultDto<AutomationRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _ruleRepository.GetQueryableAsync();
        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var rules = await AsyncExecuter.ToListAsync(
            queryable.OrderBy(r => r.Priority).ThenBy(r => r.Name)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AutomationRuleDto>(
            totalCount,
            rules.Select(MapToDto).ToList());
    }

    public async Task<PagedResultDto<AutomationExecutionLogDto>> GetExecutionLogsAsync(
        Guid ruleId, PagedAndSortedResultRequestDto input)
    {
        var query = await _logRepository.GetQueryableAsync();
        var filtered = query.Where(l => l.AutomationRuleId == ruleId);

        var totalCount = filtered.Count();
        var logs = filtered
            .OrderByDescending(l => l.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<AutomationExecutionLogDto>(
            totalCount,
            logs.Select(l => new AutomationExecutionLogDto
            {
                Id = l.Id,
                AutomationRuleId = l.AutomationRuleId,
                SourceDocumentId = l.SourceDocumentId,
                SourceDocumentType = l.SourceDocumentType,
                IsSuccess = l.IsSuccess,
                ErrorMessage = l.ErrorMessage,
                ExecutionDurationMs = l.ExecutionDurationMs,
                CreationTime = l.CreationTime,
            }).ToList());
    }

    public async Task<AutomationRuleDto> ToggleActiveAsync(Guid id)
    {
        var rule = await _ruleRepository.GetAsync(id);
        rule.IsActive = !rule.IsActive;
        await _ruleRepository.UpdateAsync(rule);
        return MapToDto(rule);
    }

    private static AutomationRuleDto MapToDto(AutomationRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Description = rule.Description,
        Trigger = rule.Trigger,
        DocumentType = rule.DocumentType,
        ConditionExpression = rule.ConditionExpression,
        Action = rule.Action,
        ActionConfig = rule.ActionConfig,
        CompanyId = rule.CompanyId,
        IsActive = rule.IsActive,
        Priority = rule.Priority,
    };
}
