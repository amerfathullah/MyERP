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

                // Enqueue batch expiry alerts (proactive notification for expiring stock)
                // Per DO-NOT: "Implement batch expiry without blocking expired batch consumption"
                // This is the proactive complement to the transactional blocking validation.
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.BatchExpiryAlertJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    AlertDaysAhead = 30,
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

                // Enqueue opportunity auto-close (inactivity cleanup)
                // Per ERPNext: crm/doctype/opportunity/opportunity.py auto_close_opportunity
                await jobManager.EnqueueAsync(new CRM.BackgroundJobs.OpportunityAutoCloseJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    InactiveDays = 30,
                });

                // Enqueue portal appointment expiry (unverified cleanup)
                // Per ERPNext PR #57270
                await jobManager.EnqueueAsync(new CRM.BackgroundJobs.AppointmentExpiryJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow,
                });

                // Enqueue contract status update (expired contracts to InactiveByExpiry)
                // Per ERPNext: crm/doctype/contract/contract.py update_status_for_contracts
                await jobManager.EnqueueAsync(new CRM.BackgroundJobs.ContractStatusUpdateJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue issue auto-close (inactivity cleanup for replied tickets)
                // Per ERPNext: support/doctype/issue/issue.py auto_close_tickets
                await jobManager.EnqueueAsync(new Support.BackgroundJobs.IssueAutoCloseJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    InactiveDays = 7,
                });

                // Enqueue supplier quotation auto-expiry
                // Per ERPNext: supplier_quotation.set_expired_status
                await jobManager.EnqueueAsync(new Purchasing.BackgroundJobs.SupplierQuotationExpiryJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue task overdue status update
                // Per ERPNext: task.set_tasks_as_overdue
                await jobManager.EnqueueAsync(new Projects.BackgroundJobs.TaskOverdueJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue fiscal year auto-creation
                // Per ERPNext: fiscal_year.auto_create_fiscal_year
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.FiscalYearAutoCreationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue serial maintenance status refresh
                // Per ERPNext: serial_no.update_maintenance_status
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.SerialMaintenanceStatusJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue quality review status evaluation
                // Per ERPNext: quality_review.review
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.QualityReviewJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue bank transaction auto-match rule evaluation
                // Per ERPNext: bank_transaction_rule.scheduler_run_rule_evaluation
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.BankTransactionAutoMatchJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue monthly sales history cache
                // Per ERPNext: company.cache_companies_monthly_sales_history
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.MonthlySalesCacheJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue project progress collection
                // Per ERPNext: project.collect_project_status
                await jobManager.EnqueueAsync(new Projects.BackgroundJobs.ProjectStatusCollectionJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue CRM event follow-up reopening
                // Per ERPNext: open_leads_opportunities_based_on_todays_event
                await jobManager.EnqueueAsync(new CRM.BackgroundJobs.CrmEventReopenJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue customer statements auto-email
                // Per ERPNext: process_statement_of_accounts.send_auto_email
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.StatementAutoEmailJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue project status digest email
                // Per ERPNext: project.send_project_status_email_to_users
                await jobManager.EnqueueAsync(new Projects.BackgroundJobs.ProjectStatusEmailJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue asset depreciation scheduler
                // Per ERPNext: assets/doctype/asset/depreciation.py
                await jobManager.EnqueueAsync(new Assets.BackgroundJobs.DepreciationSchedulerArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue maintenance schedule reminders
                // Per ERPNext: maintenance_schedule.generate_schedule
                await jobManager.EnqueueAsync(new Maintenance.BackgroundJobs.MaintenanceScheduleReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue supplier scorecard periodic evaluation
                // Per ERPNext: supplier_scorecard.generate_scorecards
                await jobManager.EnqueueAsync(new Purchasing.BackgroundJobs.SupplierScorecardEvaluationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue unbilled orders notification digest
                // Per ERPNext: unbilled_item_notifications
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.UnbilledOrdersNotificationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue annual leave auto-allocation
                // Per ERPNext: leave_control_panel.allocate_leave
                await jobManager.EnqueueAsync(new HumanResources.BackgroundJobs.LeaveAutoAllocationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue inactive lead follow-up notifications
                // Per ERPNext: crm.send_lead_followup_email
                await jobManager.EnqueueAsync(new CRM.BackgroundJobs.LeadFollowupJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                    InactivityThresholdDays = 7,
                });

                // Enqueue expense claim reimbursed amount syncing
                // Per ERPNext: expense_claim.update_status_for_expense_claim
                await jobManager.EnqueueAsync(new HumanResources.BackgroundJobs.ExpenseClaimPaymentStatusJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue shift assignment status expiry
                // Per ERPNext: shift_assignment.update_shift_assignment_status
                await jobManager.EnqueueAsync(new HumanResources.BackgroundJobs.ShiftAssignmentExpiryJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue LHDN monthly B2C consolidation reminder
                // Per Malaysia LHDN MyInvois compliance guidelines
                await jobManager.EnqueueAsync(new EInvoice.BackgroundJobs.LhdnConsolidationReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue LHDN pending e-invoice status synchronization
                // Per LHDN MyInvois polling guidelines
                await jobManager.EnqueueAsync(new EInvoice.BackgroundJobs.LhdnPendingStatusSyncJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue promotional coupons and schemes expiry
                // Per ERPNext: coupon_code.update_coupon_code_status
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.PromotionalExpiryJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue stock reservation auto-cancellation for completed/cancelled orders
                // Per ERPNext: stock_reservation_entry.auto_cancel_stock_reservation_entries
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.StockReservationAutoCancelJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue dunning status synchronization and resolution
                // Per ERPNext: dunning.update_dunning_status
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.DunningStatusSyncJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue queued stock valuation reposting
                // Per ERPNext: stock_reposting_settings.repost_incorrect_valuation_entries
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.StockValuationCorrectionJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue recurring subscription processing and billing
                // Per ERPNext: subscription.process
                await jobManager.EnqueueAsync(new Sales.BackgroundJobs.SubscriptionProcessingJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue monthly stock balance closing snapshots
                // Per ERPNext: stock_closing_entry.process
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.StockClosingJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue overdue quality inspection alerts
                // Per ERPNext: quality_inspection.notify_inspectors
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.QualityInspectionReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue delivery trip status advancement and driver dispatch alerts
                // Per ERPNext: delivery_trip.notify_customers
                await jobManager.EnqueueAsync(new Inventory.BackgroundJobs.DeliveryTripNotificationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue support ticket SLA variance and breach alerts
                // Per ERPNext: support.doctype.issue.issue.set_service_level_agreement_variance
                await jobManager.EnqueueAsync(new Support.BackgroundJobs.SupportSlaBreachJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue manufacturing work order operation sync and completion
                // Per ERPNext: work_order.update_operation_status
                await jobManager.EnqueueAsync(new Manufacturing.BackgroundJobs.WorkOrderOperationSyncJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                });

                // Enqueue upcoming maintenance schedule visit reminders
                // Per ERPNext: maintenance_schedule.send_maintenance_reminder
                await jobManager.EnqueueAsync(new Maintenance.BackgroundJobs.MaintenanceScheduleReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue submitted payment orders pending bank execution
                // Per ERPNext: payment_order.update_payment_order_status
                await jobManager.EnqueueAsync(new Accounting.BackgroundJobs.PaymentOrderNotificationJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });

                // Enqueue Malaysia monthly statutory remittance reminders (EPF/SOCSO/EIS/PCB by 15th)
                await jobManager.EnqueueAsync(new HumanResources.BackgroundJobs.PayrollStatutoryRemittanceReminderJobArgs
                {
                    CompanyId = company.Id,
                    TenantId = company.TenantId,
                    AsOfDate = DateTime.UtcNow.Date,
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NightlyProcessingWorker: Failed to enqueue jobs for company {CompanyId} ({CompanyName}). Continuing with next company.",
                    company.Id, company.Name);
            }
        }

        logger.LogInformation("NightlyProcessingWorker: Enqueued {Count} companies for nightly processing (54 jobs).", companies.Count);
    }
}
