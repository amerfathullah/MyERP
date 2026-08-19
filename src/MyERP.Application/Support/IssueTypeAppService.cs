using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Support.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

[Authorize(MyERPPermissions.IssueTypes.Default)]
public class IssueTypeAppService : ApplicationService, IIssueTypeAppService
{
    private readonly IRepository<IssueType, Guid> _repository;

    public IssueTypeAppService(IRepository<IssueType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<IssueTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<IssueType, IssueTypeDto>(entity);
    }

    public async Task<PagedResultDto<IssueTypeDto>> GetListAsync(GetIssueTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(t => t.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<IssueTypeDto>(totalCount,
            items.Select(ObjectMapper.Map<IssueType, IssueTypeDto>).ToList());
    }

    [Authorize(MyERPPermissions.IssueTypes.Create)]
    public async Task<IssueTypeDto> CreateAsync(CreateUpdateIssueTypeDto input)
    {
        var entity = new IssueType(GuidGenerator.Create(), input.Name, CurrentTenant.Id)
        {
            Description = input.Description,
        };
        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "IssueType", entity.Id,
            "Created", Guid.Empty,
            entity.Name, "Draft", "Active",
            CurrentUser.Id,
            $"Issue type '{entity.Name}' created", CurrentTenant.Id));

        return ObjectMapper.Map<IssueType, IssueTypeDto>(entity);
    }

    [Authorize(MyERPPermissions.IssueTypes.Edit)]
    public async Task<IssueTypeDto> UpdateAsync(Guid id, CreateUpdateIssueTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.Description = input.Description;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "IssueType", entity.Id,
            "Updated", Guid.Empty,
            entity.Name, "Active", "Active",
            CurrentUser.Id,
            $"Issue type '{entity.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<IssueType, IssueTypeDto>(entity);
    }

    [Authorize(MyERPPermissions.IssueTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
