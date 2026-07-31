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

                // Enqueue recurring journal entry generation (monthly accruals: rent, insurance, etc.)
                await jobManager.EnqueueAsync(new Core.BackgroundJobs.RecurringJournalEntryJobArgs
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

                // Enqueue exchange rate auto-fetch (refreshes stale currency pairs from external API)
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.ExchangeRateAutoFetchJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    MaxStaleDays = 1,
                });

                // Enqueue invoice status safety-net update (catches missed payment status updates)
                // Per DO-NOT: "Skip daily invoice status recalculation"
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.InvoiceStatusUpdateJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue payment reminders for overdue invoices
                // Per ERPNext: send_payment_reminders daily scheduler event
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.PaymentReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    ReminderCooldownDays = 7,
                });

                // Enqueue BOM cost auto-update (when raw material prices change)
                // Per ERPNext: Manufacturing Settings.update_bom_costs_automatically
                await jobManager.EnqueueAsync(new Manufacturing.BackgroundJobs.BomCostAutoUpdateJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue Work Order overdue notification
                // Per ERPNext: send_notification_for_overdue_work_orders daily scheduler
                await jobManager.EnqueueAsync(new Manufacturing.BackgroundJobs.WorkOrderOverdueNotificationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    UserId = Guid.Empty, // Resolved at job execution time
                });

                // Enqueue Purchase Order overdue delivery alert
                // Per ERPNext: procurement managers need daily alerts on late supplier deliveries
                await jobManager.EnqueueAsync(new Purchasing.BackgroundJobs.PurchaseOrderOverdueAlertJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    UserId = Guid.Empty,
                });

                // Enqueue upcoming payment due date alerts (proactive cash flow management)
                // Per ERPNext: daily reminder for invoices due in 3/7 days — separate from overdue reminders
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.UpcomingPaymentDueAlertJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    UserId = Guid.Empty,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NightlyProcessingWorker: Failed to enqueue jobs for company {CompanyId} ({CompanyName}). Continuing with next company.",
                    company.Id, company.Name);
            }
        }

        logger.LogInformation("NightlyProcessingWorker: Enqueued {Count} companies for nightly processing (16 jobs).", companies.Count);
    }
}
