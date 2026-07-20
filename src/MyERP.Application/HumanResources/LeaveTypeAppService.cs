using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

public class LeaveTypeDetailDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public int MaxDaysAllowed { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public bool AllowCarryForward { get; set; }
    public int MaxCarryForwardDays { get; set; }
    public int CarryForwardExpiryMonths { get; set; }
    public bool IsPaidLeave { get; set; }
    public bool IncludeHolidays { get; set; }
    public bool AllowNegativeBalance { get; set; }
}

public class CreateUpdateLeaveTypeDto
{
    public string Name { get; set; } = null!;
    public int MaxDaysAllowed { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool AllowCarryForward { get; set; }
    public int MaxCarryForwardDays { get; set; }
    public int CarryForwardExpiryMonths { get; set; }
    public bool IsPaidLeave { get; set; } = true;
    public bool IncludeHolidays { get; set; }
    public bool AllowNegativeBalance { get; set; }
}

[Authorize(MyERPPermissions.Employees.Default)]
public class LeaveTypeAppService : ApplicationService
{
    private readonly IRepository<LeaveType, Guid> _repository;

    public LeaveTypeAppService(IRepository<LeaveType, Guid> repository)
        => _repository = repository;

    public async Task<PagedResultDto<LeaveTypeDetailDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _repository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query.OrderBy(t => t.Name)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        return new PagedResultDto<LeaveTypeDetailDto>(totalCount,
            items.Select(ObjectMapper.Map<LeaveType, LeaveTypeDetailDto>).ToList());
    }

    public async Task<LeaveTypeDetailDto> GetAsync(Guid id)
        => ObjectMapper.Map<LeaveType, LeaveTypeDetailDto>(await _repository.GetAsync(id));

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LeaveTypeDetailDto> CreateAsync(CreateUpdateLeaveTypeDto input)
    {
        var leaveType = new LeaveType(GuidGenerator.Create(), input.Name, (decimal)input.MaxDaysAllowed, CurrentTenant.Id)
        {
            RequiresApproval = input.RequiresApproval,
            AllowCarryForward = input.AllowCarryForward,
            MaxCarryForwardDays = (decimal)input.MaxCarryForwardDays,
            CarryForwardExpiryMonths = input.CarryForwardExpiryMonths,
            IsPaidLeave = input.IsPaidLeave,
            IncludeHolidays = input.IncludeHolidays,
            AllowNegativeBalance = input.AllowNegativeBalance,
        };
        await _repository.InsertAsync(leaveType);
        return ObjectMapper.Map<LeaveType, LeaveTypeDetailDto>(leaveType);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveTypeDetailDto> UpdateAsync(Guid id, CreateUpdateLeaveTypeDto input)
    {
        var leaveType = await _repository.GetAsync(id);
        leaveType.Name = input.Name;
        leaveType.MaxDaysAllowed = (decimal)input.MaxDaysAllowed;
        leaveType.RequiresApproval = input.RequiresApproval;
        leaveType.AllowCarryForward = input.AllowCarryForward;
        leaveType.MaxCarryForwardDays = (decimal)input.MaxCarryForwardDays;
        leaveType.CarryForwardExpiryMonths = input.CarryForwardExpiryMonths;
        leaveType.IsPaidLeave = input.IsPaidLeave;
        leaveType.IncludeHolidays = input.IncludeHolidays;
        leaveType.AllowNegativeBalance = input.AllowNegativeBalance;
        await _repository.UpdateAsync(leaveType);
        return ObjectMapper.Map<LeaveType, LeaveTypeDetailDto>(leaveType);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
