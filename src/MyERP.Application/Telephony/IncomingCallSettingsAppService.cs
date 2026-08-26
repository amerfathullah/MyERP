using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Telephony.Entities;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Telephony;

[Authorize(MyERPPermissions.IncomingCallSettings.Default)]
public class IncomingCallSettingsAppService : MyERPAppService, IIncomingCallSettingsAppService
{
    private readonly IRepository<IncomingCallSettings, Guid> _repository;

    public IncomingCallSettingsAppService(IRepository<IncomingCallSettings, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<IncomingCallSettingsDto> GetAsync()
    {
        var entity = await GetOrCreateAsync();
        return new IncomingCallSettingsMapper().Map(entity);
    }

    [Authorize(MyERPPermissions.IncomingCallSettings.Edit)]
    public async Task<IncomingCallSettingsDto> UpdateAsync(UpdateIncomingCallSettingsDto input)
    {
        var entity = await GetOrCreateAsync();

        entity.CallRouting = input.CallRouting;
        entity.GreetingMessage = input.GreetingMessage?.Trim();
        entity.AgentBusyMessage = input.AgentBusyMessage?.Trim();
        entity.AgentUnavailableMessage = input.AgentUnavailableMessage?.Trim();

        entity.ClearSchedules();
        if (input.Schedules != null)
        {
            foreach (var s in input.Schedules)
            {
                entity.AddSchedule(s.DayOfWeek, s.FromTime, s.ToTime, s.EmployeeGroupId);
            }
        }

        await _repository.UpdateAsync(entity);
        return new IncomingCallSettingsMapper().Map(entity);
    }

    public async Task<Guid?> GetActiveEmployeeGroupAsync(DayOfWeek dayOfWeek, TimeSpan time)
    {
        var entity = await GetOrCreateAsync();
        return entity.GetActiveEmployeeGroup(dayOfWeek, time);
    }

    private async Task<IncomingCallSettings> GetOrCreateAsync()
    {
        var list = await _repository.GetListAsync();
        var entity = list.FirstOrDefault();

        if (entity == null)
        {
            entity = new IncomingCallSettings(GuidGenerator.Create(), CallRoutingMode.Sequential, tenantId: CurrentTenant.Id);
            await _repository.InsertAsync(entity);
        }

        return entity;
    }
}
