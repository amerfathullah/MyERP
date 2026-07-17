using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using MyERP.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources;

[Authorize(MyERPPermissions.Employees.Default)]
public class LeaveAppService : ApplicationService
{
    private readonly IRepository<LeaveApplication, Guid> _leaveRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
    private readonly IRepository<LeaveAllocation, Guid> _allocationRepository;

    public LeaveAppService(
        IRepository<LeaveApplication, Guid> leaveRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository,
        IRepository<LeaveAllocation, Guid> allocationRepository)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _allocationRepository = allocationRepository;
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
        // Per DO-NOT: "Allow leave application with overlapping dates for same employee"
        var existingQuery = await _leaveRepository.GetQueryableAsync();
        var hasOverlap = existingQuery.Any(l =>
            l.EmployeeId == input.EmployeeId
            && l.Status != HumanResources.Entities.LeaveApplicationStatus.Cancelled
            && l.Status != HumanResources.Entities.LeaveApplicationStatus.Rejected
            && l.FromDate <= input.ToDate
            && l.ToDate >= input.FromDate);

        if (hasOverlap)
        {
            throw new Volo.Abp.BusinessException("MyERP:14004")
                .WithData("fromDate", input.FromDate)
                .WithData("toDate", input.ToDate);
        }

        var leave = new LeaveApplication(
            GuidGenerator.Create(), input.CompanyId, input.EmployeeId, input.LeaveTypeId,
            input.FromDate, input.ToDate, input.TotalLeaveDays, CurrentTenant.Id)
        {
            EmployeeName = input.EmployeeName,
            LeaveTypeName = input.LeaveTypeName,
            HalfDay = input.HalfDay,
            Reason = input.Reason,
            LeaveApproverId = input.LeaveApproverId,
        };
        await _leaveRepository.InsertAsync(leave);
        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> ApproveAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);

        // Check sufficient balance before approving
        var allocQuery = await _allocationRepository.GetQueryableAsync();
        var allocation = allocQuery.FirstOrDefault(a =>
            a.EmployeeId == leave.EmployeeId
            && a.LeaveTypeId == leave.LeaveTypeId
            && a.FromDate <= leave.FromDate
            && a.ToDate >= leave.ToDate);

        if (allocation != null && allocation.Balance < leave.TotalLeaveDays)
        {
            throw new Volo.Abp.BusinessException("MyERP:14001")
                .WithData("leaveType", leave.LeaveTypeName ?? "Leave")
                .WithData("requested", leave.TotalLeaveDays)
                .WithData("available", allocation.Balance);
        }

        leave.Approve();

        // Deduct from leave allocation balance
        if (allocation != null)
        {
            allocation.DeductLeave(leave.TotalLeaveDays);
            await _allocationRepository.UpdateAsync(allocation);
        }

        await _leaveRepository.UpdateAsync(leave);
        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> RejectAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        leave.Reject();
        await _leaveRepository.UpdateAsync(leave);
        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }

    [Authorize(MyERPPermissions.Employees.Edit)]
    public async Task<LeaveApplicationDto> CancelAsync(Guid id)
    {
        var leave = await _leaveRepository.GetAsync(id);
        var wasApproved = leave.Status == LeaveApplicationStatus.Approved;
        leave.Cancel();

        // Restore leave balance if previously approved
        if (wasApproved)
        {
            var allocQuery = await _allocationRepository.GetQueryableAsync();
            var allocation = allocQuery.FirstOrDefault(a =>
                a.EmployeeId == leave.EmployeeId
                && a.LeaveTypeId == leave.LeaveTypeId
                && a.FromDate <= leave.FromDate
                && a.ToDate >= leave.ToDate);

            if (allocation != null)
            {
                allocation.RestoreLeave(leave.TotalLeaveDays);
                await _allocationRepository.UpdateAsync(allocation);
            }
        }

        await _leaveRepository.UpdateAsync(leave);
        return ObjectMapper.Map<LeaveApplication, LeaveApplicationDto>(leave);
    }


}

// DTOs

public class LeaveTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal MaxDaysAllowed { get; set; }
    public bool IsPaidLeave { get; set; }
    public bool AllowCarryForward { get; set; }
    public bool RequiresApproval { get; set; }
}

public class CreateLeaveTypeDto
{
    [Required][StringLength(100)] public string Name { get; set; } = null!;
    [Required] public decimal MaxDaysAllowed { get; set; }
    public bool IsPaidLeave { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;
    public bool AllowCarryForward { get; set; }
    public decimal MaxCarryForwardDays { get; set; }
}

public class LeaveApplicationDto : AuditedEntityDto<Guid>
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalLeaveDays { get; set; }
    public bool HalfDay { get; set; }
    public string? Reason { get; set; }
    public LeaveApplicationStatus Status { get; set; }
}

public class CreateLeaveApplicationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeaveDays { get; set; }
    public bool HalfDay { get; set; }
    [StringLength(1000)] public string? Reason { get; set; }
    public Guid? LeaveApproverId { get; set; }
}

public class GetLeaveListDto : PagedAndSortedResultRequestDto
{
    public Guid? EmployeeId { get; set; }
    public LeaveApplicationStatus? Status { get; set; }
}
