using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class DocumentActivityLogDto : EntityDto<Guid>
{
    public string DocumentType { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public string? DocumentNumber { get; set; }
    public string ActivityType { get; set; } = null!;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? Details { get; set; }
    public DateTime CreationTime { get; set; }
}
