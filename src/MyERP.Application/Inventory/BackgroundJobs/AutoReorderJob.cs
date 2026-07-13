using System;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Inventory.DomainServices;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Inventory.BackgroundJobs;

/// <summary>
/// Background job that scans all items for reorder needs.
/// Equivalent to ERPNext's daily reorder_item scheduled task.
/// Creates Material Requests for items below their configured reorder level.
/// 
/// Should be scheduled to run daily (or more frequently for critical items).
/// </summary>
public class AutoReorderJob : AsyncBackgroundJob<AutoReorderJobArgs>, ITransientDependency
{
    private readonly AutoReorderService _reorderService;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly ILogger<AutoReorderJob> _logger;

    public AutoReorderJob(
        AutoReorderService reorderService,
        IRepository<Company, Guid> companyRepository,
        ILogger<AutoReorderJob> logger)
    {
        _reorderService = reorderService;
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(AutoReorderJobArgs args)
    {
        _logger.LogInformation("Starting auto-reorder scan for company {CompanyId}", args.CompanyId);

        var createdMRs = await _reorderService.CheckAndReorderAsync(args.CompanyId, args.TenantId);

        if (createdMRs.Count > 0)
        {
            _logger.LogInformation(
                "Auto-reorder created {Count} Material Request(s) for company {CompanyId}",
                createdMRs.Count, args.CompanyId);
        }
        else
        {
            _logger.LogDebug("Auto-reorder scan complete. No items below reorder level for company {CompanyId}", args.CompanyId);
        }
    }
}

/// <summary>
/// Arguments for the auto-reorder background job.
/// </summary>
public class AutoReorderJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Scheduled worker that enqueues auto-reorder jobs for all active companies.
/// Can be registered as a recurring ABP background worker.
/// </summary>
public class AutoReorderWorker : ITransientDependency
{
    private readonly IBackgroundJobManager _jobManager;
    private readonly IRepository<Company, Guid> _companyRepository;

    public AutoReorderWorker(
        IBackgroundJobManager jobManager,
        IRepository<Company, Guid> companyRepository)
    {
        _jobManager = jobManager;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Enqueues a reorder check job for each active company.
    /// Called by the ABP periodic background worker timer (e.g., daily at 6 AM).
    /// </summary>
    public async Task EnqueueForAllCompaniesAsync()
    {
        var companies = await _companyRepository.GetListAsync(c => c.IsActive);

        foreach (var company in companies)
        {
            await _jobManager.EnqueueAsync(new AutoReorderJobArgs
            {
                CompanyId = company.Id,
                TenantId = company.TenantId,
            });
        }
    }
}
