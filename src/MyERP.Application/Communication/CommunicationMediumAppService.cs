using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Communication.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Communication;

[Authorize(MyERPPermissions.CommunicationMedia.Default)]
public class CommunicationMediumAppService : MyERPAppService, ICommunicationMediumAppService
{
    private readonly IRepository<CommunicationMedium, Guid> _repository;

    public CommunicationMediumAppService(IRepository<CommunicationMedium, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CommunicationMediumDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return new CommunicationMediumMapper().Map(entity);
    }

    public async Task<PagedResultDto<CommunicationMediumDto>> GetListAsync(GetCommunicationMediumListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CommunicationMediumType.HasValue)
        {
            query = query.Where(x => x.CommunicationMediumType == input.CommunicationMediumType.Value);
        }

        if (input.IsDisabled.HasValue)
        {
            query = query.Where(x => x.IsDisabled == input.IsDisabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            query = query.Where(x => x.CommunicationChannel != null && x.CommunicationChannel.ToLower().Contains(filter));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var entities = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.CommunicationMediumType)
                 .ThenBy(x => x.CommunicationChannel)
                 .Skip(input.SkipCount)
                 .Take(input.MaxResultCount));

        var dtos = entities.Select(new CommunicationMediumMapper().Map).ToList();
        return new PagedResultDto<CommunicationMediumDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.CommunicationMedia.Create)]
    public async Task<CommunicationMediumDto> CreateAsync(CreateUpdateCommunicationMediumDto input)
    {
        var entity = new CommunicationMedium(
            GuidGenerator.Create(),
            input.CommunicationMediumType,
            input.CommunicationChannel?.Trim(),
            input.CatchAllEmployeeGroupId,
            input.ProviderSupplierId,
            input.IsDisabled,
            CurrentTenant.Id);

        if (input.Timeslots != null)
        {
            foreach (var slot in input.Timeslots)
            {
                entity.AddTimeslot(slot.DayOfWeek, slot.FromTime, slot.ToTime, slot.EmployeeGroupId);
            }
        }

        await _repository.InsertAsync(entity);
        return new CommunicationMediumMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CommunicationMedia.Edit)]
    public async Task<CommunicationMediumDto> UpdateAsync(Guid id, CreateUpdateCommunicationMediumDto input)
    {
        var entity = await _repository.GetAsync(id);

        entity.CommunicationMediumType = input.CommunicationMediumType;
        entity.CommunicationChannel = input.CommunicationChannel?.Trim();
        entity.CatchAllEmployeeGroupId = input.CatchAllEmployeeGroupId;
        entity.ProviderSupplierId = input.ProviderSupplierId;
        entity.IsDisabled = input.IsDisabled;

        entity.ClearTimeslots();
        if (input.Timeslots != null)
        {
            foreach (var slot in input.Timeslots)
            {
                entity.AddTimeslot(slot.DayOfWeek, slot.FromTime, slot.ToTime, slot.EmployeeGroupId);
            }
        }

        await _repository.UpdateAsync(entity);
        return new CommunicationMediumMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.CommunicationMedia.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<Guid?> GetHandlingEmployeeGroupAsync(Guid id, DayOfWeek dayOfWeek, TimeSpan time)
    {
        var entity = await _repository.GetAsync(id);
        return entity.GetHandlingEmployeeGroup(dayOfWeek, time);
    }
}
