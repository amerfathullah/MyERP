using System;
using System.Threading.Tasks;
using MyERP.Automation.DTOs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Automation;

public interface IAutomationRuleAppService : IApplicationService
{
    Task<AutomationRuleDto> CreateAsync(CreateAutomationRuleDto input);
    Task<AutomationRuleDto> UpdateAsync(Guid id, UpdateAutomationRuleDto input);
    Task DeleteAsync(Guid id);
    Task<AutomationRuleDto> GetAsync(Guid id);
    Task<PagedResultDto<AutomationRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task<PagedResultDto<AutomationExecutionLogDto>> GetExecutionLogsAsync(Guid ruleId, PagedAndSortedResultRequestDto input);
    Task<AutomationRuleDto> ToggleActiveAsync(Guid id);
}
