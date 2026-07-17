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

/// <summary>
/// Manages leave allocations — assigns annual leave balance to employees.
/// HR admins allocate leaves at start of fiscal year; system deducts on approval.
/// </summary>
[Authorize(MyERPPermissions.Employees.Edit)]
public class LeaveAllocationAppService : ApplicationService
{
    private readonly IRepository<LeaveAllocation, Guid> _repository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;

    public LeaveAllocationAppService(
        IRepository<LeaveAllocation, Guid> repository,
        IRepository<LeaveType, Guid> leaveTypeRepository)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
    }

    public async Task<PagedResultDto<LeaveAllocationDto>> GetListAsync(GetLeaveAllocationListDto input)
    {
        var query = await _repository.GetQueryableAsync();

        if (input.EmployeeId.HasValue)
            query = query.Where(a => a.EmployeeId == input.EmployeeId.Value);
        if (input.CompanyId.HasValue)
            query = query.Where(a => a.CompanyId == input.CompanyId.Value);
        if (input.LeaveTypeId.HasValue)
            query = query.Where(a => a.LeaveTypeId == input.LeaveTypeId.Value);

        var totalCount = query.Count();
        var items = query
            .OrderByDescending(a => a.FromDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<LeaveAllocationDto>(
            totalCount,
            items.Select(ObjectMapper.Map<LeaveAllocation, LeaveAllocationDto>).ToList());
    }

    public async Task<LeaveAllocationDto> GetAsync(Guid id)
    {
        var alloc = await _repository.GetAsync(id);
        return ObjectMapper.Map<LeaveAllocation, LeaveAllocationDto>(alloc);
    }

    /// <summary>
    /// Get current balance for an employee's leave type (for UI display).
    /// </summary>
    public async Task<decimal> GetBalanceAsync(Guid employeeId, Guid leaveTypeId, DateTime asOfDate)
    {
        var query = await _repository.GetQueryableAsync();
        var allocation = query.FirstOrDefault(a =>
            a.EmployeeId == employeeId
            && a.LeaveTypeId == leaveTypeId
            && a.FromDate <= asOfDate
            && a.ToDate >= asOfDate);

        return allocation?.Balance ?? 0m;
    }

    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<LeaveAllocationDto> CreateAsync(CreateLeaveAllocationDto input)
    {
        var alloc = new LeaveAllocation(
            GuidGenerator.Create(),
            input.CompanyId,
            input.EmployeeId,
            input.LeaveTypeId,
            input.FromDate,
            input.ToDate,
            input.TotalLeavesAllocated,
            CurrentTenant.Id)
        {
            CarryForwardDays = input.CarryForwardDays,
        };

        await _repository.InsertAsync(alloc, autoSave: true);
        return ObjectMapper.Map<LeaveAllocation, LeaveAllocationDto>(alloc);
    }

    /// <summary>
    /// Bulk-allocate leaves for all active employees of a company for a leave type.
    /// Common operation at start of fiscal year.
    /// </summary>
    [Authorize(MyERPPermissions.Employees.Create)]
    public async Task<int> BulkAllocateAsync(BulkLeaveAllocationDto input)
    {
        var employeeRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Employee, Guid>>();
        var empQuery = await employeeRepo.GetQueryableAsync();
        var activeEmployees = empQuery
            .Where(e => e.CompanyId == input.CompanyId && e.Status == EmploymentStatus.Active)
            .Select(e => e.Id)
            .ToList();

        var count = 0;
        foreach (var empId in activeEmployees)
        {
            // Check for existing allocation in same period
            var existingQuery = await _repository.GetQueryableAsync();
            var exists = existingQuery.Any(a =>
                a.EmployeeId == empId
                && a.LeaveTypeId == input.LeaveTypeId
                && a.FromDate == input.FromDate
                && a.ToDate == input.ToDate);

            if (exists) continue; // Skip already allocated

            var alloc = new LeaveAllocation(
                GuidGenerator.Create(),
                input.CompanyId,
                empId,
                input.LeaveTypeId,
                input.FromDate,
                input.ToDate,
                input.TotalLeavesPerEmployee,
                CurrentTenant.Id);

            await _repository.InsertAsync(alloc);
            count++;
        }

        return count;
    }

    [Authorize(MyERPPermissions.Employees.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var alloc = await _repository.GetAsync(id);
        if (alloc.LeavesUsed > 0)
        {
            throw new Volo.Abp.BusinessException("MyERP:14002")
                .WithData("used", alloc.LeavesUsed);
        }
        await _repository.DeleteAsync(id);
    }


}

// DTOs

public class LeaveAllocationDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalLeavesAllocated { get; set; }
    public decimal CarryForwardDays { get; set; }
    public decimal LeavesUsed { get; set; }
    public decimal Balance { get; set; }
}

public class CreateLeaveAllocationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid EmployeeId { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeavesAllocated { get; set; }
    public decimal CarryForwardDays { get; set; }
}

public class BulkLeaveAllocationDto
{
    [Required] public Guid CompanyId { get; set; }
    [Required] public Guid LeaveTypeId { get; set; }
    [Required] public DateTime FromDate { get; set; }
    [Required] public DateTime ToDate { get; set; }
    [Required] public decimal TotalLeavesPerEmployee { get; set; }
}

public class GetLeaveAllocationListDto : PagedAndSortedResultRequestDto
{
    public Guid? EmployeeId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? LeaveTypeId { get; set; }
}
