using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Permissions;
using MyERP.Support.Entities;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support;

[Authorize(MyERPPermissions.ServiceLevelAgreements.Default)]
public class ServiceLevelAgreementAppService : ApplicationService, IServiceLevelAgreementAppService
{
    private readonly IRepository<ServiceLevelAgreement, Guid> _repository;

    public ServiceLevelAgreementAppService(IRepository<ServiceLevelAgreement, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<ServiceLevelAgreementDto> GetAsync(Guid id)
    {
        var entity = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<ServiceLevelAgreementDto>> GetListAsync(GetServiceLevelAgreementListDto input)
    {
        var query = (await _repository.WithDetailsAsync()).AsQueryable();
        if (input.CompanyId.HasValue)
            query = query.Where(s => s.CompanyId == input.CompanyId.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter;
            query = query.Where(s => s.Name.Contains(f));
        }

        var totalCount = query.Count();
        var items = query.OrderBy(s => s.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<ServiceLevelAgreementDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.ServiceLevelAgreements.Create)]
    public async Task<ServiceLevelAgreementDto> CreateAsync(CreateServiceLevelAgreementDto input)
    {
        await GuardSingleDefaultAsync(input.CompanyId, input.IsDefault, null);

        var entity = new ServiceLevelAgreement(GuidGenerator.Create(), input.CompanyId, input.Name,
            input.ResolutionTimeHours, input.ResponseTimeHours, CurrentTenant.Id)
        {
            EntityType = input.EntityType,
            EntityId = input.EntityId,
            HolidayListId = input.HolidayListId,
            IsDefault = input.IsDefault,
            ApplyOnResolution = input.ApplyOnResolution,
        };

        foreach (var p in input.Priorities)
        {
            if (p.ResponseTimeHours <= 0 || p.ResolutionTimeHours <= 0)
            {
                throw new BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                    .WithData("field", "ResponseTimeHours/ResolutionTimeHours");
            }

            entity.AddPriority(new ServiceLevelPriority(GuidGenerator.Create(), entity.Id,
                p.PriorityName, p.ResponseTimeHours, p.ResolutionTimeHours) { IsDefault = p.IsDefault });
        }

        foreach (var d in input.ServiceDays)
        {
            if (d.EndTime < d.StartTime)
            {
                throw new BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
            }

            entity.AddServiceDay(new ServiceDay(GuidGenerator.Create(), entity.Id, d.DayOfWeek, d.StartTime, d.EndTime));
        }

        await _repository.InsertAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ServiceLevelAgreement", entity.Id,
            "Created", entity.CompanyId,
            entity.Name, "Draft", "Active", CurrentUser.Id,
            $"Service Level Agreement '{entity.Name}' created", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.ServiceLevelAgreements.Edit)]
    public async Task<ServiceLevelAgreementDto> UpdateAsync(Guid id, CreateServiceLevelAgreementDto input)
    {
        await GuardSingleDefaultAsync(input.CompanyId, input.IsDefault, id);

        var entity = (await _repository.WithDetailsAsync()).First(s => s.Id == id);
        entity.Name = input.Name;
        entity.EntityType = input.EntityType;
        entity.EntityId = input.EntityId;
        entity.HolidayListId = input.HolidayListId;
        entity.ResolutionTimeHours = input.ResolutionTimeHours;
        entity.ResponseTimeHours = input.ResponseTimeHours;
        entity.IsDefault = input.IsDefault;
        entity.ApplyOnResolution = input.ApplyOnResolution;

        await _repository.UpdateAsync(entity);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ServiceLevelAgreement", entity.Id,
            "Updated", entity.CompanyId,
            entity.Name, "Active", "Active", CurrentUser.Id,
            $"Service Level Agreement '{entity.Name}' updated", CurrentTenant.Id));

        return MapToDto(entity);
    }

    [Authorize(MyERPPermissions.ServiceLevelAgreements.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    /// <summary>Only one default SLA is allowed per company (matches ERPNext's default-scoping rule).</summary>
    private async Task GuardSingleDefaultAsync(Guid companyId, bool isDefault, Guid? excludingId)
    {
        if (!isDefault) return;

        var query = await _repository.GetQueryableAsync();
        var hasOtherDefault = query.Any(s => s.CompanyId == companyId && s.IsDefault && s.Id != excludingId);
        if (hasOtherDefault)
            throw new BusinessException(MyERPDomainErrorCodes.DuplicateDefaultServiceLevelAgreement);
    }

    private static ServiceLevelAgreementDto MapToDto(ServiceLevelAgreement entity) => new()
    {
        Id = entity.Id,
        CompanyId = entity.CompanyId,
        Name = entity.Name,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        HolidayListId = entity.HolidayListId,
        ResolutionTimeHours = entity.ResolutionTimeHours,
        ResponseTimeHours = entity.ResponseTimeHours,
        IsDefault = entity.IsDefault,
        ApplyOnResolution = entity.ApplyOnResolution,
        IsActive = entity.IsActive,
        CreationTime = entity.CreationTime,
        LastModificationTime = entity.LastModificationTime,
        Priorities = entity.Priorities.Select(p => new ServiceLevelPriorityDto
        {
            Id = p.Id,
            PriorityName = p.PriorityName,
            ResponseTimeHours = p.ResponseTimeHours,
            ResolutionTimeHours = p.ResolutionTimeHours,
            IsDefault = p.IsDefault,
        }).ToList(),
        ServiceDays = entity.ServiceDays.Select(d => new ServiceDayDto
        {
            Id = d.Id,
            DayOfWeek = d.DayOfWeek,
            StartTime = d.StartTime,
            EndTime = d.EndTime,
        }).ToList(),
    };
}
