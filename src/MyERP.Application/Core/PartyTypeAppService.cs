using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Core.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

[Authorize(MyERPPermissions.PartyTypes.Default)]
public class PartyTypeAppService : MyERPAppService, IPartyTypeAppService
{
    private readonly IRepository<PartyType, Guid> _repository;

    public PartyTypeAppService(IRepository<PartyType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PartyTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new PartyTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<PartyTypeDto>> GetListAsync(GetPartyTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.AccountType.HasValue)
            query = query.Where(x => x.AccountType == input.AccountType.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new PartyTypeMapper().Map(e)).ToList();
        return new PagedResultDto<PartyTypeDto>(totalCount, dtos);
    }

    public async Task<List<PartyTypeDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Name));
        return entities.Select(e => new PartyTypeMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.PartyTypes.Create)]
    public async Task<PartyTypeDto> CreateAsync(CreateUpdatePartyTypeDto input)
    {
        var entity = new PartyType(GuidGenerator.Create(), input.Name, input.AccountType, CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new PartyTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.PartyTypes.Edit)]
    public async Task<PartyTypeDto> UpdateAsync(Guid id, CreateUpdatePartyTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Name = input.Name;
        entity.AccountType = input.AccountType;

        await _repository.UpdateAsync(entity);
        return new PartyTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.PartyTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
