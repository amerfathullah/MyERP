using System;
using System.Threading.Tasks;
using MyERP.Notification.DTOs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Notification;

public interface INotificationAppService : IApplicationService
{
    Task<NotificationSummaryDto> GetSummaryAsync();
    Task<PagedResultDto<AppNotificationDto>> GetListAsync(PagedAndSortedResultRequestDto input);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
