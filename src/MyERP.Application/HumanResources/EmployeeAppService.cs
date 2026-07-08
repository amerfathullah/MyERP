using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class EmployeeAppService : ApplicationService, IEmployeeAppService
{
    private readonly IRepository<Employee, Guid> _repository;
    private readonly IDocumentNumberGenerator _numberGenerator;

    public EmployeeAppService(
        IRepository<Employee, Guid> repository,
        IDocumentNumberGenerator numberGenerator)
    {
        _repository = repository;
        _numberGenerator = numberGenerator;
    }

    public async Task<EmployeeDto> GetAsync(Guid id)
    {
        var employee = await _repository.GetAsync(id);
        return MapToDto(employee);
    }

    public async Task<PagedResultDto<EmployeeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await _repository.GetCountAsync();
        var employees = await _repository.GetPagedListAsync(
            input.SkipCount, input.MaxResultCount, input.Sorting ?? "FirstName ASC");

        return new PagedResultDto<EmployeeDto>(
            totalCount,
            employees.Select(MapToDto).ToList());
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<EmployeeDto> CreateAsync(CreateUpdateEmployeeDto input)
    {
        var employeeId = await _numberGenerator.GenerateAsync("Employee", input.CompanyId);

        var employee = new Employee(
            GuidGenerator.Create(),
            input.CompanyId,
            employeeId,
            input.FirstName);

        employee.LastName = input.LastName;
        employee.DateOfBirth = input.DateOfBirth;
        employee.DateOfJoining = input.DateOfJoining;
        employee.Phone = input.Phone;
        employee.Email = input.Email;
        employee.Designation = input.Designation;
        employee.Department = input.Department;
        employee.EpfNumber = input.EpfNumber;
        employee.SocsoNumber = input.SocsoNumber;
        employee.TaxNumber = input.TaxNumber;

        await _repository.InsertAsync(employee, autoSave: true);
        return MapToDto(employee);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<EmployeeDto> UpdateAsync(Guid id, CreateUpdateEmployeeDto input)
    {
        var employee = await _repository.GetAsync(id);

        employee.FirstName = input.FirstName;
        employee.LastName = input.LastName;
        employee.DateOfBirth = input.DateOfBirth;
        employee.DateOfJoining = input.DateOfJoining;
        employee.Phone = input.Phone;
        employee.Email = input.Email;
        employee.Designation = input.Designation;
        employee.Department = input.Department;
        employee.EpfNumber = input.EpfNumber;
        employee.SocsoNumber = input.SocsoNumber;
        employee.TaxNumber = input.TaxNumber;

        await _repository.UpdateAsync(employee, autoSave: true);
        return MapToDto(employee);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static EmployeeDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        CompanyId = e.CompanyId,
        EmployeeId = e.EmployeeId,
        FirstName = e.FirstName,
        LastName = e.LastName,
        FullName = e.FullName,
        DateOfBirth = e.DateOfBirth,
        DateOfJoining = e.DateOfJoining,
        DateOfResignation = e.DateOfResignation,
        Citizenship = e.Citizenship.ToString(),
        Phone = e.Phone,
        Email = e.Email,
        Designation = e.Designation,
        Department = e.Department,
        Status = e.Status.ToString(),
        CreationTime = e.CreationTime,
        LastModificationTime = e.LastModificationTime,
    };
}
