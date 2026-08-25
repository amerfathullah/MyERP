using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Inventory.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory;

[Authorize(MyERPPermissions.QualityInspectionParameterGroups.Default)]
public class QualityInspectionParameterGroupAppService : MyERPAppService, IQualityInspectionParameterGroupAppService
{
    private readonly IRepository<QualityInspectionParameterGroup, Guid> _repository;

    public QualityInspectionParameterGroupAppService(IRepository<QualityInspectionParameterGroup, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<QualityInspectionParameterGroupDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new QualityInspectionParameterGroupMapper().Map(entity);
    }

    public async Task<PagedResultDto<QualityInspectionParameterGroupDto>> GetListAsync(GetQualityInspectionParameterGroupListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.GroupName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.GroupName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new QualityInspectionParameterGroupMapper().Map(e)).ToList();
        return new PagedResultDto<QualityInspectionParameterGroupDto>(totalCount, dtos);
    }

    public async Task<List<QualityInspectionParameterGroupDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(
            query.Where(x => x.IsActive)
                 .OrderBy(x => x.GroupName));

        return entities.Select(e => new QualityInspectionParameterGroupMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.QualityInspectionParameterGroups.Create)]
    public async Task<QualityInspectionParameterGroupDto> CreateAsync(CreateUpdateQualityInspectionParameterGroupDto input)
    {
        var entity = new QualityInspectionParameterGroup(
            GuidGenerator.Create(),
            input.GroupName,
            input.Description,
            CurrentTenant.Id)
        {
            IsActive = input.IsActive
        };

        await _repository.InsertAsync(entity);
        return new QualityInspectionParameterGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.QualityInspectionParameterGroups.Edit)]
    public async Task<QualityInspectionParameterGroupDto> UpdateAsync(Guid id, CreateUpdateQualityInspectionParameterGroupDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.GroupName = input.GroupName;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new QualityInspectionParameterGroupMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.QualityInspectionParameterGroups.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
