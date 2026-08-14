using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class NotificationLogDto : EntityDto<Guid>
{
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
