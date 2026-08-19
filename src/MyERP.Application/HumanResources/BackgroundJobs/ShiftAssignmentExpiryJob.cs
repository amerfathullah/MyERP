using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.HumanResources.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.HumanResources.BackgroundJobs;

/// <summary>
/// Background job that updates status on shift assignments past their EndDate to Inactive.
/// Per ERPNext: shift_assignment.update_shift_assignment_status (daily scheduler).
/// </summary>
public class ShiftAssignmentExpiryJob : AsyncBackgroundJob<ShiftAssignmentExpiryJobArgs>, ITransientDependency
{
    private readonly IRepository<ShiftAssignment, Guid> _shiftAssignmentRepository;
    private readonly ILogger<ShiftAssignmentExpiryJob> _logger;

    public ShiftAssignmentExpiryJob(
        IRepository<ShiftAssignment, Guid> shiftAssignmentRepository,
        ILogger<ShiftAssignmentExpiryJob> logger)
    {
        _shiftAssignmentRepository = shiftAssignmentRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(ShiftAssignmentExpiryJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("ShiftAssignmentExpiryJob: Checking expired shift assignments for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _shiftAssignmentRepository.GetQueryableAsync();
        var expiredAssignments = query
            .Where(s => s.CompanyId == args.CompanyId &&
                        s.Status == ShiftAssignmentStatus.Active &&
                        s.EndDate.HasValue &&
                        s.EndDate.Value.Date < asOfDate.Date)
            .ToList();

        var updatedCount = 0;
        foreach (var assignment in expiredAssignments)
        {
            assignment.Status = ShiftAssignmentStatus.Inactive;
            await _shiftAssignmentRepository.UpdateAsync(assignment);
            updatedCount++;
        }

        _logger.LogInformation("ShiftAssignmentExpiryJob: Deactivated {Count} expired shift assignments for company {CompanyId}",
            updatedCount, args.CompanyId);
    }
}

public class ShiftAssignmentExpiryJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
