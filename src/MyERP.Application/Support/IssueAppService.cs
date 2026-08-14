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

    public IssueAppService(IRepository<Issue, Guid> issueRepository)
    {
        _issueRepository = issueRepository;
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
        await _issueRepository.InsertAsync(issue);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
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
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> ReopenAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Reopen();
        await _issueRepository.UpdateAsync(issue);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }

    [Authorize(MyERPPermissions.Issues.Edit)]
    public async Task<IssueDto> HoldAsync(Guid id)
    {
        var issue = await _issueRepository.GetAsync(id);
        issue.Hold();
        await _issueRepository.UpdateAsync(issue);
        return ObjectMapper.Map<Issue, IssueDto>(issue);
    }
}

