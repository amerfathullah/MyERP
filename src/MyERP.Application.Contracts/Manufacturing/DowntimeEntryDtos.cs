using System;
using Volo.Abp.Application.Dtos;

namespace MyERP.Manufacturing;

public class DowntimeEntryDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid WorkstationId { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public decimal DowntimeMinutes { get; set; }
    public string StopReason { get; set; } = null!;
    public string? Remarks { get; set; }
}

public class CreateUpdateDowntimeEntryDto
{
    public Guid CompanyId { get; set; }
    public Guid WorkstationId { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }
    public string StopReason { get; set; } = null!;
    public string? Remarks { get; set; }
}
