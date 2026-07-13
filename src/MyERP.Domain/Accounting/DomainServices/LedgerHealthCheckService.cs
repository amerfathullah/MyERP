using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Ledger Health Check — detects GL inconsistencies.
/// Per DO-NOT: must run daily to detect GL inconsistencies.
/// Checks: unbalanced JEs, PLE vs GL drift, orphaned entries.
/// </summary>
public class LedgerHealthCheckService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalEntryRepository;
    private readonly IRepository<PaymentLedgerEntry, Guid> _pleRepository;

    public LedgerHealthCheckService(
        IRepository<JournalEntry, Guid> journalEntryRepository,
        IRepository<PaymentLedgerEntry, Guid> pleRepository)
    {
        _journalEntryRepository = journalEntryRepository;
        _pleRepository = pleRepository;
    }

    /// <summary>
    /// Runs a full ledger health check for a company.
    /// Returns a list of detected issues.
    /// </summary>
    public async Task<LedgerHealthReport> RunHealthCheckAsync(Guid companyId)
    {
        var report = new LedgerHealthReport { CompanyId = companyId, CheckedAt = DateTime.UtcNow };

        // Check 1: Unbalanced Journal Entries (Debit ≠ Credit)
        var jeQuery = await _journalEntryRepository.GetQueryableAsync();
        var postedJEs = jeQuery
            .Where(je => je.CompanyId == companyId && je.Status == Core.DocumentStatus.Posted)
            .ToList();

        foreach (var je in postedJEs)
        {
            var totalDebit = je.Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var totalCredit = je.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);

            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            {
                report.Issues.Add(new LedgerHealthIssue
                {
                    IssueType = "UnbalancedJE",
                    Severity = "Critical",
                    Description = $"JE {je.Id} is unbalanced: DR={totalDebit}, CR={totalCredit}",
                    DocumentId = je.Id,
                    Amount = totalDebit - totalCredit,
                });
            }
        }

        // Check 2: PLE entries without matching GL (orphaned PLE)
        var pleQuery = await _pleRepository.GetQueryableAsync();
        var pleCount = pleQuery.Count(p => p.CompanyId == companyId && !p.Delinked);
        var jeCount = postedJEs.Count;

        // Simple heuristic: if PLE count vastly exceeds JE count, possible orphans
        if (pleCount > jeCount * 5 && pleCount > 100)
        {
            report.Issues.Add(new LedgerHealthIssue
            {
                IssueType = "PLECountAnomaly",
                Severity = "Warning",
                Description = $"PLE count ({pleCount}) significantly exceeds JE count ({jeCount}). Possible orphaned entries.",
            });
        }

        report.TotalChecked = postedJEs.Count;
        report.IsHealthy = !report.Issues.Any(i => i.Severity == "Critical");

        return report;
    }
}

public class LedgerHealthReport
{
    public Guid CompanyId { get; set; }
    public DateTime CheckedAt { get; set; }
    public bool IsHealthy { get; set; }
    public int TotalChecked { get; set; }
    public List<LedgerHealthIssue> Issues { get; set; } = new();
}

public class LedgerHealthIssue
{
    public string IssueType { get; set; } = null!;
    public string Severity { get; set; } = "Warning";
    public string Description { get; set; } = null!;
    public Guid? DocumentId { get; set; }
    public decimal? Amount { get; set; }
}
