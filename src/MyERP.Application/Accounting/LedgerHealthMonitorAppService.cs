using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.DomainServices;
using MyERP.Accounting.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Accounting;

[Authorize(MyERPPermissions.Accounts.Default)]
public class LedgerHealthMonitorAppService : ApplicationService, ILedgerHealthMonitorAppService
{
    private readonly IRepository<LedgerHealthMonitorSettings, Guid> _settingsRepository;
    private readonly IRepository<LedgerHealthRecord, Guid> _recordRepository;
    private readonly LedgerHealthCheckService _checkService;

    public LedgerHealthMonitorAppService(
        IRepository<LedgerHealthMonitorSettings, Guid> settingsRepository,
        IRepository<LedgerHealthRecord, Guid> recordRepository,
        LedgerHealthCheckService checkService)
    {
        _settingsRepository = settingsRepository;
        _recordRepository = recordRepository;
        _checkService = checkService;
    }

    public async Task<LedgerHealthMonitorSettingsDto> GetSettingsAsync(GetLedgerHealthMonitorSettingsInput input)
    {
        var query = await _settingsRepository.GetQueryableAsync();
        var settings = query.FirstOrDefault(s => s.CompanyId == input.CompanyId);
        return ToDto(input.CompanyId, settings);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<LedgerHealthMonitorSettingsDto> UpdateSettingsAsync(UpdateLedgerHealthMonitorSettingsDto input)
    {
        var query = await _settingsRepository.GetQueryableAsync();
        var settings = query.FirstOrDefault(s => s.CompanyId == input.CompanyId);

        if (settings == null)
        {
            settings = new LedgerHealthMonitorSettings(GuidGenerator.Create(), input.CompanyId, CurrentTenant.Id)
            {
                IsEnabled = input.IsEnabled,
                LookbackPeriodDays = input.LookbackPeriodDays,
            };
            await _settingsRepository.InsertAsync(settings);
        }
        else
        {
            settings.IsEnabled = input.IsEnabled;
            settings.LookbackPeriodDays = input.LookbackPeriodDays;
            await _settingsRepository.UpdateAsync(settings);
        }

        return ToDto(input.CompanyId, settings);
    }

    [Authorize(MyERPPermissions.Accounts.Edit)]
    public async Task<LedgerHealthCheckRunResultDto> RunCheckAsync(RunLedgerHealthCheckDto input)
    {
        var records = await _checkService.RunAndPersistAsync(input.CompanyId);

        return new LedgerHealthCheckRunResultDto
        {
            IsHealthy = !records.Any(r => r.Severity == "Critical"),
            TotalChecked = records.Count,
            Issues = records.Select(ToDto).ToList(),
        };
    }

    public async Task<PagedResultDto<LedgerHealthRecordDto>> GetRecordsAsync(GetLedgerHealthRecordsInput input)
    {
        var query = await _recordRepository.GetQueryableAsync();
        query = query.Where(r => r.CompanyId == input.CompanyId);

        var totalCount = query.Count();
        var records = query
            .OrderByDescending(r => r.CheckedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<LedgerHealthRecordDto>(totalCount, records.Select(ToDto).ToList());
    }

    private static LedgerHealthMonitorSettingsDto ToDto(Guid companyId, LedgerHealthMonitorSettings? settings)
    {
        if (settings == null)
            return new LedgerHealthMonitorSettingsDto { CompanyId = companyId, IsEnabled = false, LookbackPeriodDays = 30 };

        return new LedgerHealthMonitorSettingsDto
        {
            CompanyId = settings.CompanyId,
            IsEnabled = settings.IsEnabled,
            LookbackPeriodDays = settings.LookbackPeriodDays,
        };
    }

    private static LedgerHealthRecordDto ToDto(LedgerHealthRecord record)
    {
        return new LedgerHealthRecordDto
        {
            Id = record.Id,
            CheckType = record.CheckType,
            Severity = record.Severity,
            Description = record.Description,
            VoucherType = record.VoucherType,
            VoucherId = record.VoucherId,
            Difference = record.Difference,
            CheckedAt = record.CheckedAt,
        };
    }
}
