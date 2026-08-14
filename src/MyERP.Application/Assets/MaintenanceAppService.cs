using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Assets.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class MaintenanceAppService : ApplicationService, IMaintenanceAppService
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
        return new PagedResultDto<MaintenanceScheduleDto>(totalCount, items.Select(x => ObjectMapper.Map<MaintenanceSchedule, MaintenanceScheduleDto>(x)).ToList());
    }

    public async Task<MaintenanceScheduleDto> GetScheduleAsync(Guid id)
    {
        var ms = (await _scheduleRepo.WithDetailsAsync()).First(s => s.Id == id);
        return ObjectMapper.Map<MaintenanceSchedule, MaintenanceScheduleDto>(ms);
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
        return ObjectMapper.Map<MaintenanceSchedule, MaintenanceScheduleDto>(ms);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<MaintenanceScheduleDto> SubmitScheduleAsync(Guid id)
    {
        var ms = (await _scheduleRepo.WithDetailsAsync()).First(s => s.Id == id);
        ms.Submit();
        await _scheduleRepo.UpdateAsync(ms);
        return ObjectMapper.Map<MaintenanceSchedule, MaintenanceScheduleDto>(ms);
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

    // --- Maintenance Visit CRUD ---

    public async Task<PagedResultDto<MaintenanceVisitDto>> GetVisitListAsync(GetMaintenanceVisitListDto input)
    {
        var query = (await _visitRepo.WithDetailsAsync()).AsQueryable();

        if (input.CompletionStatus.HasValue)
            query = query.Where(v => v.CompletionStatus == input.CompletionStatus.Value);
        if (input.MaintenanceScheduleId.HasValue)
            query = query.Where(v => v.MaintenanceScheduleId == input.MaintenanceScheduleId.Value);
        if (!string.IsNullOrWhiteSpace(input.MaintenanceType))
            query = query.Where(v => v.MaintenanceType == input.MaintenanceType);
        if (input.CustomerId.HasValue)
            query = query.Where(v => v.CustomerId == input.CustomerId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(v => v.VisitDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MaintenanceVisitDto>(totalCount,
            items.Select(ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>).ToList());
    }

    public async Task<MaintenanceVisitDto> GetVisitAsync(Guid id)
    {
        var visit = (await _visitRepo.WithDetailsAsync()).First(v => v.Id == id);
        return ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>(visit);
    }

    [Authorize(MyERPPermissions.Assets.Create)]
    public async Task<MaintenanceVisitDto> CreateVisitAsync(CreateMaintenanceVisitDto input)
    {
        var visit = new MaintenanceVisit(GuidGenerator.Create(), input.CompanyId,
            input.VisitDate, input.MaintenanceType, CurrentTenant.Id)
        {
            MaintenanceScheduleId = input.MaintenanceScheduleId,
            CustomerId = input.CustomerId,
            ContactId = input.ContactId,
        };

        foreach (var p in input.Purposes)
        {
            visit.AddPurpose(new MaintenanceVisitPurpose(GuidGenerator.Create(), visit.Id, p.WorkDone)
            {
                ItemId = p.ItemId,
                ItemName = p.ItemName,
                SerialNoId = p.SerialNoId,
                WorkDetails = p.WorkDetails,
            });
        }

        await _visitRepo.InsertAsync(visit);
        return ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>(visit);
    }

    [Authorize(MyERPPermissions.Assets.Edit)]
    public async Task<MaintenanceVisitDto> UpdateVisitAsync(Guid id, CreateMaintenanceVisitDto input)
    {
        var visit = (await _visitRepo.WithDetailsAsync()).First(v => v.Id == id);

        if (visit.CompletionStatus == MaintenanceVisitStatus.Completed ||
            visit.CompletionStatus == MaintenanceVisitStatus.Cancelled)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("message", "Cannot edit a completed or cancelled visit.");
        }

        visit.VisitDate = input.VisitDate;
        visit.MaintenanceType = input.MaintenanceType;
        visit.MaintenanceScheduleId = input.MaintenanceScheduleId;
        visit.CustomerId = input.CustomerId;
        visit.ContactId = input.ContactId;

        await _visitRepo.UpdateAsync(visit);
        return ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>(visit);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<MaintenanceVisitDto> CompleteVisitAsync(Guid id)
    {
        var visit = await _visitRepo.GetAsync(id);
        visit.Complete();
        await _visitRepo.UpdateAsync(visit);
        return ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>(visit);
    }

    [Authorize(MyERPPermissions.Assets.Submit)]
    public async Task<MaintenanceVisitDto> CancelVisitAsync(Guid id)
    {
        var visit = await _visitRepo.GetAsync(id);
        visit.Cancel();
        await _visitRepo.UpdateAsync(visit);
        return ObjectMapper.Map<MaintenanceVisit, MaintenanceVisitDto>(visit);
    }

    [Authorize(MyERPPermissions.Assets.Delete)]
    public async Task DeleteVisitAsync(Guid id)
    {
        var visit = await _visitRepo.GetAsync(id);
        if (visit.CompletionStatus == MaintenanceVisitStatus.Completed)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition)
                .WithData("message", "Cannot delete a completed visit.");
        }
        await _visitRepo.DeleteAsync(id);
    }
}

