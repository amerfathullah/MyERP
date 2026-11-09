using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core;
using MyERP.HumanResources.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.HumanResources.DomainServices;

/// <summary>
/// Domain service for Employee lifecycle rules.
/// Handles onboarding, status transitions, and validation of linked documents before deletion.
/// Per SKILL employee: Cannot delete with linked transactions (leave, salary, attendance)
/// </summary>
public class EmployeeLifecycleManager : DomainService
{
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<LeaveApplication, Guid> _leaveRepository;
    private readonly IRepository<PayrollEntryLine, Guid> _payrollLineRepository;
    private readonly IRepository<Attendance, Guid> _attendanceRepository;

    public EmployeeLifecycleManager(
        IRepository<Employee, Guid> employeeRepository,
        IRepository<LeaveApplication, Guid> leaveRepository,
        IRepository<PayrollEntryLine, Guid> payrollLineRepository,
        IRepository<Attendance, Guid> attendanceRepository)
    {
        _employeeRepository = employeeRepository;
        _leaveRepository = leaveRepository;
        _payrollLineRepository = payrollLineRepository;
        _attendanceRepository = attendanceRepository;
    }

    /// <summary>
    /// Changes the status of an employee.
    /// Example: Active -> Left (requires DateOfResignation)
    /// </summary>
    public async Task ChangeStatusAsync(Employee employee, EmploymentStatus newStatus, DateTime? dateOfResignation = null)
    {
        if (employee.Status == newStatus)
        {
            return;
        }

        if (newStatus == EmploymentStatus.Resigned)
        {
            if (!dateOfResignation.HasValue)
            {
                throw new BusinessException("MyERP:HR:001", "Date of Resignation is required when marking employee as Resigned.");
            }
            employee.DateOfResignation = dateOfResignation.Value;
        }

        // Per ERPNext employee.validate_status(): cannot relieve (Resigned/Terminated) an employee
        // while other active employees still report to them — reassign subordinates first.
        if (newStatus == EmploymentStatus.Resigned || newStatus == EmploymentStatus.Terminated)
        {
            var hasActiveSubordinates = await _employeeRepository.AnyAsync(e =>
                e.ReportsToEmployeeId == employee.Id &&
                e.Status != EmploymentStatus.Resigned &&
                e.Status != EmploymentStatus.Terminated);

            if (hasActiveSubordinates)
            {
                throw new BusinessException("MyERP:HR:005",
                    "Cannot relieve this employee — one or more active employees still report to them. Reassign their reports first.");
            }
        }

        employee.Status = newStatus;
        await _employeeRepository.UpdateAsync(employee);
    }

    /// <summary>
    /// Validates if an employee can be deleted.
    /// An employee cannot be deleted if they have linked transactions such as Leaves, Salary slips, or Attendance.
    /// </summary>
    public async Task CheckDeletionRulesAsync(Guid employeeId)
    {
        var hasLeaves = await _leaveRepository.AnyAsync(l => l.EmployeeId == employeeId);
        if (hasLeaves)
        {
            throw new BusinessException("MyERP:HR:002", "Cannot delete Employee because there are linked Leave Applications.");
        }

        var hasPayroll = await _payrollLineRepository.AnyAsync(p => p.EmployeeId == employeeId);
        if (hasPayroll)
        {
            throw new BusinessException("MyERP:HR:003", "Cannot delete Employee because there are linked Payroll entries.");
        }

        var hasAttendance = await _attendanceRepository.AnyAsync(a => a.EmployeeId == employeeId);
        if (hasAttendance)
        {
            throw new BusinessException("MyERP:HR:004", "Cannot delete Employee because there are linked Attendance records.");
        }
    }

    /// <summary>
    /// Resolves the active Holiday List for an employee as on a given date.
    /// Per ERPNext setup/doctype/employee/employee.py::get_holiday_list_for_employee (commits e17d3aac93 & 44d935b8f2):
    /// 1. Invokes optional holidayListOverride (passing employeeId, raiseException, asOnDate) if provided.
    /// 2. Checks active ShiftAssignment covering asOnDate whose ShiftType specifies a HolidayListId.
    /// 3. Falls back to the company's HolidayList for asOnDate.Year (preferring IsDefault), or company default HolidayList.
    /// </summary>
    public async Task<HolidayList?> GetHolidayListForEmployeeAsync(
        Guid employeeId,
        bool raiseException = true,
        DateTime? asOn = null,
        Func<Guid, bool, DateTime, Task<Guid?>>? holidayListOverride = null)
    {
        var targetDate = (asOn ?? DateTime.UtcNow.Date).Date;
        var holidayListRepo = LazyServiceProvider?.LazyGetService<IRepository<HolidayList, Guid>>();

        if (holidayListOverride != null)
        {
            var overrideListId = await holidayListOverride(employeeId, raiseException, targetDate);
            if (overrideListId.HasValue && holidayListRepo != null)
            {
                var hlQuery = await holidayListRepo.WithDetailsAsync(h => h.Holidays);
                return hlQuery.FirstOrDefault(h => h.Id == overrideListId.Value);
            }
            return null;
        }

        if (holidayListRepo == null)
            return null;

        var employee = await _employeeRepository.FindAsync(employeeId);
        if (employee == null)
        {
            if (raiseException)
            {
                throw new BusinessException(MyERPDomainErrorCodes.EntityNotFound)
                    .WithData("entityType", "Employee")
                    .WithData("id", employeeId);
            }
            return null;
        }

        var holidayListsQuery = await holidayListRepo.WithDetailsAsync(h => h.Holidays);

        // Check ShiftAssignment active on targetDate -> ShiftType.HolidayListId
        var shiftAssignmentRepo = LazyServiceProvider?.LazyGetService<IRepository<ShiftAssignment, Guid>>();
        var shiftTypeRepo = LazyServiceProvider?.LazyGetService<IRepository<ShiftType, Guid>>();
        if (shiftAssignmentRepo != null && shiftTypeRepo != null)
        {
            var saQuery = await shiftAssignmentRepo.GetQueryableAsync();
            var activeShift = saQuery
                .Where(sa => sa.EmployeeId == employeeId
                          && sa.Status == ShiftAssignmentStatus.Active
                          && sa.StartDate <= targetDate
                          && (!sa.EndDate.HasValue || sa.EndDate.Value >= targetDate))
                .OrderByDescending(sa => sa.StartDate)
                .FirstOrDefault();

            if (activeShift != null)
            {
                var shiftType = await shiftTypeRepo.FindAsync(activeShift.ShiftTypeId);
                if (shiftType?.HolidayListId != null)
                {
                    var shiftHolidayList = holidayListsQuery.FirstOrDefault(h => h.Id == shiftType.HolidayListId.Value);
                    if (shiftHolidayList != null)
                        return shiftHolidayList;
                }
            }
        }

        // Fall back to Company HolidayList matching targetDate.Year (preferring IsDefault), then any company default
        var companyLists = holidayListsQuery
            .Where(h => h.CompanyId == employee.CompanyId)
            .ToList();

        var resolved = companyLists
            .Where(h => h.Year == targetDate.Year)
            .OrderByDescending(h => h.IsDefault)
            .FirstOrDefault()
            ?? companyLists.FirstOrDefault(h => h.IsDefault);

        if (resolved == null && raiseException)
        {
            throw new BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", $"Please set a default Holiday List for Employee {employee.EmployeeId} or Company {employee.CompanyId}.");
        }

        return resolved;
    }

    /// <summary>
    /// Checks whether a given date (defaults to today) is a holiday for the specified employee.
    /// Per ERPNext commits e17d3aac93 & 44d935b8f2: resolves `date = today()` BEFORE calling
    /// GetHolidayListForEmployeeAsync(..., asOn: date) so the date is passed to the holiday list lookup.
    /// </summary>
    public async Task<bool> IsHolidayAsync(
        Guid employeeId,
        DateTime? date = null,
        bool raiseException = true,
        bool onlyNonWeekly = false,
        Func<Guid, bool, DateTime, Task<Guid?>>? holidayListOverride = null)
    {
        var checkDate = (date ?? DateTime.UtcNow.Date).Date;
        var holidayList = await GetHolidayListForEmployeeAsync(
            employeeId,
            raiseException,
            asOn: checkDate,
            holidayListOverride: holidayListOverride);

        if (holidayList == null)
            return false;

        return holidayList.Holidays.Any(h =>
            h.HolidayDate.Date == checkDate
            && (!onlyNonWeekly || !h.IsWeeklyOff));
    }
}
