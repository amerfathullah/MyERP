using System;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that processes deferred revenue recognition for a company.
/// Generates Journal Entries for service periods that have elapsed.
/// Per ERPNext: runs monthly to recognize revenue for completed service periods.
/// 
/// Logic:
/// 1. Find posted Sales Invoices with EnableDeferredRevenue items
/// 2. For each item with service period elapsed (ServiceStartDate to ServiceEndDate)
/// 3. Generate monthly JE: DR Deferred Revenue → CR Revenue
/// 4. Final period absorbs rounding difference (total - already_booked)
/// </summary>
public class DeferredRevenueJob : AsyncBackgroundJob<DeferredRevenueJobArgs>, ITransientDependency
{
    private readonly DeferredAccountingService _deferredService;
    private readonly ILogger<DeferredRevenueJob> _logger;

    public DeferredRevenueJob(
        DeferredAccountingService deferredService,
        ILogger<DeferredRevenueJob> logger)
    {
        _deferredService = deferredService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DeferredRevenueJobArgs args)
    {
        _logger.LogInformation("DeferredRevenueJob: Processing deferred revenue for company {CompanyId}, as of {AsOfDate}",
            args.CompanyId, args.AsOfDate.ToString("yyyy-MM-dd"));

        try
        {
            var count = await _deferredService.ProcessDeferredRevenueAsync(
                args.CompanyId, args.AsOfDate, args.TenantId);

            _logger.LogInformation(
                "DeferredRevenueJob: Created {Count} recognition JEs for company {CompanyId}",
                count, args.CompanyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeferredRevenueJob: Error processing company {CompanyId}", args.CompanyId);
            throw; // ABP will handle retry
        }
    }
}

public class DeferredRevenueJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;
}
