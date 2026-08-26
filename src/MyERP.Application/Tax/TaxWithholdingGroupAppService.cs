using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Tax.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Tax;

[Authorize(MyERPPermissions.TaxWithholdingGroups.Default)]
public class TaxWithholdingGroupAppService : MyERPAppService, ITaxWithholdingGroupAppService
{
    private readonly IRepository<TaxWithholdingGroup, Guid> _repository;

    public TaxWithholdingGroupAppService(IRepository<TaxWithholdingGroup, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TaxWithholdingGroupDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new TaxWithholdingGroupMapper().Map(entity);
    }

    public async Task<PagedResultDto<TaxWithholdingGroupDto>> GetListAsync(GetTaxWithholdingGroupListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.GroupName.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.GroupName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new TaxWithholdingGroupMapper().Map).ToList();
        return new PagedResultDto<TaxWithholdingGroupDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.TaxWithholdingGroups.Create)]
    public async Task<TaxWithholdingGroupDto> CreateAsync(CreateUpdateTaxWithholdingGroupDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.GroupName.ToLower() == input.GroupName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Tax withholding group '{input.GroupName}' already exists.");
        }

        var entity = new TaxWithholdingGroup(
            GuidGenerator.Create(),
            input.GroupName.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new TaxWithholdingGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TaxWithholdingGroups.Edit)]
    public async Task<TaxWithholdingGroupDto> UpdateAsync(Guid id, CreateUpdateTaxWithholdingGroupDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.GroupName.ToLower() == input.GroupName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Tax withholding group '{input.GroupName}' already exists.");
        }

        entity.SetGroupName(input.GroupName.Trim());
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new TaxWithholdingGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TaxWithholdingGroups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
