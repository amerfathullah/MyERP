using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyERP.Support.Entities;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Support.BackgroundJobs;

/// <summary>
/// Auto-closes replied support tickets that have been inactive for N days.
/// ERPNext equivalent: support/doctype/issue/issue.py auto_close_tickets (daily scheduler).
/// </summary>
public class IssueAutoCloseJob : AsyncBackgroundJob<IssueAutoCloseJobArgs>, ITransientDependency
{
    private readonly IRepository<Issue, Guid> _repository;
    private readonly ILogger<IssueAutoCloseJob> _logger;

    public IssueAutoCloseJob(IRepository<Issue, Guid> repository, ILogger<IssueAutoCloseJob> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public override async Task ExecuteAsync(IssueAutoCloseJobArgs args)
    {
        var asOfDate = args.AsOfDate ?? DateTime.UtcNow.Date;
        var inactiveDays = args.InactiveDays > 0 ? args.InactiveDays : 7;
        var cutoffDate = asOfDate.AddDays(-inactiveDays);

        var query = await _repository.GetQueryableAsync();
        var staleIssues = query
            .Where(i => i.CompanyId == args.CompanyId &&
                        i.Status == IssueStatus.Replied &&
                        (i.LastModificationTime ?? i.CreationTime) <= cutoffDate)
            .ToList();

        var closedCount = 0;
        foreach (var issue in staleIssues)
        {
            issue.Resolve("Auto-closed due to inactivity");
            await _repository.UpdateAsync(issue);
            closedCount++;
        }

        _logger.LogInformation("IssueAutoCloseJob auto-closed {Count} inactive replied issues for company {CompanyId} as of {Date}",
            closedCount, args.CompanyId, asOfDate);
    }
}

public class IssueAutoCloseJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public int InactiveDays { get; set; } = 7;
}
