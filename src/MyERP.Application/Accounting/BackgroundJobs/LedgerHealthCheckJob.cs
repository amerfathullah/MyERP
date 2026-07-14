using System;
using System.Threading.Tasks;
using MyERP.Accounting.DomainServices;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;

namespace MyERP.Accounting.BackgroundJobs;

/// <summary>
/// Background job that runs daily GL consistency checks.
/// Per DO-NOT: "Skip ledger health checks — must run daily to detect GL inconsistencies"
/// 
/// Checks: unbalanced JEs (DR ≠ CR), PLE count anomalies, orphaned entries.
/// Results are logged. Critical issues should trigger admin notifications.
/// </summary>
public class LedgerHealthCheckJob : AsyncBackgroundJob<LedgerHealthCheckJobArgs>, ITransientDependency
{
    private readonly LedgerHealthCheckService _healthCheckService;
    private readonly ILogger<LedgerHealthCheckJob> _logger;

    public LedgerHealthCheckJob(
        LedgerHealthCheckService healthCheckService,
        ILogger<LedgerHealthCheckJob> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(LedgerHealthCheckJobArgs args)
    {
        _logger.LogInformation("Starting ledger health check for company {CompanyId}", args.CompanyId);

        try
        {
            var report = await _healthCheckService.RunHealthCheckAsync(args.CompanyId);

            if (report.IsHealthy)
            {
                _logger.LogInformation(
                    "Ledger health check PASSED for company {CompanyId}: {IssueCount} issues found (all informational)",
                    args.CompanyId, report.Issues.Count);
            }
            else
            {
                _logger.LogWarning(
                    "Ledger health check FAILED for company {CompanyId}: {IssueCount} critical issues found",
                    args.CompanyId, report.Issues.Count);

                foreach (var issue in report.Issues)
                {
                    _logger.LogWarning("  Ledger issue [{Severity}]: {Description}",
                        issue.Severity, issue.Description);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ledger health check failed for company {CompanyId}", args.CompanyId);
        }
    }
}

public class LedgerHealthCheckJobArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
}
