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

[Authorize(MyERPPermissions.MaintenanceSchedules.Default)]
public class MaintenanceScheduleAppService : ApplicationService, IMaintenanceScheduleAppService
{
    private readonly IRepository<MaintenanceSchedule, Guid> _repository;

    public MaintenanceScheduleAppService(IRepository<MaintenanceSchedule, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<MaintenanceScheduleDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<MaintenanceScheduleDto>> GetListAsync(GetMaintenanceScheduleListDto input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable.WhereIf(input.CustomerId.HasValue,
            s => s.CustomerId == input.CustomerId!.Value);
        queryable = queryable.WhereIf(input.Status.HasValue,
            s => (int)s.Status == input.Status!.Value);

        var totalCount = queryable.Count();
        var items = queryable
            .OrderByDescending(s => s.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<MaintenanceScheduleDto>(
            totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Create)]
    public async Task<MaintenanceScheduleDto> CreateAsync(CreateMaintenanceScheduleDto input)
    {
        var entity = new MaintenanceSchedule(
            GuidGenerator.Create(), input.CompanyId,
            input.Items.First().StartDate, input.Items.First().EndDate,
            input.Items.First().Periodicity.ToString(), CurrentTenant.Id)
        {
            CustomerId = input.CustomerId,
            SalesOrderId = input.SalesOrderId
        };

        if (input.Items.Any())
        {
            entity.ItemId = input.Items.First().ItemId;
            entity.SerialNoId = input.Items.First().SerialNoId;
            entity.SalesPersonId = input.Items.First().SalesPersonId;
        }

        await _repository.InsertAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Edit)]
    public async Task<MaintenanceScheduleDto> UpdateAsync(Guid id, CreateMaintenanceScheduleDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.CustomerId = input.CustomerId;
        entity.SalesOrderId = input.SalesOrderId;
        await _repository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Edit)]
    public async Task<MaintenanceScheduleDto> GenerateScheduleAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        // Generate evenly-spaced visit dates per ERPNext algorithm
        var dateDiff = (entity.EndDate - entity.StartDate).Days;
        var daysInPeriod = GetDaysInPeriod(entity.Periodicity);
        var noOfVisits = daysInPeriod > 0 ? Math.Max(1, dateDiff / daysInPeriod) : 1;
        var interval = dateDiff / noOfVisits;

        for (int i = 0; i < noOfVisits; i++)
        {
            var scheduledDate = entity.StartDate.AddDays(i * interval);
            if (scheduledDate > entity.EndDate)
                scheduledDate = entity.EndDate;
            entity.AddDetail(new MaintenanceScheduleDetail(
                GuidGenerator.Create(), entity.Id, scheduledDate));
        }

        await _repository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Submit)]
    public async Task<MaintenanceScheduleDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Submit)]
    public async Task<MaintenanceScheduleDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    private static int GetDaysInPeriod(string periodicity) => periodicity switch
    {
        "Weekly" => 7,
        "Monthly" => 30,
        "Quarterly" => 91,
        "HalfYearly" or "Half Yearly" => 182,
        "Yearly" => 365,
        "TwoYearly" => 730,
        "ThreeYearly" => 1095,
        _ => 30
    };

    private static MaintenanceScheduleDto MapToDto(MaintenanceSchedule entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        CustomerId = entity.CustomerId ?? Guid.Empty,
        SalesOrderId = entity.SalesOrderId,
        Status = (int)entity.Status,
        Items = new()
        {
            new MaintenanceScheduleItemDto
            {
                Id = entity.Id,
                ItemId = entity.ItemId ?? Guid.Empty,
                SerialNoId = entity.SerialNoId,
                SalesPersonId = entity.SalesPersonId,
                StartDate = entity.StartDate,
                EndDate = entity.EndDate,
                NoOfVisits = entity.Details.Count,
                Periodicity = 0
            }
        },
        ScheduleDetails = entity.Details.Select(d => new MaintenanceScheduleDetailDto
        {
            Id = d.Id,
            ItemId = entity.ItemId ?? Guid.Empty,
            ScheduledDate = d.ScheduledDate,
            ActualDate = d.ActualDate,
            SalesPersonId = entity.SalesPersonId,
            Status = d.IsCompleted ? 1 : 0
        }).ToList()
    };
}
