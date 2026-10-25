using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.DomainServices;
using MyERP.HumanResources.DomainServices;
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
        await ValidateUserAccessAsync(input.UserId, null);
        await ValidateUserNotAlreadyLinkedAsync(input.UserId, currentEmployeeId: null);

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

        if (input.ReportsToEmployeeId.HasValue)
        {
            var manager = await _repository.FindAsync(input.ReportsToEmployeeId.Value);
            if (manager != null && manager.Status != EmploymentStatus.Active)
            {
                throw new Volo.Abp.BusinessException("MyERP:HR:007", "Reports To employee must be an Active employee.");
            }
        }

        employee.ReportsToEmployeeId = input.ReportsToEmployeeId;
        employee.UserId = input.UserId;

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
        if (input.ReportsToEmployeeId == id)
        {
            throw new Volo.Abp.BusinessException("MyERP:HR:006", "An employee cannot report to themselves.");
        }

        if (input.ReportsToEmployeeId.HasValue)
        {
            var manager = await _repository.FindAsync(input.ReportsToEmployeeId.Value);
            if (manager != null && manager.Status != EmploymentStatus.Active)
            {
                throw new Volo.Abp.BusinessException("MyERP:HR:007", "Reports To employee must be an Active employee.");
            }
        }

        var employee = await _repository.GetAsync(id);
        await ValidateUserAccessAsync(input.UserId, employee.UserId);
        await ValidateUserNotAlreadyLinkedAsync(input.UserId, currentEmployeeId: id);

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
        employee.ReportsToEmployeeId = input.ReportsToEmployeeId;
        employee.UserId = input.UserId;

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
        var employee = await _repository.GetAsync(id);
        if (employee.UserId.HasValue)
        {
            await ValidateUserAccessAsync(null, employee.UserId);
        }

        var lifecycleManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.EmployeeLifecycleManager>();
        await lifecycleManager.CheckDeletionRulesAsync(id);

        await _repository.DeleteAsync(id);
    }

    /// <summary>
    /// One IdentityUser account should map to at most one Employee — approver-identity checks
    /// (Leave/Expense Claim) resolve "is CurrentUser this employee?" by this link, so a shared
    /// UserId across employees would make that resolution ambiguous.
    /// </summary>
    /// <summary>
    /// Validates that non-admin editors cannot link or unlink User accounts holding admin roles (PR #61894 / commit c80bd8912b).
    /// </summary>
    private async Task ValidateUserAccessAsync(Guid? targetUserId, Guid? existingUserId)
    {
        var editorRoles = CurrentUser.Roles ?? Array.Empty<string>();
        if (UserAccessValidator.IsAdminUser(editorRoles))
        {
            return;
        }

        var userManager = LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Identity.IdentityUserManager>();
        var userIdsToCheck = new List<Guid>();
        if (targetUserId.HasValue) userIdsToCheck.Add(targetUserId.Value);
        if (existingUserId.HasValue && existingUserId.Value != targetUserId) userIdsToCheck.Add(existingUserId.Value);

        foreach (var uid in userIdsToCheck)
        {
            var user = await userManager.FindByIdAsync(uid.ToString());
            if (user != null)
            {
                var targetRoles = await userManager.GetRolesAsync(user);
                if (!UserAccessValidator.CanModifyUserLink(editorRoles, targetRoles))
                {
                    throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                        .WithData("detail", $"You are not permitted to link or unlink administrator user {user.UserName}.");
                }
            }
        }
    }

    private async Task ValidateUserNotAlreadyLinkedAsync(Guid? userId, Guid? currentEmployeeId)
    {
        if (!userId.HasValue)
        {
            return;
        }

        var query = await _repository.GetQueryableAsync();
        var alreadyLinked = query.Any(e => e.UserId == userId.Value && e.Id != currentEmployeeId);
        if (alreadyLinked)
        {
            throw new Volo.Abp.BusinessException("MyERP:HR:008", "This user account is already linked to another employee.");
        }
    }
}

