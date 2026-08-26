using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.EDI.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.EDI;

[Authorize(MyERPPermissions.CodeLists.Default)]
public class CodeListAppService : MyERPAppService, ICodeListAppService
{
    private readonly IRepository<CodeList, Guid> _repository;

    public CodeListAppService(IRepository<CodeList, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CodeListDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CodeListMapper().Map(entity);
    }

    public async Task<PagedResultDto<CodeListDto>> GetListAsync(GetCodeListListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Publisher))
        {
            var publisher = input.Publisher.Trim().ToLower();
            query = query.Where(x => x.Publisher != null && x.Publisher.ToLower().Contains(publisher));
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(filter) ||
                                     (x.CanonicalUri != null && x.CanonicalUri.ToLower().Contains(filter)) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Title)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new CodeListMapper().Map).ToList();
        return new PagedResultDto<CodeListDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.CodeLists.Create)]
    public async Task<CodeListDto> CreateAsync(CreateUpdateCodeListDto input)
    {
        var entity = new CodeList(
            GuidGenerator.Create(),
            input.Title.Trim(),
            input.CanonicalUri?.Trim(),
            input.Url?.Trim(),
            input.DefaultCommonCode?.Trim(),
            input.Version?.Trim(),
            input.Publisher?.Trim(),
            input.PublisherId?.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new CodeListMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CodeLists.Edit)]
    public async Task<CodeListDto> UpdateAsync(Guid id, CreateUpdateCodeListDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.Title = input.Title.Trim();
        entity.CanonicalUri = input.CanonicalUri?.Trim();
        entity.Url = input.Url?.Trim();
        entity.DefaultCommonCode = input.DefaultCommonCode?.Trim();
        entity.Version = input.Version?.Trim();
        entity.Publisher = input.Publisher?.Trim();
        entity.PublisherId = input.PublisherId?.Trim();
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new CodeListMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CodeLists.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
