using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Core.Entities;
using MyERP.Sales.DomainServices;
using MyERP.Sales.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that processes subscription billing for a company.
/// Generates invoices for active subscriptions whose current period has ended.
/// Equivalent to ERPNext's process_all_subscriptions scheduled task.
///
/// Per ERPNext subscription rules:
/// - Checks late-fire cap (won't generate if &gt; 1 billing cycle past period end)
/// - Generates Sales Invoice with proper trial period discount (100%)
/// - Advances billing period after invoice creation
/// - Auto-cancels subscriptions past their end date
/// - Per-subscription error isolation (one failure doesn't block others)
/// </summary>
public class SubscriptionBillingJob : AsyncBackgroundJob<SubscriptionBillingJobArgs>, ITransientDependency
{
    private readonly IRepository<Subscription, Guid> _subscriptionRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly SubscriptionBillingEngine _billingEngine;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<SubscriptionBillingJob> _logger;

    public SubscriptionBillingJob(
        IRepository<Subscription, Guid> subscriptionRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        SubscriptionBillingEngine billingEngine,
        IGuidGenerator guidGenerator,
        ILogger<SubscriptionBillingJob> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _billingEngine = billingEngine;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SubscriptionBillingJobArgs args)
    {
        _logger.LogInformation("Starting subscription billing for company {CompanyId}", args.CompanyId);

        var subs = await _subscriptionRepository.GetListAsync(
            s => s.CompanyId == args.CompanyId
              && s.Status == SubscriptionStatus.Active);

        var invoicesGenerated = 0;
        var cancelled = 0;
        var errors = 0;

        foreach (var sub in subs)
        {
            try
            {
                // Auto-cancel past end date
                if (sub.EndDate.HasValue && args.AsOfDate > sub.EndDate.Value)
                {
                    sub.Cancel();
                    await _subscriptionRepository.UpdateAsync(sub);
                    cancelled++;
                    continue;
                }

                // Catch-up billing: generate ALL missed invoices, not just one
                // Per DO-NOT: "Implement subscription without catch-up invoice generation for past periods"
                var missedPeriods = _billingEngine.GetMissedPeriodsCount(sub, args.AsOfDate);
                for (int period = 0; period < missedPeriods; period++)
                {
                    // Check if invoice is due for the current period
                    if (!_billingEngine.IsInvoiceDue(sub, args.AsOfDate))
                        break;

                    // Check late-fire cap (skip if too far past period end)
                    if (!_billingEngine.IsWithinLateFireCap(sub, args.AsOfDate))
                    {
                        _logger.LogWarning("Subscription {SubId} past late-fire cap, skipping", sub.Id);
                        break;
                    }

                    // Build invoice items (handles trial period 100% discount)
                    var items = _billingEngine.BuildInvoiceItems(sub, args.AsOfDate);
                    if (!items.Any()) break;

                    // Create Sales Invoice
                    var invoiceRef = _billingEngine.GenerateInvoiceReference(sub);
                    var invoice = new SalesInvoice(
                        _guidGenerator.Create(), sub.CompanyId, sub.PartyId, invoiceRef,
                        sub.CurrentInvoiceStart ?? args.AsOfDate, args.TenantId);
                    invoice.Notes = $"Subscription {sub.SubscriptionNumber} — " +
                        $"{sub.CurrentInvoiceStart:dd/MM/yyyy} to {sub.CurrentInvoiceEnd:dd/MM/yyyy}";

                    foreach (var item in items)
                        invoice.AddItem(item.ItemId, item.ItemName ?? "Subscription Item",
                            item.Qty, item.Rate, 0m);

                    await _salesInvoiceRepository.InsertAsync(invoice);
                    invoicesGenerated++;

                    // Advance period and check completion
                    if (_billingEngine.AdvancePeriodAndCheckCompletion(sub))
                    {
                        cancelled++;
                        break; // Subscription completed, stop generating
                    }
                }

                await _subscriptionRepository.UpdateAsync(sub);
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error processing subscription {SubId}", sub.Id);
            }
        }

        _logger.LogInformation(
            "Subscription billing complete for company {CompanyId}: {Invoices} invoices, {Cancelled} cancelled, {Errors} errors",
            args.CompanyId, invoicesGenerated, cancelled, errors);
    }
}

public class SubscriptionBillingJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
}
