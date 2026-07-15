using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Permissions;
using MyERP.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Core;

/// <summary>
/// Read-only access to notification logs — audit trail for all sent notifications.
/// Tracks: Email, InApp, Push channels with Queued→Sent/Failed status.
/// Used by: admin dashboard, delivery troubleshooting, compliance audit.
/// </summary>
[Authorize(MyERPPermissions.AutomationRules.Default)]
public class NotificationLogAppService : ApplicationService
{
    private readonly IRepository<NotificationLog, Guid> _repository;

    public NotificationLogAppService(IRepository<NotificationLog, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<NotificationLogDto>> GetListAsync(GetNotificationLogListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (!string.IsNullOrEmpty(input.Channel))
            query = query.Where(n => n.Channel.ToString() == input.Channel);
        if (!string.IsNullOrEmpty(input.Status))
            query = query.Where(n => n.Status.ToString() == input.Status);
        if (!string.IsNullOrEmpty(input.DocumentType))
            query = query.Where(n => n.DocumentType == input.DocumentType);

        var count = query.Count();
        var list = query.OrderByDescending(n => n.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<NotificationLogDto>(count, list.Select(MapToDto).ToList());
    }

    public async Task<NotificationLogDto> GetAsync(Guid id)
    {
        var log = await _repository.GetAsync(id);
        return MapToDto(log);
    }

    /// <summary>
    /// Get failed notification count for dashboard widget.
    /// </summary>
    public async Task<int> GetFailedCountAsync()
    {
        var query = await _repository.GetQueryableAsync();
        return query.Count(n => n.Status == NotificationStatus.Failed || n.Status == NotificationStatus.PermanentlyFailed);
    }

    private static NotificationLogDto MapToDto(NotificationLog n) => new()
    {
        Id = n.Id,
        Recipient = n.Recipient,
        Subject = n.Subject,
        Channel = n.Channel.ToString(),
        Status = n.Status.ToString(),
        DocumentType = n.DocumentType,
        DocumentId = n.DocumentId,
        ErrorMessage = n.ErrorMessage,
        RetryCount = n.RetryCount,
        SentAt = n.SentAt,
        CreatedAt = n.CreationTime
    };
}

#region DTOs

public class NotificationLogDto
{
    public Guid Id { get; set; }
    public string Recipient { get; set; } = null!;
    public string? Subject { get; set; }
    public string Channel { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? DocumentType { get; set; }
    public Guid? DocumentId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetNotificationLogListDto : PagedAndSortedResultRequestDto
{
    public string? Channel { get; set; }
    public string? Status { get; set; }
    public string? DocumentType { get; set; }
}

#endregion
