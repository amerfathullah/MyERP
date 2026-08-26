using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Communication;

public interface ICommunicationMediumAppService : ICrudAppService<CommunicationMediumDto, Guid, GetCommunicationMediumListDto, CreateUpdateCommunicationMediumDto, CreateUpdateCommunicationMediumDto>
{
    Task<Guid?> GetHandlingEmployeeGroupAsync(Guid id, DayOfWeek dayOfWeek, TimeSpan time);
}
