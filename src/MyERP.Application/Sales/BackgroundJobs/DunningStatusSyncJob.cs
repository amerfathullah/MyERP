using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Core;
using MyERP.Sales.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Sales.BackgroundJobs;

/// <summary>
/// Background job that syncs payment status on submitted Dunning notices.
/// Automatically resolves Dunning documents when all underlying invoices have been paid.
/// Per ERPNext: dunning.update_dunning_status (daily scheduler).
/// </summary>
public class DunningStatusSyncJob : AsyncBackgroundJob<DunningStatusSyncJobArgs>, ITransientDependency
{
    private readonly IRepository<Dunning, Guid> _dunningRepository;
    private readonly IRepository<SalesInvoice, Guid> _salesInvoiceRepository;
    private readonly ILogger<DunningStatusSyncJob> _logger;

    public DunningStatusSyncJob(
        IRepository<Dunning, Guid> dunningRepository,
        IRepository<SalesInvoice, Guid> salesInvoiceRepository,
        ILogger<DunningStatusSyncJob> logger)
    {
        _dunningRepository = dunningRepository;
        _salesInvoiceRepository = salesInvoiceRepository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DunningStatusSyncJobArgs args)
    {
        _logger.LogInformation("DunningStatusSyncJob: Checking submitted dunning notices for company {CompanyId}",
            args.CompanyId);

        var query = await _dunningRepository.GetQueryableAsync();
        var submittedDunnings = query
            .Where(d => d.CompanyId == args.CompanyId && d.Status == DocumentStatus.Submitted)
            .ToList();

        if (!submittedDunnings.Any())
            return;

        var invoiceIds = submittedDunnings
            .SelectMany(d => d.OverduePayments)
            .Select(p => p.SalesInvoiceId)
            .Distinct()
            .ToList();

        var invQuery = await _salesInvoiceRepository.GetQueryableAsync();
        var invoices = invQuery
            .Where(i => invoiceIds.Contains(i.Id))
            .ToList();

        var resolvedCount = 0;
        foreach (var dunning in submittedDunnings)
        {
            var allPaid = true;
            foreach (var payment in dunning.OverduePayments)
            {
                var inv = invoices.FirstOrDefault(i => i.Id == payment.SalesInvoiceId);
                if (inv != null && inv.OutstandingAmount > 0)
                {
                    allPaid = false;
                    break;
                }
            }

            if (allPaid)
            {
                dunning.Resolve();
                await _dunningRepository.UpdateAsync(dunning);
                resolvedCount++;
            }
        }

        _logger.LogInformation("DunningStatusSyncJob: Resolved {Count} of {Total} dunning notices for company {CompanyId}",
            resolvedCount, submittedDunnings.Count, args.CompanyId);
    }
}

public class DunningStatusSyncJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
