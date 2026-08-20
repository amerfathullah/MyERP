using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using MyERP.Maintenance.Entities;
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
        if (input.Items == null || input.Items.Count == 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DocumentMustHaveItems);
        }

        var firstItem = input.Items.First();
        if (firstItem.EndDate < firstItem.StartDate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var itemIds = input.Items.Select(i => i.ItemId).Distinct().ToArray();
        var itemValidation = LazyServiceProvider.LazyGetRequiredService<MyERP.Inventory.DomainServices.ItemTransactionValidationService>();
        await itemValidation.ValidateItemsForTransactionAsync(itemIds);

        var entity = new MaintenanceSchedule(
            GuidGenerator.Create(), input.CompanyId,
            firstItem.StartDate, firstItem.EndDate,
            firstItem.Periodicity.ToString(), CurrentTenant.Id)
        {
            CustomerId = input.CustomerId,
            SalesOrderId = input.SalesOrderId
        };

        if (input.Items.Any())
        {
            entity.ItemId = firstItem.ItemId;
            entity.SerialNoId = firstItem.SerialNoId;
            entity.SalesPersonId = firstItem.SalesPersonId;
        }

        await _repository.InsertAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaintenanceSchedule", entity.Id,
            "Created", entity.CompanyId,
            entity.Id.ToString()[..8], "Draft", "Draft", CurrentUser.Id,
            $"Maintenance Schedule {entity.Id.ToString()[..8]} created", CurrentTenant.Id));

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
        entity.ClearDetails();

        // Generate evenly-spaced visit dates per ERPNext algorithm with holiday awareness (gotcha #850)
        var dateDiff = (entity.EndDate - entity.StartDate).Days;
        var daysInPeriod = GetDaysInPeriod(entity.Periodicity);
        var noOfVisits = daysInPeriod > 0 ? Math.Max(1, dateDiff / daysInPeriod) : 1;
        var interval = dateDiff / noOfVisits;

        var holidayListRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<MyERP.HumanResources.Entities.HolidayList, Guid>>();
        var holidayLists = await holidayListRepo.GetListAsync(h => h.CompanyId == entity.CompanyId, includeDetails: true);

        for (int i = 0; i < noOfVisits; i++)
        {
            var scheduledDate = entity.StartDate.AddDays(i * interval);
            if (scheduledDate > entity.EndDate)
                scheduledDate = entity.EndDate;

            var holidayList = holidayLists.FirstOrDefault(h => h.Year == scheduledDate.Year)
                ?? holidayLists.FirstOrDefault(h => h.IsDefault);

            if (holidayList != null)
            {
                while (holidayList.IsHoliday(scheduledDate) && scheduledDate < entity.EndDate)
                {
                    scheduledDate = scheduledDate.AddDays(1);
                }
            }

            entity.AddDetail(new MaintenanceScheduleDetail(
                GuidGenerator.Create(), entity.Id, scheduledDate));
        }

        await _repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaintenanceSchedule", entity.Id,
            "ScheduleGenerated", entity.CompanyId,
            entity.Id.ToString()[..8], "Draft", "Draft", CurrentUser.Id,
            $"Maintenance Schedule {entity.Id.ToString()[..8]} generated {noOfVisits} visits", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Submit)]
    public async Task<MaintenanceScheduleDto> SubmitAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Submit();
        await _repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaintenanceSchedule", entity.Id,
            "Submitted", entity.CompanyId,
            entity.Id.ToString()[..8], "Draft", "Submitted", CurrentUser.Id,
            $"Maintenance Schedule {entity.Id.ToString()[..8]} submitted", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.MaintenanceSchedules.Submit)]
    public async Task<MaintenanceScheduleDto> CancelAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        entity.Cancel();
        await _repository.UpdateAsync(entity, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "MaintenanceSchedule", entity.Id,
            "Cancelled", entity.CompanyId,
            entity.Id.ToString()[..8], entity.Status.ToString(), "Cancelled", CurrentUser.Id,
            $"Maintenance Schedule {entity.Id.ToString()[..8]} cancelled", CurrentTenant.Id));

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
