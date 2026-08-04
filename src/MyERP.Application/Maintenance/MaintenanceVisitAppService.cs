using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Assets.Entities;
using MyERP.Permissions;

namespace MyERP.Maintenance;

[Authorize(MyERPPermissions.MaintenanceVisits.Default)]
public class MaintenanceVisitAppService : ApplicationService, IMaintenanceVisitAppService
{
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepository;

    public MaintenanceVisitAppService(IRepository<MaintenanceVisit, Guid> visitRepository)
    {
        _visitRepository = visitRepository;
    }

    public async Task<MaintenanceVisitDto> GetAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<MaintenanceVisitDto>> GetListAsync(GetMaintenanceVisitListDto input)
    {
        var queryable = await _visitRepository.GetQueryableAsync();
        queryable = queryable.WhereIf(input.CustomerId.HasValue,
            v => v.CustomerId == input.CustomerId!.Value);
        queryable = queryable.WhereIf(input.MaintenanceScheduleId.HasValue,
            v => v.MaintenanceScheduleId == input.MaintenanceScheduleId!.Value);
        queryable = queryable.WhereIf(input.MaintenanceType.HasValue,
            v => v.MaintenanceType == (input.MaintenanceType == 0 ? "Scheduled" :
                input.MaintenanceType == 1 ? "Unscheduled" : "Breakdown"));

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(v => v.VisitDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<MaintenanceVisitDto>(
            totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Create)]
    public async Task<MaintenanceVisitDto> CreateAsync(CreateMaintenanceVisitDto input)
    {
        var typeStr = input.MaintenanceType switch
        {
            0 => "Scheduled",
            1 => "Unscheduled",
            2 => "Breakdown",
            _ => "Scheduled"
        };

        var entity = new MaintenanceVisit(
            GuidGenerator.Create(), input.CompanyId,
            input.VisitDate, typeStr, CurrentTenant.Id)
        {
            CustomerId = input.CustomerId,
            ContactId = input.ContactId,
            MaintenanceScheduleId = input.MaintenanceScheduleId
        };

        foreach (var purposeDto in input.Purposes)
        {
            entity.AddPurpose(new MaintenanceVisitPurpose(
                GuidGenerator.Create(), entity.Id, purposeDto.WorkDone ?? string.Empty)
            {
                ItemId = purposeDto.ItemId,
                SerialNoId = purposeDto.SerialNoId
            });
        }

        await _visitRepository.InsertAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Edit)]
    public async Task<MaintenanceVisitDto> UpdateAsync(Guid id, CreateMaintenanceVisitDto input)
    {
        var entity = await _visitRepository.GetAsync(id);
        entity.VisitDate = input.VisitDate;
        entity.ContactId = input.ContactId;
        await _visitRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _visitRepository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Submit)]
    public async Task<MaintenanceVisitDto> SubmitAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);
        entity.Complete();
        await _visitRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceVisits.Submit)]
    public async Task<MaintenanceVisitDto> CancelAsync(Guid id)
    {
        var entity = await _visitRepository.GetAsync(id);
        entity.Cancel();
        await _visitRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    private static MaintenanceVisitDto MapToDto(MaintenanceVisit entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        VisitNumber = entity.Id.ToString("N")[..8].ToUpper(),
        CustomerId = entity.CustomerId ?? Guid.Empty,
        MaintenanceType = entity.MaintenanceType switch
        {
            "Scheduled" => 0,
            "Unscheduled" => 1,
            "Breakdown" => 2,
            _ => 0
        },
        VisitDate = entity.VisitDate,
        CompletionStatus = (int)entity.CompletionStatus,
        MaintenanceScheduleId = entity.MaintenanceScheduleId,
        IsSubmitted = entity.CompletionStatus == MaintenanceVisitStatus.Completed,
        IsCancelled = entity.CompletionStatus == MaintenanceVisitStatus.Cancelled,
        Purposes = entity.Purposes.Select(p => new MaintenanceVisitPurposeDto
        {
            Id = p.Id,
            ItemId = p.ItemId ?? Guid.Empty,
            SerialNoId = p.SerialNoId,
            WorkDone = p.WorkDone,
            Status = 0
        }).ToList()
    };
}
