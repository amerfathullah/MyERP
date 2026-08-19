using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.CRM.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.CRM.BackgroundJobs;

/// <summary>
/// Closes stale opportunities inactive for N days.
/// ERPNext equivalent: crm/doctype/opportunity/opportunity.py auto_close_opportunity (daily scheduler).
/// </summary>
public class OpportunityAutoCloseJob : AsyncBackgroundJob<OpportunityAutoCloseJobArgs>, ITransientDependency
{
    private readonly IRepository<Opportunity, Guid> _repository;
    private readonly ILogger<OpportunityAutoCloseJob> _logger;

    public OpportunityAutoCloseJob(IRepository<Opportunity, Guid> repository, ILogger<OpportunityAutoCloseJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(OpportunityAutoCloseJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var inactiveDays = args.InactiveDays > 0 ? args.InactiveDays : 30;
        var cutoffDate = asOfDate.AddDays(-inactiveDays);

        var query = await _repository.GetQueryableAsync();
        var staleOpportunities = query
            .Where(o => o.CompanyId == args.CompanyId &&
                        (o.Status == OpportunityStatus.Open || o.Status == OpportunityStatus.Replied) &&
                        (o.LastModificationTime ?? o.CreationTime) <= cutoffDate)
            .ToList();

        var closedCount = 0;
        foreach (var opp in staleOpportunities)
        {
            opp.Close();
            await _repository.UpdateAsync(opp);
            closedCount++;
        }

        _logger.LogInformation("OpportunityAutoCloseJob closed {Count} stale opportunities for company {CompanyId} as of {Date}",
            closedCount, args.CompanyId, asOfDate);
    }
}

public class OpportunityAutoCloseJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int InactiveDays { get; set; } = 30;
}
