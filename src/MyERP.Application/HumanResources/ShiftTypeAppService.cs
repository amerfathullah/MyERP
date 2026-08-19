using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class ShiftTypeAppService : ApplicationService, IShiftTypeAppService
{
    private readonly IRepository<ShiftType, Guid> _repository;
    public ShiftTypeAppService(IRepository<ShiftType, Guid> repository) => _repository = repository;

    public async Task<ShiftTypeDto> GetAsync(Guid id) => ObjectMapper.Map<ShiftType, ShiftTypeDto>(await _repository.GetAsync(id));

    public async Task<PagedResultDto<ShiftTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(s => s.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<ShiftTypeDto>(totalCount, items.Select(ObjectMapper.Map<ShiftType, ShiftTypeDto>).ToList());
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<ShiftTypeDto> CreateAsync(CreateShiftTypeDto input)
    {
        if (input.EndTime < input.StartTime)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var shiftType = new ShiftType(GuidGenerator.Create(), input.CompanyId, input.Name, input.StartTime, input.EndTime, CurrentTenant.Id)
        {
            HolidayListId = input.HolidayListId,
        };
        await _repository.InsertAsync(shiftType);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ShiftType", shiftType.Id,
            "Created", shiftType.CompanyId,
            shiftType.Name, "Draft", "Active", CurrentUser.Id,
            $"Shift type '{shiftType.Name}' created ({shiftType.StartTime:hh\\:mm} - {shiftType.EndTime:hh\\:mm})", CurrentTenant.Id));

        return ObjectMapper.Map<ShiftType, ShiftTypeDto>(shiftType);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ShiftTypeDto> UpdateAsync(Guid id, CreateShiftTypeDto input)
    {
        if (input.EndTime < input.StartTime)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        var shiftType = await _repository.GetAsync(id);
        shiftType.Name = input.Name;
        shiftType.StartTime = input.StartTime;
        shiftType.EndTime = input.EndTime;
        shiftType.HolidayListId = input.HolidayListId;
        await _repository.UpdateAsync(shiftType);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "ShiftType", shiftType.Id,
            "Updated", shiftType.CompanyId,
            shiftType.Name, "Active", "Active", CurrentUser.Id,
            $"Shift type '{shiftType.Name}' updated", CurrentTenant.Id));

        return ObjectMapper.Map<ShiftType, ShiftTypeDto>(shiftType);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
