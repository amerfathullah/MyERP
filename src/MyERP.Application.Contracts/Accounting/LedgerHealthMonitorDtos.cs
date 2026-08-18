using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace MyERP.Accounting;

public class LedgerHealthMonitorSettingsDto
{
    public Guid CompanyId { get; set; }
    public bool IsEnabled { get; set; }
    public int LookbackPeriodDays { get; set; }
}

public class GetLedgerHealthMonitorSettingsInput
{
    public Guid CompanyId { get; set; }
}

public class UpdateLedgerHealthMonitorSettingsDto
{
    public Guid CompanyId { get; set; }
    public bool IsEnabled { get; set; }
    public int LookbackPeriodDays { get; set; } = 30;
}

public class RunLedgerHealthCheckDto
{
    public Guid CompanyId { get; set; }
}

public class GetLedgerHealthRecordsInput : PagedAndSortedResultRequestDto
{
    public Guid CompanyId { get; set; }
}

public class LedgerHealthRecordDto : EntityDto<Guid>
{
    public string CheckType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? VoucherType { get; set; }
    public Guid? VoucherId { get; set; }
    public decimal? Difference { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class LedgerHealthCheckRunResultDto
{
    public bool IsHealthy { get; set; }
    public int TotalChecked { get; set; }
    public List<LedgerHealthRecordDto> Issues { get; set; } = new();
}

public interface ILedgerHealthMonitorAppService : IApplicationService
{
    Task<LedgerHealthMonitorSettingsDto> GetSettingsAsync(GetLedgerHealthMonitorSettingsInput input);
    Task<LedgerHealthMonitorSettingsDto> UpdateSettingsAsync(UpdateLedgerHealthMonitorSettingsDto input);
    Task<LedgerHealthCheckRunResultDto> RunCheckAsync(RunLedgerHealthCheckDto input);
    Task<PagedResultDto<LedgerHealthRecordDto>> GetRecordsAsync(GetLedgerHealthRecordsInput input);
}
