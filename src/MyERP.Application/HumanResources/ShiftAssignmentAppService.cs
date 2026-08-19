using System;
using System.Collections.Generic;
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
public class ShiftAssignmentAppService : ApplicationService, IShiftAssignmentAppService
{
    private readonly IRepository<ShiftAssignment, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<ShiftType, Guid> _shiftTypeRepository;

    public ShiftAssignmentAppService(
        IRepository<ShiftAssignment, Guid> repository,
        IRepository<Employee, Guid> employeeRepository,
        IRepository<ShiftType, Guid> shiftTypeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
        _shiftTypeRepository = shiftTypeRepository;
    }

    private async Task<ShiftAssignmentDto> MapWithNamesAsync(ShiftAssignment assignment)
    {
        var dto = ObjectMapper.Map<ShiftAssignment, ShiftAssignmentDto>(assignment);
        var employee = await _employeeRepository.FindAsync(assignment.EmployeeId);
        dto.EmployeeName = employee?.FullName;
        var shiftType = await _shiftTypeRepository.FindAsync(assignment.ShiftTypeId);
        dto.ShiftTypeName = shiftType?.Name;
        return dto;
    }

    public async Task<ShiftAssignmentDto> GetAsync(Guid id) => await MapWithNamesAsync(await _repository.GetAsync(id));

    public async Task<PagedResultDto<ShiftAssignmentDto>> GetListAsync(GetShiftAssignmentListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == input.CompanyId.Value);
        if (input.EmployeeId.HasValue)
            query = query.Where(a => a.EmployeeId == input.EmployeeId.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.StartDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        var dtos = new List<ShiftAssignmentDto>();
        foreach (var item in items)
            dtos.Add(await MapWithNamesAsync(item));

        return new PagedResultDto<ShiftAssignmentDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<ShiftAssignmentDto> CreateAsync(CreateShiftAssignmentDto input)
    {
        var assignment = new ShiftAssignment(GuidGenerator.Create(), input.CompanyId, input.EmployeeId, input.ShiftTypeId, input.StartDate, CurrentTenant.Id)
        {
            EndDate = input.EndDate,
        };
        await _repository.InsertAsync(assignment);
        return await MapWithNamesAsync(assignment);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<ShiftAssignmentDto> UpdateAsync(Guid id, CreateShiftAssignmentDto input)
    {
        var assignment = await _repository.GetAsync(id);
        assignment.EmployeeId = input.EmployeeId;
        assignment.ShiftTypeId = input.ShiftTypeId;
        assignment.StartDate = input.StartDate.Date;
        assignment.EndDate = input.EndDate;
        await _repository.UpdateAsync(assignment);
        return await MapWithNamesAsync(assignment);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
