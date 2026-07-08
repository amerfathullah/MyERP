using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Workflow.DomainServices;
using MyERP.Workflow.DTOs;
using MyERP.Workflow.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace MyERP.Workflow;

[Authorize]
public class ApprovalWorkflowAppService : ApplicationService, IApprovalWorkflowAppService
{
    private readonly IRepository<ApprovalRule, Guid> _ruleRepository;
    private readonly IRepository<ApprovalRequest, Guid> _requestRepository;
    private readonly ApprovalWorkflowManager _workflowManager;

    public ApprovalWorkflowAppService(
        IRepository<ApprovalRule, Guid> ruleRepository,
        IRepository<ApprovalRequest, Guid> requestRepository,
        ApprovalWorkflowManager workflowManager)
    {
        _ruleRepository = ruleRepository;
        _requestRepository = requestRepository;
        _workflowManager = workflowManager;
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Create)]
    public async Task<ApprovalRuleDto> CreateRuleAsync(CreateApprovalRuleDto input)
    {
        var rule = new ApprovalRule(
            GuidGenerator.Create(),
            input.DocumentType,
            input.Name,
            input.Level,
            CurrentTenant.Id)
        {
            ApproverRoleName = input.ApproverRoleName,
            ApproverUserId = input.ApproverUserId,
            ConditionExpression = input.ConditionExpression,
            MinimumAmount = input.MinimumAmount,
            CompanyId = input.CompanyId,
            IsActive = input.IsActive,
            Description = input.Description
        };

        await _ruleRepository.InsertAsync(rule);
        return MapRuleToDto(rule);
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Edit)]
    public async Task<ApprovalRuleDto> UpdateRuleAsync(Guid id, UpdateApprovalRuleDto input)
    {
        var rule = await _ruleRepository.GetAsync(id);
        rule.Name = input.Name;
        rule.Level = input.Level;
        rule.ApproverRoleName = input.ApproverRoleName;
        rule.ApproverUserId = input.ApproverUserId;
        rule.ConditionExpression = input.ConditionExpression;
        rule.MinimumAmount = input.MinimumAmount;
        rule.CompanyId = input.CompanyId;
        rule.IsActive = input.IsActive;
        rule.Description = input.Description;

        await _ruleRepository.UpdateAsync(rule);
        return MapRuleToDto(rule);
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Delete)]
    public async Task DeleteRuleAsync(Guid id)
    {
        await _ruleRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Default)]
    public async Task<PagedResultDto<ApprovalRuleDto>> GetRulesAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _ruleRepository.GetCountAsync();
        var rules = await _ruleRepository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "Level");

        return new PagedResultDto<ApprovalRuleDto>(
            totalCount,
            rules.Select(MapRuleToDto).ToList());
    }

    [Authorize(MyERPPermissions.ApprovalWorkflows.Default)]
    public async Task<ApprovalRuleDto> GetRuleAsync(Guid id)
    {
        var rule = await _ruleRepository.GetAsync(id);
        return MapRuleToDto(rule);
    }

    public async Task<ApprovalRequestDto> ApproveAsync(ReviewApprovalDto input)
    {
        await _workflowManager.ApproveAndAdvanceAsync(input.RequestId, CurrentUser.GetId(), input.Remarks);
        var request = await _requestRepository.GetAsync(input.RequestId);
        return MapRequestToDto(request);
    }

    public async Task<ApprovalRequestDto> RejectAsync(ReviewApprovalDto input)
    {
        var request = await _requestRepository.GetAsync(input.RequestId);
        request.Reject(CurrentUser.GetId(), input.Remarks);
        await _requestRepository.UpdateAsync(request);
        return MapRequestToDto(request);
    }

    public async Task<PagedResultDto<ApprovalRequestDto>> GetPendingApprovalsAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _requestRepository.GetQueryableAsync();
        var pending = query.Where(r => r.Status == ApprovalStatus.Pending);

        var totalCount = pending.Count();
        var items = pending
            .OrderByDescending(r => r.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<ApprovalRequestDto>(
            totalCount,
            items.Select(MapRequestToDto).ToList());
    }

    public async Task<PagedResultDto<ApprovalRequestDto>> GetDocumentApprovalsAsync(string documentType, Guid documentId)
    {
        var requests = await _requestRepository.GetListAsync(
            r => r.DocumentType == documentType && r.DocumentId == documentId);

        return new PagedResultDto<ApprovalRequestDto>(
            requests.Count,
            requests.Select(MapRequestToDto).ToList());
    }

    private ApprovalRequestDto MapRequestToDto(ApprovalRequest request)
    {
        return new ApprovalRequestDto
        {
            Id = request.Id,
            ApprovalRuleId = request.ApprovalRuleId,
            DocumentType = request.DocumentType,
            DocumentId = request.DocumentId,
            Level = request.Level,
            Status = request.Status,
            ReviewedByUserId = request.ReviewedByUserId,
            ReviewedAt = request.ReviewedAt,
            Remarks = request.Remarks,
            RequestedByUserId = request.RequestedByUserId,
            CreationTime = request.CreationTime
        };
    }

    private static ApprovalRuleDto MapRuleToDto(ApprovalRule rule)
    {
        return new ApprovalRuleDto
        {
            Id = rule.Id,
            DocumentType = rule.DocumentType,
            Name = rule.Name,
            Level = rule.Level,
            ApproverRoleName = rule.ApproverRoleName,
            ApproverUserId = rule.ApproverUserId,
            ConditionExpression = rule.ConditionExpression,
            MinimumAmount = rule.MinimumAmount,
            CompanyId = rule.CompanyId,
            IsActive = rule.IsActive,
            Description = rule.Description
        };
    }
}
