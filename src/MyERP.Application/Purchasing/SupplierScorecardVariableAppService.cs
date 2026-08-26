using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Purchasing.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Purchasing;

[Authorize(MyERPPermissions.SupplierScorecardVariables.Default)]
public class SupplierScorecardVariableAppService : MyERPAppService, ISupplierScorecardVariableAppService
{
    private readonly IRepository<SupplierScorecardVariable, Guid> _repository;

    public SupplierScorecardVariableAppService(IRepository<SupplierScorecardVariable, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SupplierScorecardVariableDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new SupplierScorecardVariableMapper().Map(entity);
    }

    public async Task<PagedResultDto<SupplierScorecardVariableDto>> GetListAsync(GetSupplierScorecardVariableListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.VariableLabel.ToLower().Contains(filter) || x.ParamName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.VariableLabel)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(e => new SupplierScorecardVariableMapper().Map(e)).ToList();
        return new PagedResultDto<SupplierScorecardVariableDto>(totalCount, dtos);
    }

    public async Task<List<SupplierScorecardVariableDto>> GetAllListAsync()
    {
        var query = await _repository.GetQueryableAsync();
        var entities = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.VariableLabel));
        return entities.Select(e => new SupplierScorecardVariableMapper().Map(e)).ToList();
    }

    [Authorize(MyERPPermissions.SupplierScorecardVariables.Create)]
    public async Task<SupplierScorecardVariableDto> CreateAsync(CreateUpdateSupplierScorecardVariableDto input)
    {
        var entity = new SupplierScorecardVariable(
            GuidGenerator.Create(),
            input.VariableLabel,
            input.ParamName,
            input.Path,
            input.IsCustom,
            input.Description,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new SupplierScorecardVariableMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.SupplierScorecardVariables.Edit)]
    public async Task<SupplierScorecardVariableDto> UpdateAsync(Guid id, CreateUpdateSupplierScorecardVariableDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.VariableLabel = input.VariableLabel;
        entity.ParamName = input.ParamName;
        entity.Path = input.Path;
        entity.IsCustom = input.IsCustom;
        entity.Description = input.Description;

        await _repository.UpdateAsync(entity);
        return new SupplierScorecardVariableMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.SupplierScorecardVariables.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
