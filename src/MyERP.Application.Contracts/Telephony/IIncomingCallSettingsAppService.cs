using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Telephony;

public interface IIncomingCallSettingsAppService : IApplicationService
{
    Task<IncomingCallSettingsDto> GetAsync();
    Task<IncomingCallSettingsDto> UpdateAsync(UpdateIncomingCallSettingsDto input);
    Task<Guid?> GetActiveEmployeeGroupAsync(DayOfWeek dayOfWeek, TimeSpan time);
}
