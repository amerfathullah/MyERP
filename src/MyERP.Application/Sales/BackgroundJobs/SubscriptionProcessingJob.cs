using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Core.DomainServices;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that processes recurring subscriptions and advances invoice billing cycles.
/// Per ERPNext: subscription.process (daily scheduler).
/// </summary>
public class SubscriptionProcessingJob : AsyncBackgroundJob<SubscriptionProcessingJobArgs>, ITransientDependency
{
    private readonly IRepository<Subscription, Guid> _subscriptionRepository;
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IDocumentNumberGenerator _numberGenerator;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<SubscriptionProcessingJob> _logger;

    public SubscriptionProcessingJob(
        IRepository<Subscription, Guid> subscriptionRepository,
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IDocumentNumberGenerator numberGenerator,
        IGuidGenerator guidGenerator,
        ILogger<SubscriptionProcessingJob> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _invoiceRepository = invoiceRepository;
        _numberGenerator = numberGenerator;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(SubscriptionProcessingJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("SubscriptionProcessingJob: Processing active subscriptions for company {CompanyId} as of {Date}",
            args.CompanyId, asOfDate);

        var query = await _subscriptionRepository.GetQueryableAsync();
        var subscriptions = query
            .Where(s => s.CompanyId == args.CompanyId && s.Status == SubscriptionStatus.Active)
            .ToList();

        if (!subscriptions.Any())
            return;

        var processedCount = 0;
        foreach (var sub in subscriptions)
        {
            // 1. Check end date
            if (sub.EndDate.HasValue && asOfDate > sub.EndDate.Value.Date)
            {
                // Completed
                sub.AdvancePeriod();
                await _subscriptionRepository.UpdateAsync(sub);
                continue;
            }

            // 2. Check if new billing period is due
            var isDue = sub.CurrentInvoiceEnd == null || sub.CurrentInvoiceEnd.Value.Date <= asOfDate;
            if (!isDue)
                continue;

            sub.AdvancePeriod();

            // Create recurring Sales Invoice
            if (sub.PartyType == "Customer" && sub.Plans.Any())
            {
                try
                {
                    var invoiceNumber = await _numberGenerator.GenerateAsync("SalesInvoice", args.CompanyId);
                    var dueDate = asOfDate.AddDays(sub.DaysUntilDue > 0 ? sub.DaysUntilDue : 30);
                    var invoice = new SalesInvoice(
                        _guidGenerator.Create(),
                        args.CompanyId,
                        sub.PartyId,
                        invoiceNumber,
                        asOfDate,
                        args.TenantId)
                    {
                        DueDate = dueDate,
                        CostCenterId = sub.CostCenterId,
                        Notes = $"Recurring subscription invoice for {sub.SubscriptionNumber ?? sub.Id.ToString()} ({sub.CurrentInvoiceStart:yyyy-MM-dd} to {sub.CurrentInvoiceEnd:yyyy-MM-dd})"
                    };

                    foreach (var plan in sub.Plans)
                    {
                        invoice.AddItem(
                            plan.ItemId,
                            plan.ItemName ?? "Subscription Item",
                            plan.Qty,
                            plan.Rate,
                            0m);
                    }

                    await _invoiceRepository.InsertAsync(invoice);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SubscriptionProcessingJob: Failed to create recurring invoice for subscription {SubscriptionId}", sub.Id);
                }
            }

            await _subscriptionRepository.UpdateAsync(sub);
        }

        _logger.LogInformation("SubscriptionProcessingJob: Generated {Count} subscription invoices for company {CompanyId}",
            processedCount, args.CompanyId);
    }
}

public class SubscriptionProcessingJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
}
