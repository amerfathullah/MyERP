using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Core;

public interface INotificationLogAppService : IApplicationService
{
    Task<PagedResultDto<NotificationLogDto>> GetListAsync(GetNotificationLogListDto input);
    Task<NotificationLogDto> GetAsync(Guid id);
    Task<int> GetFailedCountAsync();
}
