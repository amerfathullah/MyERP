using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Telephony.Entities;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Telephony;

[Authorize(MyERPPermissions.TelephonyCallTypes.Default)]
public class TelephonyCallTypeAppService : MyERPAppService, ITelephonyCallTypeAppService
{
    private readonly IRepository<TelephonyCallType, Guid> _repository;

    public TelephonyCallTypeAppService(IRepository<TelephonyCallType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<TelephonyCallTypeDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new TelephonyCallTypeMapper().Map(entity);
    }

    public async Task<PagedResultDto<TelephonyCallTypeDto>> GetListAsync(GetTelephonyCallTypeListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == input.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.CallTypeName.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.CallTypeName)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new TelephonyCallTypeMapper().Map).ToList();
        return new PagedResultDto<TelephonyCallTypeDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.TelephonyCallTypes.Create)]
    public async Task<TelephonyCallTypeDto> CreateAsync(CreateUpdateTelephonyCallTypeDto input)
    {
        var entity = new TelephonyCallType(
            GuidGenerator.Create(),
            input.CallTypeName.Trim(),
            input.IsActive,
            CurrentTenant.Id);

        await _repository.InsertAsync(entity);
        return new TelephonyCallTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TelephonyCallTypes.Edit)]
    public async Task<TelephonyCallTypeDto> UpdateAsync(Guid id, CreateUpdateTelephonyCallTypeDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.CallTypeName = input.CallTypeName.Trim();
        entity.IsActive = input.IsActive;

        await _repository.UpdateAsync(entity);
        return new TelephonyCallTypeMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.TelephonyCallTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
