using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class LeaveAppService : ApplicationService, ILeaveAppService
{
    private readonly IRepository<LeaveApplication, Guid> _leaveRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
    private readonly IRepository<LeaveAllocation, Guid> _allocationRepository;
    private readonly IRepository<Employee, Guid> _employeeRepository;

    public LeaveAppService(
        IRepository<LeaveApplication, Guid> leaveRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository,
        IRepository<LeaveAllocation, Guid> allocationRepository,
        IRepository<Employee, Guid> employeeRepository)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _allocationRepository = allocationRepository;
        _employeeRepository = employeeRepository;
    }

    // Leave Types

    public async Task<List<LeaveTypeDto>> GetLeaveTypesAsync()
    {
        var types = await _leaveTypeRepository.GetListAsync();
        return types.Where(t => t.IsActive).OrderBy(t => t.Name)
            .Select(ObjectMapper.Map<LeaveType, LeaveTypeDto>).ToList();
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto input)
    {
        var type = new LeaveType(GuidGenerator.Create(), input.Name, input.MaxDaysAllowed, CurrentTenant.Id)
        {
            IsPaidLeave = input.IsPaidLeave,
            RequiresApproval = input.RequiresApproval,
            AllowCarryForward = input.AllowCarryForward,
            MaxCarryForwardDays = input.MaxCarryForwardDays,
        };
        await _leaveTypeRepository.InsertAsync(type);
        return ObjectMapper.Map<LeaveType, LeaveTypeDto>(type);
    }

    // Leave Applications

    public async Task<LeaveApplicationDto> GetAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    public async Task<PagedResultDto<LeaveApplicationDto>> GetListAsync(GetLeaveListDto input)
    {
        var query = await _leaveRepository.GetQueryableAsync();
        if (input.EmployeeId.HasValue)
            query = query.Where(l => l.EmployeeId == input.EmployeeId.Value);
        if (input.Status.HasValue)
            query = query.Where(l => l.Status == input.Status.Value);

        var totalCount = query.Count();
        var items = query.OrderByDescending(l => l.FromDate)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<LeaveApplicationDto>(totalCount, items.Select(ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>).ToList());
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LeaveApplicationDto> ApplyAsync(CreateLeaveApplicationDto input)
    {
        if (input.ToDate < input.FromDate)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.InvalidDateRange);
        }

        if (input.TotalLeaveDays <= 0)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.AmountMustBePositive)
                .WithData("field", "TotalLeaveDays");
        }

        // Per ERPNext leave_application.py: leave_approver defaults from the employee's reporting
        // manager when not explicitly set.
        var leaveApproverId = input.LeaveApproverId;
        if (!leaveApproverId.HasValue)
        {
            var applicant = await _employeeRepository.FindAsync(input.EmployeeId);
            leaveApproverId = applicant?.ReportsToEmployeeId;
        }

        var leave = new LeaveApplication(
            GuidGenerator.Create(), input.CompanyId, input.EmployeeId, input.LeaveTypeId,
            input.FromDate, input.ToDate, input.TotalLeaveDays, CurrentTenant.Id)
        {
            EmployeeName = input.EmployeeName,
            LeaveTypeName = input.LeaveTypeName,
            HalfDay = input.HalfDay,
            Reason = input.Reason,
            LeaveApproverId = leaveApproverId,
        };

        // Per DO-NOT: "Allow leave application with overlapping dates for same employee"
        // Delegate to LeaveManager for proper domain-level overlap detection
        var leaveManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.LeaveManager>();
        await leaveManager.ValidateNoOverlapAsync(leave);

        await _leaveRepository.InsertAsync(leave);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LeaveApplication", leave.Id,
            "Applied", leave.CompanyId,
            leave.EmployeeName ?? leave.EmployeeId.ToString(), "Draft", "Open", CurrentUser.Id,
            $"Leave Application for {leave.EmployeeName} ({leave.TotalLeaveDays} days from {leave.FromDate:yyyy-MM-dd} to {leave.ToDate:yyyy-MM-dd}) applied", CurrentTenant.Id));

        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> ApproveAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        await ValidateActingUserIsApproverAsync(leave);

        // Delegate balance validation + deduction to LeaveManager (DDD pattern)
        var leaveManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.LeaveManager>();
        await leaveManager.DeductOnApprovalAsync(leave);

        leave.Approve();
        await _leaveRepository.UpdateAsync(leave);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LeaveApplication", leave.Id,
            "Approved", leave.CompanyId,
            leave.EmployeeName ?? leave.EmployeeId.ToString(), "Open", "Approved", CurrentUser.Id,
            $"Leave Application for {leave.EmployeeName} approved", CurrentTenant.Id));

        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> RejectAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        await ValidateActingUserIsApproverAsync(leave);
        leave.Reject();
        await _leaveRepository.UpdateAsync(leave);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LeaveApplication", leave.Id,
            "Rejected", leave.CompanyId,
            leave.EmployeeName ?? leave.EmployeeId.ToString(), "Open", "Rejected", CurrentUser.Id,
            $"Leave Application for {leave.EmployeeName} rejected", CurrentTenant.Id));

        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> CancelAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        var wasApproved = leave.Status == LeaveApplicationStatus.Approved;
        leave.Cancel();

        // Restore leave balance if previously approved (delegate to LeaveManager)
        if (wasApproved)
        {
            var leaveManager = LazyServiceProvider.LazyGetRequiredService<DomainServices.LeaveManager>();
            await leaveManager.RestoreOnCancellationAsync(leave);
        }

        await _leaveRepository.UpdateAsync(leave);

        var activityLogRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Core.Entities.DocumentActivityLog, Guid>>();
        await activityLogRepo.InsertAsync(new Core.Entities.DocumentActivityLog(
            GuidGenerator.Create(), "LeaveApplication", leave.Id,
            "Cancelled", leave.CompanyId,
            leave.EmployeeName ?? leave.EmployeeId.ToString(), wasApproved ? "Approved" : "Open", "Cancelled", CurrentUser.Id,
            $"Leave Application for {leave.EmployeeName} cancelled", CurrentTenant.Id));

        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    /// <summary>
    /// Per ERPNext leave_application.py::validate_leave_approver: only the designated approver
    /// may act. Enforced only once BOTH an approver is resolved on the application AND that
    /// approver Employee has a linked user account — orgs that haven't set up ReportsTo/UserId
    /// yet keep today's permission-only gate rather than being silently locked out.
    /// </summary>
    private async Task ValidateActingUserIsApproverAsync(LeaveApplication leave)
    {
        if (!leave.LeaveApproverId.HasValue)
        {
            return;
        }

        var approver = await _employeeRepository.FindAsync(leave.LeaveApproverId.Value);
        if (approver?.UserId == null)
        {
            return;
        }

        if (approver.UserId != CurrentUser.Id)
        {
            throw new BusinessException("MyERP:HR:007", "Only the designated leave approver may approve or reject this application.");
        }
    }
}
