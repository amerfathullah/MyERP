using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Permissions;
using MyERP.Support.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

[Authorize(MyERPPermissions.Issues.Default)]
public class IssueAppService : ApplicationService, IIssueAppService
{
    private readonly IRepository<Issue, Guid> _issueRepository;
    private readonly IRepository<ServiceLevelAgreement, Guid> _slaRepository;

    public IssueAppService(IRepository<Issue, Guid> issueRepository, IRepository<ServiceLevelAgreement, Guid> slaRepository)
    {
        _issueRepository = issueRepository;
        _slaRepository = slaRepository;
    }

    public async Task<IssueDto> GetAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    public async Task<PagedResultDto<IssueDto>> GetListAsync(GetIssueListDto input)
    {
        var query = await _issueRepository.GetQueryableAsync();
        if (input.Status.HasValue)
            query = query.Where(i => i.Status == input.Status.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(i => i.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(i => i.Subject.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(i => i.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<IssueDto>(totalCount, items.Select(ObjectMapper.Map<Issue, IssueDto>).ToList());
    }

    [Authorize(MyERPPermissions.Issues.Create)]
    public async Task<IssueDto> CreateAsync(CreateIssueDto input)
    {
        var issue = new Issue(GuidGenerator.Create(), input.CompanyId, input.Subject, CurrentTenant.Id)
        {
            Description = input.Description,
            Priority = input.Priority ?? "Medium",
            IssueType = input.IssueType,
            CustomerId = input.CustomerId,
            RaisedVia = input.RaisedVia,
        };

        var sla = await FindApplicableSlaAsync(input.CompanyId, input.CustomerId);
        if (sla != null)
        {
            var (responseHours, resolutionHours) = sla.GetTargets(issue.Priority);
            issue.ApplySla(sla.Id, responseHours, resolutionHours);
        }

        await _issueRepository.InsertAsync(issue);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    /// <summary>
    /// Resolves the applicable SLA: a Customer-scoped agreement takes precedence over the company default.
    /// Mirrors ERPNext's get_active_service_level_agreement_for entity-priority lookup.
    /// </summary>
    private async Task<ServiceLevelAgreement?> FindApplicableSlaAsync(Guid companyId, Guid? customerId)
    {
        var query = (await _slaRepository.WithDetailsAsync())
            .Where(s => s.CompanyId == companyId && s.IsActive);

        if (customerId.HasValue)
        {
            var scoped = query.FirstOrDefault(s => s.EntityType == "Customer" && s.EntityId == customerId.Value);
            if (scoped != null) return scoped;
        }

        return query.FirstOrDefault(s => s.IsDefault);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> ReplyAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Reply();
        await _issueRepository.UpdateAsync(issue);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> ResolveAsync(Guid id, ResolveIssueDto input)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Resolve(input.Resolution);
        await _issueRepository.UpdateAsync(issue);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Issue", issue.Id,
            "Resolved", issue.CompanyId,
            issue.Subject, issue.Status.ToString(), "Resolved", CurrentUser.Id,
            $"Issue {issue.Subject} resolved. Resolution: {input.Resolution}", CurrentTenant.Id));

        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> ReopenAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Reopen();
        await _issueRepository.UpdateAsync(issue);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Issue", issue.Id,
            "Reopened", issue.CompanyId,
            issue.Subject, issue.Status.ToString(), "Open", CurrentUser.Id,
            $"Issue {issue.Subject} reopened", CurrentTenant.Id));

        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> HoldAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Hold();
        await _issueRepository.UpdateAsync(issue);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Issue", issue.Id,
            "OnHold", issue.CompanyId,
            issue.Subject, issue.Status.ToString(), "OnHold", CurrentUser.Id,
            $"Issue {issue.Subject} put on hold", CurrentTenant.Id));

        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }
}

