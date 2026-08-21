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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Issue", issue.Id,
            "Created", issue.CompanyId,
            issue.Subject, "Draft", issue.Status.ToString(), CurrentUser.Id,
            $"Issue '{issue.Subject}' created with priority {issue.Priority}", CurrentTenant.Id));

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

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Issue", issue.Id,
            "Replied", issue.CompanyId,
            issue.Subject, issue.Status.ToString(), issue.Status.ToString(), CurrentUser.Id,
            $"Reply added to issue '{issue.Subject}'", CurrentTenant.Id));

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

    [Authorize(MyERPPermissions.Issues.Create)]
    public async Task<IssueDto> SplitAsync(Guid id, SplitIssueDto input)
    {
        var originalIssue = await _issueRepository.GetAsync(id);

        var splitIssue = new Issue(Guid.NewGuid(), originalIssue.CompanyId, input.Subject, originalIssue.TenantId)
        {
            Description = originalIssue.Description,
            Priority = originalIssue.Priority,
            IssueType = originalIssue.IssueType,
            CustomerId = originalIssue.CustomerId,
            ContactId = originalIssue.ContactId,
            AssignedToId = originalIssue.AssignedToId,
            RaisedVia = originalIssue.RaisedVia,
            SplitFromIssueId = originalIssue.Id
        };

        var sla = await FindApplicableSlaAsync(splitIssue.CompanyId, splitIssue.CustomerId);
        if (sla != null)
        {
            var (responseHours, resolutionHours) = sla.GetTargets(splitIssue.Priority);
            splitIssue.ApplySla(sla.Id, responseHours, resolutionHours);
        }

        await _issueRepository.InsertAsync(splitIssue);

        var activityLogRepo = LazyServiceProvider?.LazyGetService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        if (activityLogRepo != null)
        {
            await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
                Guid.NewGuid(), "Issue", splitIssue.Id,
                "Created", splitIssue.CompanyId,
                splitIssue.Subject, "Draft", splitIssue.Status.ToString(), CurrentUser?.Id,
                $"Issue '{splitIssue.Subject}' split from '{originalIssue.Subject}'", CurrentTenant?.Id));
        }

        return new IssueDto
        {
            Id = splitIssue.Id,
            CompanyId = splitIssue.CompanyId,
            Subject = splitIssue.Subject,
            Description = splitIssue.Description,
            Priority = splitIssue.Priority,
            IssueType = splitIssue.IssueType,
            CustomerId = splitIssue.CustomerId,
            AssignedToId = splitIssue.AssignedToId,
            RaisedVia = splitIssue.RaisedVia,
            OpeningDate = splitIssue.OpeningDate,
            Status = splitIssue.Status,
            ServiceLevelAgreementId = splitIssue.ServiceLevelAgreementId,
            FirstResponseTime = splitIssue.FirstResponseTime,
            ResolutionTime = splitIssue.ResolutionTime,
            AgreementStatus = splitIssue.AgreementStatus,
            SplitFromIssueId = splitIssue.SplitFromIssueId
        };
    }
}

