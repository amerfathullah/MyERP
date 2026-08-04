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
}
