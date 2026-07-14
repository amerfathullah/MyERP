using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyERP.Core.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;

namespace MyERP.BackgroundWorkers;

/// <summary>
/// Periodic background worker that runs nightly tasks:
/// - Auto-reorder check (creates MRs for items below reorder level)
/// - Asset depreciation posting (creates JEs for due depreciation entries)
/// Both are enqueued as separate background jobs per company for parallel processing.
/// Runs daily at midnight (configurable via Timer.Period).
/// </summary>
public class NightlyProcessingWorker : AsyncPeriodicBackgroundWorkerBase
{
    public NightlyProcessingWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // Run every 24 hours (86,400,000 ms)
        Timer.Period = 24 * 60 * 60 * 1000;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<NightlyProcessingWorker>>();
        var jobManager = workerContext.ServiceProvider.GetRequiredService<IBackgroundJobManager>();
        var companyRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<Company, Guid>>();

        logger.LogInformation("NightlyProcessingWorker: Starting nightly batch processing...");

        var companies = await companyRepository.GetListAsync(c => c.IsActive);

        foreach (var company in companies)
        {
            try
            {
                // Enqueue auto-reorder check
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.AutoReorderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue depreciation posting
                await jobManager.EnqueueAsync(new Assets.BackgroundJobs.DepreciationSchedulerArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue subscription billing
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.SubscriptionBillingJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue auto-dunning (overdue invoice notices)
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.AutoDunningJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue deferred revenue recognition
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.DeferredRevenueJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue quotation auto-expiry
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.QuotationExpiryJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue recurring invoice generation
                await jobManager.EnqueueAsync(new Core.BackgroundJobs.RecurringInvoiceJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue ledger health check (per DO-NOT: must run daily to detect GL inconsistencies)
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.LedgerHealthCheckJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NightlyProcessingWorker: Failed to enqueue jobs for company {CompanyId} ({CompanyName}). Continuing with next company.",
                    company.Id, company.Name);
            }
        }

        logger.LogInformation("NightlyProcessingWorker: Enqueued {Count} companies for nightly processing (9 jobs).", companies.Count);
    }
}
