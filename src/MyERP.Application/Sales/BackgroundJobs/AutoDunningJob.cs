using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Sales.Entities;
using MyERP.Core;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that auto-generates dunning notices for overdue Sales Invoices.
/// Per ERPNext: dunning is created when invoice is overdue beyond configured grace days.
/// 
/// Logic:
/// 1. Find all Posted SI with OutstandingAmount > 0 and DueDate < today
/// 2. Group by customer
/// 3. For each customer: determine dunning level (1 + count of existing submitted dunnings)
/// 4. Per DO-NOT: dunning level sequencing must be enforced (Level N before N+1)
/// 5. Create Dunning with overdue payment lines
/// 6. Skip invoices that already have an active (Draft/Submitted) dunning
/// </summary>
public class AutoDunningJob : AsyncBackgroundJob<AutoDunningJobArgs>, ITransientDependency
{
    private readonly IRepository<SalesInvoice, Guid> _invoiceRepository;
    private readonly IRepository<Dunning, Guid> _dunningRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<AutoDunningJob> _logger;

    public AutoDunningJob(
        IRepository<SalesInvoice, Guid> invoiceRepository,
        IRepository<Dunning, Guid> dunningRepository,
        IGuidGenerator guidGenerator,
        ILogger<AutoDunningJob> logger)
    {
        _invoiceRepository = invoiceRepository;
        _dunningRepository = dunningRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(AutoDunningJobArgs args)
    {
        _logger.LogInformation("AutoDunningJob: Starting for company {CompanyId}", args.CompanyId);

        var today = args.AsOfDate.Date;

        // Find overdue posted invoices with outstanding > 0
        var siQuery = await _invoiceRepository.GetQueryableAsync();
        var overdueInvoices = siQuery
            .Where(si => si.CompanyId == args.CompanyId
                && si.Status == Core.DocumentStatus.Posted
                && si.OutstandingAmount > 0
                && si.DueDate.HasValue
                && si.DueDate.Value < today)
            .ToList();

        if (!overdueInvoices.Any())
        {
            _logger.LogInformation("AutoDunningJob: No overdue invoices for company {CompanyId}", args.CompanyId);
            return;
        }

        // Get existing active dunnings to avoid duplicates
        var dunningQuery = await _dunningRepository.GetQueryableAsync();
        var activeDunningInvoiceIds = dunningQuery
            .Where(d => d.CompanyId == args.CompanyId
                && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.Submitted))
            .SelectMany(d => d.OverduePayments.Select(p => p.SalesInvoiceId))
            .Distinct()
            .ToList();

        // Filter out invoices already under active dunning
        var eligibleInvoices = overdueInvoices
            .Where(si => !activeDunningInvoiceIds.Contains(si.Id))
            .ToList();

        if (!eligibleInvoices.Any())
        {
            _logger.LogInformation("AutoDunningJob: All overdue invoices already have dunnings");
            return;
        }

        // Group by customer
        var byCustomer = eligibleInvoices.GroupBy(si => si.CustomerId);
        var created = 0;

        foreach (var group in byCustomer)
        {
            var customerId = group.Key;

            // Determine dunning level: count existing submitted dunnings for this customer + 1
            var existingLevel = dunningQuery
                .Where(d => d.CustomerId == customerId
                    && d.CompanyId == args.CompanyId
                    && d.Status == DocumentStatus.Submitted)
                .Select(d => d.DunningLevel)
                .OrderByDescending(l => l)
                .FirstOrDefault();

            var nextLevel = existingLevel + 1;

            // Create dunning
            var dunning = new Dunning(
                _guidGenerator.Create(),
                args.CompanyId,
                customerId,
                today,
                nextLevel,
                args.TenantId);

            foreach (var invoice in group)
            {
                var overdueDays = (int)(today - invoice.DueDate!.Value).TotalDays;
                dunning.AddOverduePayment(invoice.Id, invoice.OutstandingAmount, invoice.DueDate.Value, overdueDays);
            }

            // Calculate fee and interest based on level
            dunning.DunningFee = nextLevel * args.FeePerLevel;
            dunning.InterestAmount = group.Sum(i => i.OutstandingAmount) * args.InterestRatePerLevel * nextLevel / 100m;

            await _dunningRepository.InsertAsync(dunning);
            created++;
        }

        _logger.LogInformation(
            "AutoDunningJob: Created {Count} dunning notices for company {CompanyId}",
            created, args.CompanyId);
    }
}

public class AutoDunningJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime AsOfDate { get; set; } = DateTime.UtcNow;

    /// <summary>Fee per dunning level (e.g., RM 50 per level).</summary>
    public decimal FeePerLevel { get; set; } = 50m;

    /// <summary>Interest rate % per level (e.g., 1.5% per level applied to outstanding).</summary>
    public decimal InterestRatePerLevel { get; set; } = 1.5m;
}
