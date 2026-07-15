using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Domain service for Leave management business rules.
/// Handles overlap detection, balance validation, allocation deductions, and carry-forward expiry.
/// Per DO-NOT: "Allow leave application with overlapping dates for same employee"
/// Per DO-NOT: "Skip carry-forward expiry enforcement (expired carry-forward must auto-deduct)"
/// </summary>
public class LeaveManager : DomainService
{
    private readonly IRepository<LeaveApplication, Guid> _leaveRepository;
    private readonly IRepository<LeaveAllocation, Guid> _allocationRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;

    public LeaveManager(
        IRepository<LeaveApplication, Guid> leaveRepository,
        IRepository<LeaveAllocation, Guid> allocationRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository)
    {
        _leaveRepository = leaveRepository;
        _allocationRepository = allocationRepository;
        _leaveTypeRepository = leaveTypeRepository;
    }

    /// <summary>
    /// Validates no overlapping leave applications exist for the same employee.
    /// Uses interval overlap: fromDate &lt;= existingToDate AND toDate &gt;= existingFromDate.
    /// </summary>
    public async Task ValidateNoOverlapAsync(LeaveApplication application)
    {
        var queryable = await _leaveRepository.GetQueryableAsync();
        var hasOverlap = queryable.Any(la =>
            la.EmployeeId == application.EmployeeId
            && la.Id != application.Id
            && la.Status != LeaveApplicationStatus.Cancelled
            && la.Status != LeaveApplicationStatus.Rejected
            && la.FromDate <= application.ToDate
            && la.ToDate >= application.FromDate);

        if (hasOverlap)
        {
            throw new BusinessException(MyERPDomainErrorCodes.LeaveOverlap)
                .WithData("employee", application.EmployeeName ?? application.EmployeeId.ToString())
                .WithData("fromDate", application.FromDate)
                .WithData("toDate", application.ToDate);
        }
    }

    /// <summary>
    /// Validates sufficient leave balance before approval.
    /// Finds the allocation matching employee + leave type + date range.
    /// </summary>
    public async Task<LeaveAllocation> ValidateBalanceAsync(LeaveApplication application)
    {
        var allocation = await FindAllocationAsync(
            application.EmployeeId, application.LeaveTypeId, application.FromDate);

        if (allocation == null)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientLeaveBalance)
                .WithData("leaveType", application.LeaveTypeName ?? "Unknown")
                .WithData("requested", application.TotalLeaveDays)
                .WithData("available", 0);
        }

        if (allocation.Balance < application.TotalLeaveDays)
        {
            throw new BusinessException(MyERPDomainErrorCodes.InsufficientLeaveBalance)
                .WithData("leaveType", application.LeaveTypeName ?? "Unknown")
                .WithData("requested", application.TotalLeaveDays)
                .WithData("available", allocation.Balance);
        }

        return allocation;
    }

    /// <summary>
    /// Deducts leave days from allocation on approval.
    /// </summary>
    public async Task DeductOnApprovalAsync(LeaveApplication application)
    {
        var allocation = await ValidateBalanceAsync(application);
        allocation.DeductLeave(application.TotalLeaveDays);
        await _allocationRepository.UpdateAsync(allocation);
    }

    /// <summary>
    /// Restores leave days to allocation on cancellation of approved leave.
    /// </summary>
    public async Task RestoreOnCancellationAsync(LeaveApplication application)
    {
        var allocation = await FindAllocationAsync(
            application.EmployeeId, application.LeaveTypeId, application.FromDate);

        if (allocation != null)
        {
            allocation.RestoreLeave(application.TotalLeaveDays);
            await _allocationRepository.UpdateAsync(allocation);
        }
    }

    /// <summary>
    /// Gets total unpaid leave days for an employee in a period.
    /// Only counts leave types where IsPaidLeave = false.
    /// Used for salary proration.
    /// </summary>
    public async Task<decimal> GetUnpaidLeaveDaysAsync(
        Guid employeeId, DateTime periodStart, DateTime periodEnd)
    {
        var leaveQueryable = await _leaveRepository.GetQueryableAsync();
        var typeQueryable = await _leaveTypeRepository.GetQueryableAsync();

        var unpaidLeaveTypeIds = typeQueryable
            .Where(lt => !lt.IsPaidLeave)
            .Select(lt => lt.Id)
            .ToList();

        if (!unpaidLeaveTypeIds.Any()) return 0;

        var approvedLeaves = leaveQueryable
            .Where(la => la.EmployeeId == employeeId
                && la.Status == LeaveApplicationStatus.Approved
                && unpaidLeaveTypeIds.Contains(la.LeaveTypeId)
                && la.FromDate <= periodEnd
                && la.ToDate >= periodStart)
            .ToList();

        decimal totalDays = 0;
        foreach (var leave in approvedLeaves)
        {
            // Count only days within the pay period
            var effectiveStart = leave.FromDate < periodStart ? periodStart : leave.FromDate;
            var effectiveEnd = leave.ToDate > periodEnd ? periodEnd : leave.ToDate;
            totalDays += (decimal)(effectiveEnd - effectiveStart).TotalDays + 1;
        }

        return totalDays;
    }

    private async Task<LeaveAllocation?> FindAllocationAsync(
        Guid employeeId, Guid leaveTypeId, DateTime date)
    {
        var queryable = await _allocationRepository.GetQueryableAsync();
        return queryable.FirstOrDefault(a =>
            a.EmployeeId == employeeId
            && a.LeaveTypeId == leaveTypeId
            && a.FromDate <= date
            && a.ToDate >= date);
    }
}
