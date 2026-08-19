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
        var shiftType = new ShiftType(GuidGenerator.Create(), input.CompanyId, input.Name, input.StartTime, input.EndTime, CurrentTenant.Id)
        {
            HolidayListId = input.HolidayListId,
        };
        await _repository.InsertAsync(shiftType);
        return ObjectMapper.Map<ShiftType, ShiftTypeDto>(shiftType);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ShiftTypeDto> UpdateAsync(Guid id, CreateShiftTypeDto input)
    {
        var shiftType = await _repository.GetAsync(id);
        shiftType.Name = input.Name;
        shiftType.StartTime = input.StartTime;
        shiftType.EndTime = input.EndTime;
        shiftType.HolidayListId = input.HolidayListId;
        await _repository.UpdateAsync(shiftType);
        return ObjectMapper.Map<ShiftType, ShiftTypeDto>(shiftType);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
