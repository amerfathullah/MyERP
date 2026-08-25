using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Sales.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales;

[Authorize(MyERPPermissions.SalesPartnerTypes.Default)]
public class SalesPartnerTypeAppService : MyERPAppService, ISalesPartnerTypeAppService
{
    private readonly IRepository<SalesPartnerType, Guid> _repository;

    public SalesPartnerTypeAppService(IRepository<SalesPartnerType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<SalesPartnerTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new SalesPartnerTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<SalesPartnerTypeDto>> GetListAsync(GetSalesPartnerTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();
        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.PartnerTypeName.ToLower().Contains(filter) ||
                                     (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.PartnerTypeName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new SalesPartnerTypeMapper().Map).ToList();
        return new PagedResultDto<SalesPartnerTypeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.SalesPartnerTypes.Create)]
    public async Task<SalesPartnerTypeDto> CreateAsync(CreateUpdateSalesPartnerTypeDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.PartnerTypeName.ToLower() == input.PartnerTypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Sales partner type '{input.PartnerTypeName}' already exists.");
        }

        var entity = new SalesPartnerType(
            GuidGenerator.Create(),
            input.PartnerTypeName.Trim(),
            input.Description?.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new SalesPartnerTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.SalesPartnerTypes.Edit)]
    public async Task<SalesPartnerTypeDto> UpdateAsync(Guid id, CreateUpdateSalesPartnerTypeDto input)
    {
        var entity = await _repository.GetAsync(id);

        var query = await _repository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x => x.Id != id && x.PartnerTypeName.ToLower() == input.PartnerTypeName.Trim().ToLower()));
        if (exists)
        {
            throw new UserFriendlyException($"Sales partner type '{input.PartnerTypeName}' already exists.");
        }

        entity.SetPartnerTypeName(input.PartnerTypeName.Trim());
        entity.Description = input.Description?.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new SalesPartnerTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.SalesPartnerTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
