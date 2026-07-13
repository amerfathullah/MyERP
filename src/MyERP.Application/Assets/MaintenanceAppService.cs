using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

public class MaintenanceScheduleDto : EntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Periodicity { get; set; } = null!;
    public int Status { get; set; }
    public MaintenanceScheduleDetailDto[] Details { get; set; } = [];
}

public class MaintenanceScheduleDetailDto
{
    public Guid Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public bool IsCompleted { get; set; }
}

public class CreateMaintenanceScheduleDto
{
    public Guid CompanyId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Periodicity { get; set; } = "Quarterly";
}

[Authorize(MyERPPermissions.Assets.Default)]
public class MaintenanceAppService : ApplicationService
{
    private readonly IRepository<MaintenanceSchedule, Guid> _scheduleRepo;
    private readonly IRepository<MaintenanceVisit, Guid> _visitRepo;

    public MaintenanceAppService(
        IRepository<MaintenanceSchedule, Guid> scheduleRepo,
        IRepository<MaintenanceVisit, Guid> visitRepo)
    {
        _scheduleRepo = scheduleRepo;
        _visitRepo = visitRepo;
    }

    public async Task<PagedResultDto<MaintenanceScheduleDto>> GetScheduleListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = (await _scheduleRepo.WithDetailsAsync()).AsQueryable();
        var totalCount = query.Count();
        var items = query.OrderByDescending(s => s.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<MaintenanceScheduleDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<MaintenanceScheduleDto> GetScheduleAsync(Guid id)
    {
        var ms = (await _scheduleRepo.WithDetailsAsync()).First(s => s.Id == id);
        return MapToDto(ms);
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<MaintenanceScheduleDto> CreateScheduleAsync(CreateMaintenanceScheduleDto input)
    {
        var ms = new MaintenanceSchedule(GuidGenerator.Create(), input.CompanyId,
            input.StartDate, input.EndDate, input.Periodicity, CurrentTenant.Id)
        {
            AssetId = input.AssetId,
            ItemId = input.ItemId,
            CustomerId = input.CustomerId,
        };
        GenerateScheduleDetails(ms);
        await _scheduleRepo.InsertAsync(ms);
        return MapToDto(ms);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<MaintenanceScheduleDto> SubmitScheduleAsync(Guid id)
    {
        var ms = (await _scheduleRepo.WithDetailsAsync()).First(s => s.Id == id);
        ms.Submit();
        await _scheduleRepo.UpdateAsync(ms);
        return MapToDto(ms);
    }

    private void GenerateScheduleDetails(MaintenanceSchedule schedule)
    {
        var months = schedule.Periodicity switch
        {
            "Monthly" => 1, "Quarterly" => 3, "Half Yearly" => 6, "Yearly" => 12, _ => 3,
        };
        var date = schedule.StartDate;
        while (date <= schedule.EndDate)
        {
            schedule.AddDetail(new MaintenanceScheduleDetail(Guid.NewGuid(), schedule.Id, date));
            date = date.AddMonths(months);
        }
    }

    private static MaintenanceScheduleDto MapToDto(MaintenanceSchedule s) => new()
    {
        Id = s.Id, CompanyId = s.CompanyId, AssetId = s.AssetId, ItemId = s.ItemId,
        CustomerId = s.CustomerId, StartDate = s.StartDate, EndDate = s.EndDate,
        Periodicity = s.Periodicity, Status = (int)s.Status,
        Details = s.Details.Select(d => new MaintenanceScheduleDetailDto
        {
            Id = d.Id, ScheduledDate = d.ScheduledDate, ActualDate = d.ActualDate, IsCompleted = d.IsCompleted,
        }).ToArray(),
    };
}
