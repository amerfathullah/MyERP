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
/// Manages Auto Repeat configurations — recurring document generation schedules.
/// Documents are always created as Draft (never auto-submitted).
/// </summary>
[Authorize(MyERPPermissions.AutomationRules.Default)]
public class AutoRepeatAppService : ApplicationService
{
    private readonly IRepository<AutoRepeat, Guid> _repository;

    public AutoRepeatAppService(IRepository<AutoRepeat, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<AutoRepeatDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<AutoRepeat, AutoRepeatDto>(entity);
    }

    public async Task<PagedResultDto<AutoRepeatDto>> GetListAsync(CompanyFilteredPagedRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(x => x.CompanyId == input.CompanyId.Value);

        var count = query.Count();
        var list = query.OrderByDescending(x => x.NextScheduleDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<AutoRepeatDto>(count, list.Select(x => ObjectMapper.Map<AutoRepeat, AutoRepeatDto>(x)).ToList());
    }

    [Authorize(MyERPPermissions.AutomationRules.Create)]
    public async Task<AutoRepeatDto> CreateAsync(CreateAutoRepeatDto input)
    {
        var entity = new AutoRepeat(
            GuidGenerator.Create(),
            input.CompanyId,
            input.ReferenceDocumentType,
            input.ReferenceDocumentId,
            input.Frequency,
            input.StartDate,
            input.EndDate,
            CurrentTenant.Id);

        entity.DayOfWeek = input.DayOfWeek;
        entity.DayOfMonth = input.DayOfMonth;
        entity.NotifyByEmail = input.NotifyByEmail;
        entity.NotifyRecipients = input.NotifyRecipients;
        entity.ReferenceDocumentNumber = input.ReferenceDocumentNumber;

        await _repository.InsertAsync(entity);
        return ObjectMapper.Map<AutoRepeat, AutoRepeatDto>(entity);
    }

    [Authorize(MyERPPermissions.AutomationRules.Edit)]
    public async Task EnableAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Enable();
        await _repository.UpdateAsync(entity);
    }

    [Authorize(MyERPPermissions.AutomationRules.Edit)]
    public async Task DisableAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Disable();
        await _repository.UpdateAsync(entity);
    }

    [Authorize(MyERPPermissions.AutomationRules.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}

#region DTOs

public class AutoRepeatDto
{
    public Guid Id { get; set; }
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

#endregion
