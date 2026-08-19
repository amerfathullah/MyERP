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
public class AttendanceAppService : ApplicationService, IAttendanceAppService
{
    private readonly IRepository<Attendance, Guid> _repository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public AttendanceAppService(IRepository<Attendance, Guid> repository, IRepository<Employee, Guid> employeeRepository)
    {
        _repository = repository;
        _employeeRepository = employeeRepository;
    }

    private async Task<AttendanceDto> MapWithEmployeeNameAsync(Attendance attendance)
    {
        var dto = ObjectMapper.Map<Attendance, AttendanceDto>(attendance);
        var employee = await _employeeRepository.FindAsync(attendance.EmployeeId);
        dto.EmployeeName = employee?.FullName;
        return dto;
    }

    public async Task<AttendanceDto> GetAsync(Guid id)
    {
        var attendance = await _repository.GetAsync(id);
        return await MapWithEmployeeNameAsync(attendance);
    }

    public async Task<PagedResultDto<AttendanceDto>> GetListAsync(GetAttendanceListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == input.CompanyId.Value);
        if (input.EmployeeId.HasValue)
            query = query.Where(a => a.EmployeeId == input.EmployeeId.Value);
        if (input.FromDate.HasValue)
            query = query.Where(a => a.Date >= input.FromDate.Value);
        if (input.ToDate.HasValue)
            query = query.Where(a => a.Date <= input.ToDate.Value);
        if (input.Status.HasValue)
            query = query.Where(a => a.Status == input.Status.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(a => a.Date)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        var dtos = new System.Collections.Generic.List<AttendanceDto>();
        foreach (var item in items)
            dtos.Add(await MapWithEmployeeNameAsync(item));

        return new PagedResultDto<AttendanceDto>(totalCount, dtos);
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<AttendanceDto> CreateAsync(CreateAttendanceDto input)
    {
        var attendance = new Attendance(GuidGenerator.Create(), input.CompanyId, input.EmployeeId, input.Date, input.Status, CurrentTenant.Id)
        {
            ShiftTypeId = input.ShiftTypeId,
            InTime = input.InTime,
            OutTime = input.OutTime,
        };
        await _repository.InsertAsync(attendance);
        return await MapWithEmployeeNameAsync(attendance);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<AttendanceDto> UpdateAsync(Guid id, CreateAttendanceDto input)
    {
        var attendance = await _repository.GetAsync(id);
        attendance.EmployeeId = input.EmployeeId;
        attendance.Date = input.Date.Date;
        attendance.Status = input.Status;
        attendance.ShiftTypeId = input.ShiftTypeId;
        attendance.InTime = input.InTime;
        attendance.OutTime = input.OutTime;
        await _repository.UpdateAsync(attendance);
        return await MapWithEmployeeNameAsync(attendance);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id) => await _repository.DeleteAsync(id);
}
