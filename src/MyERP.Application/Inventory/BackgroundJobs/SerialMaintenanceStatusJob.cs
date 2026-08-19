using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Inventory.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that updates Serial No maintenance/warranty/AMC status.
/// Per ERPNext: serial_no.update_maintenance_status (daily scheduler).
/// </summary>
public class SerialMaintenanceStatusJob : AsyncBackgroundJob<SerialMaintenanceStatusJobArgs>, ITransientDependency
{
    private readonly IRepository<SerialNo, Guid> _repository;
    private readonly ILogger<SerialMaintenanceStatusJob> _logger;

    public SerialMaintenanceStatusJob(
        IRepository<SerialNo, Guid> repository,
        ILogger<SerialMaintenanceStatusJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SerialMaintenanceStatusJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("SerialMaintenanceStatusJob: Updating serial maintenance status for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _repository.GetQueryableAsync();
        var serials = query
            .Where(s => s.CompanyId == args.CompanyId &&
                        (s.WarrantyExpiryDate.HasValue || s.AmcExpiryDate.HasValue))
            .ToList();

        var updatedCount = 0;
        foreach (var serial in serials)
        {
            var prevStatus = serial.MaintenanceStatus;
            serial.UpdateMaintenanceStatus(asOfDate);
            if (serial.MaintenanceStatus != prevStatus)
            {
                await _repository.UpdateAsync(serial);
                updatedCount++;
            }
        }

        _logger.LogInformation("SerialMaintenanceStatusJob: Updated {Count} of {Total} serial numbers for company {CompanyId}",
            updatedCount, serials.Count, args.CompanyId);
    }
}

public class SerialMaintenanceStatusJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
