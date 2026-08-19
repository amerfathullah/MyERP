using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.CRM.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM;

[Authorize(MyERPPermissions.Leads.Default)]
public class CampaignAppService : ApplicationService, ICampaignAppService
{
    private readonly IRepository<Campaign, Guid> _repository;

    public CampaignAppService(IRepository<Campaign, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CampaignDto> GetAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(c => c.Id == id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<CampaignDto>> GetListAsync(GetCampaignListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(c => c.CampaignName.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(c => c.CampaignName)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<CampaignDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Leads.Create)]
    public async Task<CampaignDto> CreateAsync(CreateUpdateCampaignDto input)
    {
        var entity = new Campaign(GuidGenerator.Create(), input.CampaignName, CurrentTenant.Id)
        {
            Description = input.Description,
        };

        foreach (var s in input.EmailSchedules)
        {
            entity.AddEmailSchedule(new CampaignEmailSchedule(GuidGenerator.Create(), entity.Id, s.EmailTemplateId, s.SendAfterDays));
        }

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Campaign", entity.Id,
            "Created", Guid.Empty,
            entity.CampaignName, "Draft", "Active",
            CurrentUser.Id,
            $"Campaign '{entity.CampaignName}' created with {entity.EmailSchedules.Count} email schedules", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Edit)]
    public async Task<CampaignDto> UpdateAsync(Guid id, CreateUpdateCampaignDto input)
    {
        var entity = (await _repository.WithDetailsAsync()).First(c => c.Id == id);
        entity.CampaignName = input.CampaignName;
        entity.Description = input.Description;
        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Campaign", entity.Id,
            "Updated", Guid.Empty,
            entity.CampaignName, "Active", "Active",
            CurrentUser.Id,
            $"Campaign '{entity.CampaignName}' updated", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.Leads.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static CampaignDto MapToDto(Campaign e) => new()
    {
        Id = e.Id,
        CampaignName = e.CampaignName,
        Description = e.Description,
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
        EmailSchedules = e.EmailSchedules.Select(s => new CampaignEmailScheduleDto
        {
            Id = s.Id,
            EmailTemplateId = s.EmailTemplateId,
            SendAfterDays = s.SendAfterDays,
        }).ToList(),
    };
}
