using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Core;

public class AutoRepeatDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public string ReferenceDocumentType { get; set; } = null!;
    public Guid ReferenceDocumentId { get; set; }
    public string? ReferenceDocumentNumber { get; set; }
    public string Frequency { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextScheduleDate { get; set; }
    public bool IsEnabled { get; set; }
    public int GeneratedCount { get; set; }
    public DateTime? LastGeneratedDate { get; set; }
    public bool NotifyByEmail { get; set; }
}

public class CreateAutoRepeatDto
{
    public Guid CompanyId { get; set; }
    public string ReferenceDocumentType { get; set; } = null!;
    public Guid ReferenceDocumentId { get; set; }
    public string? ReferenceDocumentNumber { get; set; }
    public RepeatFrequency Frequency { get; set; }
    public RepeatDayOfWeek? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool NotifyByEmail { get; set; }
    public string? NotifyRecipients { get; set; }
}
