using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.OpportunityTypes.Default)]
public class OpportunityTypeAppService : MyERPAppService, IOpportunityTypeAppService
{
    private readonly IRepository<Entities.OpportunityType, Guid> _repository;

    public OpportunityTypeAppService(IRepository<Entities.OpportunityType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<OpportunityTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new OpportunityTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<OpportunityTypeDto>> GetListAsync(GetOpportunityTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.Name)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new OpportunityTypeMapper().Map).ToList();
        return new PagedResultDto<OpportunityTypeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.OpportunityTypes.Create)]
    public async Task<OpportunityTypeDto> CreateAsync(CreateUpdateOpportunityTypeDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Name.ToLower() == input.Name.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Opportunity type '{input.Name}' already exists.");
        }

        var entity = new Entities.OpportunityType(
            GuidGenerator.Create(),
            input.Name.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new OpportunityTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.OpportunityTypes.Edit)]
    public async Task<OpportunityTypeDto> UpdateAsync(Guid id, CreateUpdateOpportunityTypeDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.Name.ToLower() == input.Name.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Opportunity type '{input.Name}' already exists.");
        }

        entity.SetName(input.Name.Trim());
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new OpportunityTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.OpportunityTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
