using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

/// <summary>
/// Account Category management — groups accounts for financial reporting.
/// Per gotcha #158: 28 standard categories with root_type scoping.
/// </summary>
[Authorize(MyERPPermissions.Accounts.Default)]
public class AccountCategoryAppService : ApplicationService
{
    private readonly IRepository<AccountCategory, Guid> _repository;

    public AccountCategoryAppService(IRepository<AccountCategory, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<AccountCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        var totalCount = queryable.Count();
        var items = queryable
            .OrderBy(c => c.RootType).ThenBy(c => c.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<AccountCategoryDto>(totalCount,
            items.Select(c => new AccountCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                RootType = c.RootType,
                Description = c.Description,
            }).ToList());
    }

    public async Task<AccountCategoryDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new AccountCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            RootType = entity.RootType,
            Description = entity.Description,
        };
    }

    [Authorize(MyERPPermissions.Accounts.Create)]
    public async Task<AccountCategoryDto> CreateAsync(CreateAccountCategoryDto input)
    {
        var entity = new AccountCategory(GuidGenerator.Create(), input.Name, input.RootType)
        {
            Description = input.Description,
        };
        await _repository.InsertAsync(entity);
        return new AccountCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            RootType = entity.RootType,
            Description = entity.Description,
        };
    }

    [Authorize(MyERPPermissions.Accounts.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
