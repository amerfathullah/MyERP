using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Notification.DTOs;
using MyERP.Notification.Entities;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace MyERP.Notification;

[Authorize]
public class NotificationAppService : ApplicationService, INotificationAppService
{
    private readonly IRepository<AppNotification, Guid> _repository;

    public NotificationAppService(IRepository<AppNotification, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync()
    {
        var userId = CurrentUser.GetId();
        var query = await _repository.GetQueryableAsync();
        var userNotifications = query.Where(n => n.UserId == userId);

        var unreadCount = userNotifications.Count(n => !n.IsRead);
        var recent = userNotifications
            .OrderByDescending(n => n.CreationTime)
            .Take(5)
            .ToList();

        return new NotificationSummaryDto
        {
            TotalUnread = unreadCount,
            RecentNotifications = recent.Select(MapToDto).ToArray()
        };
    }

    public async Task<PagedResultDto<AppNotificationDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var userId = CurrentUser.GetId();
        var query = await _repository.GetQueryableAsync();
        var userNotifications = query.Where(n => n.UserId == userId);

        var totalCount = userNotifications.Count();
        var items = userNotifications
            .OrderByDescending(n => n.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<AppNotificationDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await _repository.GetAsync(id);
        notification.MarkAsRead();
        await _repository.UpdateAsync(notification);
    }

    public async Task MarkAllAsReadAsync()
    {
        var userId = CurrentUser.GetId();
        var unread = await _repository.GetListAsync(n => n.UserId == userId && !n.IsRead);
        foreach (var notification in unread)
        {
            notification.MarkAsRead();
        }
        await _repository.UpdateManyAsync(unread);
    }

    private AppNotificationDto MapToDto(AppNotification entity) => new()
    {
        Id = entity.Id,
        Subject = entity.Subject,
        Body = entity.Body,
        Severity = entity.Severity,
        IsRead = entity.IsRead,
        ReadAt = entity.ReadAt,
        ActionUrl = entity.ActionUrl,
        SourceDocumentType = entity.SourceDocumentType,
        SourceDocumentId = entity.SourceDocumentId,
        CreationTime = entity.CreationTime
    };
}
