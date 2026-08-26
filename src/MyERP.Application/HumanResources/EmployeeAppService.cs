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
        return ObjectMapper.Map<Employee, EmployeeDto>(employee);
    }

    public async Task<PagedResultDto<EmployeeDto>> GetListAsync(GetEmployeeListDto input)
    {
        var filter = input.Filter;
        var queryable = await _repository.GetQueryableAsync();

        if (input.CompanyId.HasValue)
            queryable = queryable.Where(e => e.CompanyId == input.CompanyId.Value);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            queryable = queryable.Where(e =>
                e.FirstName.Contains(filter)
                || (e.LastName != null && e.LastName.Contains(filter))
                || e.EmployeeId.Contains(filter)
                || (e.Department != null && e.Department.Contains(filter)));
        }

        var totalCount = queryable.Count();
        var employees = queryable
            .OrderBy(e => e.FirstName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<EmployeeDto>(
            totalCount,
            employees.Select(x => ObjectMapper.Map<Employee, EmployeeDto>(x)).ToList());
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
        employee.Gender = input.Gender;
        employee.EpfNumber = input.EpfNumber;
        employee.SocsoNumber = input.SocsoNumber;
        employee.TaxNumber = input.TaxNumber;

        await _repository.InsertAsync(employee, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Employee", employee.Id,
            "Created", employee.CompanyId,
            $"{employee.FirstName} {employee.LastName}".Trim(), "Draft", "Active", CurrentUser.Id,
            $"Employee '{employee.FirstName} {employee.LastName}' ({employee.EmployeeId}) created", CurrentTenant.Id));

        return ObjectMapper.Map<Employee, EmployeeDto>(employee);
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
        employee.Gender = input.Gender;
        employee.EpfNumber = input.EpfNumber;
        employee.SocsoNumber = input.SocsoNumber;
        employee.TaxNumber = input.TaxNumber;

        await _repository.UpdateAsync(employee, autoSave: true);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "Employee", employee.Id,
            "Updated", employee.CompanyId,
            $"{employee.FirstName} {employee.LastName}".Trim(), "Active", "Active", CurrentUser.Id,
            $"Employee '{employee.FirstName} {employee.LastName}' ({employee.EmployeeId}) updated", CurrentTenant.Id));

        return ObjectMapper.Map<Employee, EmployeeDto>(employee);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<EmployeeDto> ChangeStatusAsync(Guid id, ChangeEmployeeStatusDto input)
    {
        var employee = await _repository.GetAsync(id);

        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.EmployeeLifecycleManager>();
        await lifecycleManager.ChangeStatusAsync(employee, input.Status, input.DateOfResignation);

        return ObjectMapper.Map<Employee, EmployeeDto>(employee);
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        // Per EmployeeLifecycleManager's own class doc: "Cannot delete with linked transactions
        // (leave, salary, attendance)" — CheckDeletionRulesAsync already implemented this and had
        // zero callers anywhere, so deletion was previously unconditional.
        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.EmployeeLifecycleManager>();
        await lifecycleManager.CheckDeletionRulesAsync(id);

        await _repository.DeleteAsync(id);
    }
}

