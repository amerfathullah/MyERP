using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Maintenance.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Maintenance.BackgroundJobs;

/// <summary>
/// Background job that auto-generates Asset Maintenance Logs for upcoming asset tasks and flags overdue logs.
/// Per ERPNext: asset_maintenance.update_maintenance_status (daily scheduler).
/// </summary>
public class AssetMaintenanceAutoLogJob : AsyncBackgroundJob<AssetMaintenanceAutoLogJobArgs>, ITransientDependency
{
    private readonly IRepository<AssetMaintenance, Guid> _maintenanceRepository;
    private readonly IRepository<AssetMaintenanceLog, Guid> _logRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<AssetMaintenanceAutoLogJob> _logger;

    public AssetMaintenanceAutoLogJob(
        IRepository<AssetMaintenance, Guid> maintenanceRepository,
        IRepository<AssetMaintenanceLog, Guid> logRepository,
        IGuidGenerator guidGenerator,
        ILogger<AssetMaintenanceAutoLogJob> logger)
    {
        _maintenanceRepository = maintenanceRepository;
        _logRepository = logRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(AssetMaintenanceAutoLogJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var upcomingWindow = asOfDate.AddDays(7);

        _logger.LogInformation("AssetMaintenanceAutoLogJob: Processing asset maintenance tasks for company {CompanyId} up to {Date}",
            args.CompanyId, upcomingWindow.ToString("yyyy-MM-dd"));

        var maintQuery = await _maintenanceRepository.WithDetailsAsync(m => m.Tasks);
        var maintenances = maintQuery
            .Where(m => m.CompanyId == args.CompanyId)
            .ToList();

        if (!maintenances.Any())
            return;

        var logQuery = await _logRepository.GetQueryableAsync();
        var existingLogs = logQuery
            .Where(l => l.CompanyId == args.CompanyId &&
                        (l.Status == AssetMaintenanceStatus.Planned || l.Status == AssetMaintenanceStatus.Overdue))
            .ToList();

        var generatedCount = 0;

        foreach (var maint in maintenances)
        {
            foreach (var task in maint.Tasks)
            {
                // Check if end date passed
                if (task.EndDate.HasValue && asOfDate > task.EndDate.Value)
                    continue;

                // Check if due within next 7 days
                if (task.NextDueDate <= upcomingWindow)
                {
                    // Check if log already generated
                    var logExists = existingLogs.Any(l => l.AssetMaintenanceTaskId == task.Id &&
                                                         l.DueDate.Date == task.NextDueDate.Date);
                    if (!logExists)
                    {
                        var log = new AssetMaintenanceLog(
                            _guidGenerator.Create(),
                            args.CompanyId,
                            maint.Id,
                            task.Id,
                            maint.AssetId,
                            task.MaintenanceTask,
                            task.NextDueDate,
                            task.Periodicity)
                        {
                            TenantId = args.TenantId,
                            AssetName = maint.AssetName,
                            ItemId = maint.ItemId,
                            ItemCode = maint.ItemCode,
                            ItemName = maint.ItemName,
                            MaintenanceType = task.MaintenanceType,
                            AssignToEmployeeId = task.AssignToEmployeeId,
                            AssignTo = task.AssignTo,
                            AssignToName = task.AssignToName,
                            Description = task.Description,
                            CertificateNo = task.CertificateNo,
                        };

                        await _logRepository.InsertAsync(log);
                        generatedCount++;

                        // Advance next due date
                        task.NextDueDate = AssetMaintenanceTask.CalculateNextDueDate(task.Periodicity, task.NextDueDate);
                    }
                }
            }

            await _maintenanceRepository.UpdateAsync(maint);
        }

        _logger.LogInformation("AssetMaintenanceAutoLogJob: Generated {Count} maintenance logs for company {CompanyId}",
            generatedCount, args.CompanyId);
    }
}

public class AssetMaintenanceAutoLogJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
