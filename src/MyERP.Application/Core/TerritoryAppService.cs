using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize(MyERPPermissions.Territories.Default)]
public class TerritoryAppService : MyERPAppService, ITerritoryAppService
{
    private readonly IRepository<Territory, Guid> _repository;

    public TerritoryAppService(IRepository<Territory, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TerritoryDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        var dto = new TerritoryMapper().Map(entity);
        if (entity.ParentId.HasValue)
        {
            var parent = await _repository.FindAsync(entity.ParentId.Value);
            dto.ParentName = parent?.Name;
        }
        return dto;
    }

    public async Task<PagedResultDto<TerritoryDto>> GetListAsync(GetTerritoryListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.ParentId.HasValue)
            query = query.Where(x => x.ParentId == input.ParentId.Value);
        if (input.IsGroup.HasValue)
            query = query.Where(x => x.IsGroup == input.IsGroup.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
            query = query.Where(x => x.Name.Contains(input.Filter));

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new TerritoryMapper().Map(e)).ToList();
        return new PagedResultDto<TerritoryDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Territories.Create)]
    public async Task<TerritoryDto> CreateAsync(CreateUpdateTerritoryDto input)
    {
        var entity = new Territory(GuidGenerator.Create(), input.Name, input.ParentId, input.IsGroup, CurrentTenant.Id)
        {
            TerritoryManagerId = input.TerritoryManagerId,
        };

        await _repository.InsertAsync(entity);
        return new TerritoryMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Territories.Edit)]
    public async Task<TerritoryDto> UpdateAsync(Guid id, CreateUpdateTerritoryDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.ParentId = input.ParentId;
        entity.IsGroup = input.IsGroup;
        entity.TerritoryManagerId = input.TerritoryManagerId;

        await _repository.UpdateAsync(entity);
        return new TerritoryMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.Territories.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
