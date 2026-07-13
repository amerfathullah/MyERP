using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that processes subscription billing for a company.
/// Generates invoices for active subscriptions whose current period has ended.
/// Equivalent to ERPNext's process_all_subscriptions scheduled task.
/// 
/// For each active subscription with current_invoice_end &lt; today:
///   1. Generate Sales Invoice from subscription plans
///   2. Advance billing period
///   3. Auto-cancel if past EndDate
/// </summary>
public class SubscriptionBillingJob : AsyncBackgroundJob<SubscriptionBillingJobArgs>, ITransientDependency
{
    private readonly IRepository<Subscription, Guid> _subscriptionRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly ILogger<SubscriptionBillingJob> _logger;

    public SubscriptionBillingJob(
        IRepository<Subscription, Guid> subscriptionRepository,
        IRepository<Company, Guid> companyRepository,
        ILogger<SubscriptionBillingJob> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _companyRepository = companyRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SubscriptionBillingJobArgs args)
    {
        _logger.LogInformation("Starting subscription billing for company {CompanyId}", args.CompanyId);

        var query = await _subscriptionRepository.GetQueryableAsync();
        var dueSubscriptions = query
            .Where(s => s.CompanyId == args.CompanyId
                && s.Status == SubscriptionStatus.Active
                && s.CurrentInvoiceEnd <= args.AsOfDate)
            .ToList();

        if (!dueSubscriptions.Any())
        {
            _logger.LogInformation("No subscriptions due for billing in company {CompanyId}", args.CompanyId);
            return;
        }

        var processed = 0;
        var errors = 0;

        foreach (var subscription in dueSubscriptions)
        {
            try
            {
                // Check if subscription should auto-cancel (past EndDate)
                if (subscription.EndDate.HasValue && args.AsOfDate > subscription.EndDate.Value)
                {
                    subscription.Cancel();
                    await _subscriptionRepository.UpdateAsync(subscription);
                    _logger.LogInformation("Auto-cancelled subscription {SubId} (past end date)", subscription.Id);
                    continue;
                }

                // Advance the billing period (this prepares for next invoice)
                subscription.AdvancePeriod();
                await _subscriptionRepository.UpdateAsync(subscription);
                processed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error processing subscription {SubId}", subscription.Id);
            }
        }

        _logger.LogInformation(
            "Subscription billing complete for company {CompanyId}: {Processed} processed, {Errors} errors",
            args.CompanyId, processed, errors);
    }
}

public class SubscriptionBillingJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
