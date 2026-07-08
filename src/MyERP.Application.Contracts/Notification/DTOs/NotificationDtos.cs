using System;
using System.ComponentModel.DataAnnotations;
using MyERP.Notification;
using Volo.Abp.Application.Dtos;

namespace MyERP.Notification.DTOs;

public class AppNotificationDto : EntityDto<Guid>
{
    public string Subject { get; set; } = null!;
    public string? Body { get; set; }
    public NotificationSeverity Severity { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public DateTime CreationTime { get; set; }
}

public class NotificationSummaryDto
{
    public int TotalUnread { get; set; }
    public AppNotificationDto[] RecentNotifications { get; set; } = [];
}
