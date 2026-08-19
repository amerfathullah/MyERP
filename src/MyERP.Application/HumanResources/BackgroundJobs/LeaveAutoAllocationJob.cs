using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.HumanResources.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.HumanResources.BackgroundJobs;

/// <summary>
/// Background job that automatically creates leave allocations for active employees for the current year.
/// Per ERPNext: leave_control_panel.allocate_leave (annual/monthly scheduler).
/// Carries forward unused balances for carry-forward enabled leave types up to MaxCarryForwardDays.
/// </summary>
public class LeaveAutoAllocationJob : AsyncBackgroundJob<LeaveAutoAllocationJobArgs>, ITransientDependency
{
    private readonly IRepository<Employee, Guid> _employeeRepository;
    private readonly IRepository<LeaveType, Guid> _leaveTypeRepository;
    private readonly IRepository<LeaveAllocation, Guid> _allocationRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<LeaveAutoAllocationJob> _logger;

    public LeaveAutoAllocationJob(
        IRepository<Employee, Guid> employeeRepository,
        IRepository<LeaveType, Guid> leaveTypeRepository,
        IRepository<LeaveAllocation, Guid> allocationRepository,
        IGuidGenerator guidGenerator,
        ILogger<LeaveAutoAllocationJob> logger)
    {
        _employeeRepository = employeeRepository;
        _leaveTypeRepository = leaveTypeRepository;
        _allocationRepository = allocationRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(LeaveAutoAllocationJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var startOfYear = new DateTime(asOfDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfYear = new DateTime(asOfDate.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        _logger.LogInformation("LeaveAutoAllocationJob: Allocating leaves for company {CompanyId} for year {Year}",
            args.CompanyId, asOfDate.Year);

        var empQuery = await _employeeRepository.GetQueryableAsync();
        var activeEmployees = empQuery
            .Where(e => e.CompanyId == args.CompanyId && e.Status == EmploymentStatus.Active)
            .ToList();

        if (!activeEmployees.Any())
            return;

        var ltQuery = await _leaveTypeRepository.GetQueryableAsync();
        var activeLeaveTypes = ltQuery
            .Where(lt => lt.IsActive && lt.MaxDaysAllowed > 0)
            .ToList();

        var allocQuery = await _allocationRepository.GetQueryableAsync();
        var currentAllocations = allocQuery
            .Where(a => a.CompanyId == args.CompanyId && a.FromDate >= startOfYear && a.ToDate <= endOfYear)
            .ToList();

        var allocatedCount = 0;
        foreach (var emp in activeEmployees)
        {
            foreach (var leaveType in activeLeaveTypes)
            {
                var existing = currentAllocations.FirstOrDefault(a => a.EmployeeId == emp.Id && a.LeaveTypeId == leaveType.Id);
                if (existing != null)
                    continue;

                decimal carryForward = 0m;
                DateTime? carryForwardExpiry = null;

                if (leaveType.AllowCarryForward)
                {
                    var prevYearStart = startOfYear.AddYears(-1);
                    var prevYearEnd = endOfYear.AddYears(-1);

                    var prevAlloc = allocQuery.FirstOrDefault(a =>
                        a.EmployeeId == emp.Id &&
                        a.LeaveTypeId == leaveType.Id &&
                        a.FromDate >= prevYearStart &&
                        a.ToDate <= prevYearEnd);

                    if (prevAlloc != null && prevAlloc.Balance > 0)
                    {
                        carryForward = Math.Min(prevAlloc.Balance, leaveType.MaxCarryForwardDays);
                        if (leaveType.CarryForwardExpiryMonths > 0)
                        {
                            carryForwardExpiry = startOfYear.AddMonths(leaveType.CarryForwardExpiryMonths);
                        }
                    }
                }

                var allocation = new LeaveAllocation(
                    _guidGenerator.Create(),
                    args.CompanyId,
                    emp.Id,
                    leaveType.Id,
                    startOfYear,
                    endOfYear,
                    leaveType.MaxDaysAllowed,
                    args.TenantId)
                {
                    CarryForwardDays = carryForward,
                    CarryForwardExpiryDate = carryForwardExpiry,
                };

                await _allocationRepository.InsertAsync(allocation);
                allocatedCount++;
            }
        }

        _logger.LogInformation("LeaveAutoAllocationJob: Created {Count} leave allocations for company {CompanyId}",
            allocatedCount, args.CompanyId);
    }
}

public class LeaveAutoAllocationJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
