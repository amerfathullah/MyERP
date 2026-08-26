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

[Authorize(MyERPPermissions.CommonCodes.Default)]
public class CommonCodeAppService : MyERPAppService, ICommonCodeAppService
{
    private readonly IRepository<CommonCode, Guid> _repository;

    public CommonCodeAppService(IRepository<CommonCode, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommonCodeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CommonCodeMapper().Map(entity);
    }

    public async Task<PagedResultDto<CommonCodeDto>> GetListAsync(GetCommonCodeListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CodeListId.HasValue)
        {
            query = query.Where(x => x.CodeListId == input.CodeListId.Value);
        }

        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(filter) ||
                                     x.Code.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Code)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new CommonCodeMapper().Map).ToList();
        return new PagedResultDto<CommonCodeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.CommonCodes.Create)]
    public async Task<CommonCodeDto> CreateAsync(CreateUpdateCommonCodeDto input)
    {
        var entity = new CommonCode(
            GuidGenerator.Create(),
            input.CodeListId,
            input.Title.Trim(),
            input.Code.Trim(),
            input.Description?.Trim(),
            input.AdditionalDataJson,
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new CommonCodeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CommonCodes.Edit)]
    public async Task<CommonCodeDto> UpdateAsync(Guid id, CreateUpdateCommonCodeDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.CodeListId = input.CodeListId;
        entity.Title = input.Title.Trim();
        entity.Code = input.Code.Trim();
        entity.Description = input.Description?.Trim();
        entity.AdditionalDataJson = input.AdditionalDataJson;
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new CommonCodeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CommonCodes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
