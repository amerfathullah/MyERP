using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.HumanResources.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Calculates salary proration for employees with approved unpaid leave during a pay period.
/// 
/// Per ERPNext salary_slip.py:
/// - If employee has Leave Without Pay (LWP) during the pay period,
///   salary is prorated: adjusted_salary = gross × (working_days - lwp_days) / working_days
/// - Only approved leave applications of leave types with IsPaidLeave=false count as LWP
/// - Leave that spans across periods is counted proportionally (days within the period only)
/// </summary>
public class UnpaidLeaveProrationService : DomainService
{
    private readonly IRepository<LeaveApplication, Guid> _leaveRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;

    public UnpaidLeaveProrationService(
        IRepository<LeaveApplication, Guid> leaveRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository)
    {
        _leaveRepository = leaveRepository;
        _leaveTypeRepository = leaveTypeRepository;
    }

    /// <summary>
    /// Gets the number of approved unpaid leave days for an employee within a pay period.
    /// </summary>
    public async Task<decimal> GetUnpaidLeaveDaysAsync(
        Guid employeeId, DateTime periodStart, DateTime periodEnd)
    {
        // Get all unpaid leave types
        var leaveTypeQuery = await _leaveTypeRepository.GetQueryableAsync();
        var unpaidLeaveTypeIds = leaveTypeQuery
            .Where(lt => !lt.IsPaidLeave && lt.IsActive)
            .Select(lt => lt.Id)
            .ToList();

        if (!unpaidLeaveTypeIds.Any())
            return 0;

        // Get approved leave applications for this employee that overlap with the pay period
        var leaveQuery = await _leaveRepository.GetQueryableAsync();
        var overlappingLeaves = leaveQuery
            .Where(la => la.EmployeeId == employeeId
                && la.Status == LeaveApplicationStatus.Approved
                && unpaidLeaveTypeIds.Contains(la.LeaveTypeId)
                && la.FromDate <= periodEnd
                && la.ToDate >= periodStart)
            .ToList();

        if (!overlappingLeaves.Any())
            return 0;

        // Calculate days within the pay period for each leave application
        decimal totalUnpaidDays = 0;
        foreach (var leave in overlappingLeaves)
        {
            var effectiveStart = leave.FromDate < periodStart ? periodStart : leave.FromDate;
            var effectiveEnd = leave.ToDate > periodEnd ? periodEnd : leave.ToDate;
            var daysInPeriod = (effectiveEnd - effectiveStart).Days + 1;
            if (daysInPeriod > 0)
                totalUnpaidDays += daysInPeriod;
        }

        return totalUnpaidDays;
    }

    /// <summary>
    /// Prorates a gross salary based on unpaid leave days.
    /// Returns (proratedGross, deductionAmount, unpaidDays).
    /// </summary>
    public static (decimal ProratedGross, decimal Deduction, decimal UnpaidDays) CalculateProration(
        decimal grossSalary, decimal unpaidLeaveDays, int workingDaysInMonth = 30)
    {
        if (unpaidLeaveDays <= 0 || workingDaysInMonth <= 0)
            return (grossSalary, 0, 0);

        // Cap unpaid days at working days (can't deduct more than full salary)
        unpaidLeaveDays = Math.Min(unpaidLeaveDays, workingDaysInMonth);

        decimal dailyRate = grossSalary / workingDaysInMonth;
        decimal deduction = Math.Round(dailyRate * unpaidLeaveDays, 2);
        decimal proratedGross = grossSalary - deduction;

        return (proratedGross, deduction, unpaidLeaveDays);
    }
}
