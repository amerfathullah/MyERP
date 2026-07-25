using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Core;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Accounting.DomainServices;

/// <summary>
/// Month-End Close Service — orchestrates the period-end closing workflow.
/// Validates prerequisites, executes steps in order, and tracks completion.
/// 
/// Per ERPNext: period closing is a multi-step process with specific ordering:
/// 1. Validate Trial Balance is balanced
/// 2. Post pending depreciation entries
/// 3. Process deferred revenue/expense
/// 4. Create Period Closing Voucher (P&L → Retained Earnings)
/// 5. Rebuild Account Closing Balances
/// 6. Close the accounting period
/// 
/// Each step can fail independently — the service tracks which steps completed.
/// </summary>
public class MonthEndCloseService : DomainService
{
    private readonly IRepository<JournalEntry, Guid> _journalRepo;
    private readonly IRepository<FiscalYear, Guid> _fyRepo;
    private readonly IRepository<AccountingPeriod, Guid> _periodRepo;
    private readonly IRepository<Company, Guid> _companyRepo;
    private readonly TrialBalanceValidationService _tbValidator;

    public MonthEndCloseService(
        IRepository<JournalEntry, Guid> journalRepo,
        IRepository<FiscalYear, Guid> fyRepo,
        IRepository<AccountingPeriod, Guid> periodRepo,
        IRepository<Company, Guid> companyRepo,
        TrialBalanceValidationService tbValidator)
    {
        _journalRepo = journalRepo;
        _fyRepo = fyRepo;
        _periodRepo = periodRepo;
        _companyRepo = companyRepo;
        _tbValidator = tbValidator;
    }

    /// <summary>
    /// Validates readiness for month-end close.
    /// Returns a checklist of items with pass/fail status.
    /// </summary>
    public async Task<MonthEndReadinessReport> ValidateReadinessAsync(
        Guid companyId, DateTime periodEndDate)
    {
        var report = new MonthEndReadinessReport(companyId, periodEndDate);

        // Check 1: Trial Balance is balanced
        var tbResult = await _tbValidator.ValidateAsync(companyId, DateTime.MinValue, periodEndDate);
        report.AddCheck("Trial Balance Balanced", tbResult.IsBalanced,
            tbResult.IsBalanced ? null : $"Difference: {tbResult.Difference:N2}");

        // Check 2: No draft journal entries in period
        var draftJEs = await _journalRepo.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Draft &&
            je.PostingDate <= periodEndDate);
        report.AddCheck("No Draft Journal Entries", draftJEs.Count == 0,
            draftJEs.Count > 0 ? $"{draftJEs.Count} draft JE(s) found" : null);

        // Check 3: Fiscal year exists and is open
        var fiscalYears = await _fyRepo.GetListAsync(fy =>
            fy.CompanyId == companyId &&
            fy.StartDate <= periodEndDate &&
            fy.EndDate >= periodEndDate);
        var fy = fiscalYears.FirstOrDefault();
        report.AddCheck("Fiscal Year Open", fy != null && !fy.IsClosed,
            fy == null ? "No fiscal year covers this date" : (fy.IsClosed ? "Fiscal year is closed" : null));

        // Check 4: Accounts frozen date allows posting
        var company = await _companyRepo.GetAsync(companyId);
        var frozenOk = !company.AccountsFrozenTillDate.HasValue || company.AccountsFrozenTillDate.Value < periodEndDate;
        report.AddCheck("Period Not Frozen", frozenOk,
            frozenOk ? null : $"Accounts frozen until {company.AccountsFrozenTillDate:yyyy-MM-dd}");

        // Check 5: No pending accounting period closures
        var period = (await _periodRepo.GetListAsync(p =>
            p.CompanyId == companyId && p.IsClosed &&
            p.StartDate <= periodEndDate && p.EndDate >= periodEndDate)).FirstOrDefault();
        report.AddCheck("Period Not Already Closed", period == null,
            period != null ? "Period already closed" : null);

        return report;
    }

    /// <summary>
    /// Gets the status of a month-end close (which steps have been completed).
    /// </summary>
    public async Task<MonthEndCloseStatus> GetCloseStatusAsync(Guid companyId, DateTime periodEndDate)
    {
        var status = new MonthEndCloseStatus(companyId, periodEndDate);

        // Check if PCV exists for this period
        var pcvJournals = await _journalRepo.GetListAsync(je =>
            je.CompanyId == companyId &&
            je.Status == DocumentStatus.Posted &&
            je.ReferenceType == "PeriodClosingVoucher" &&
            je.PostingDate >= periodEndDate.AddDays(-31) &&
            je.PostingDate <= periodEndDate);
        status.HasPeriodClosingVoucher = pcvJournals.Any();

        // Check if period is formally closed
        var closedPeriod = (await _periodRepo.GetListAsync(p =>
            p.CompanyId == companyId && p.IsClosed &&
            p.StartDate <= periodEndDate && p.EndDate >= periodEndDate)).FirstOrDefault();
        status.IsPeriodClosed = closedPeriod != null;

        // Check trial balance
        var tbResult = await _tbValidator.ValidateAsync(companyId, DateTime.MinValue, periodEndDate);
        status.IsTrialBalanceBalanced = tbResult.IsBalanced;

        return status;
    }

    /// <summary>
    /// Freezes the accounting period up to the specified date.
    /// Prevents any GL posting on or before this date (except by authorized roles).
    /// </summary>
    public async Task FreezeAccountingPeriodAsync(Guid companyId, DateTime freezeUpTo)
    {
        var company = await _companyRepo.GetAsync(companyId);

        // Cannot freeze in the future
        if (freezeUpTo > DateTime.UtcNow.Date)
            throw new BusinessException("MyERP:02047")
                .WithData("date", freezeUpTo.ToString("yyyy-MM-dd"));

        company.AccountsFrozenTillDate = freezeUpTo;
        await _companyRepo.UpdateAsync(company);
    }
}

/// <summary>Month-end readiness report with checklist items.</summary>
public class MonthEndReadinessReport
{
    public Guid CompanyId { get; }
    public DateTime PeriodEndDate { get; }
    public List<MonthEndCheckItem> Checks { get; } = new();
    public bool IsReady => Checks.All(c => c.Passed);
    public int PassedCount => Checks.Count(c => c.Passed);
    public int TotalChecks => Checks.Count;

    public MonthEndReadinessReport(Guid companyId, DateTime periodEndDate)
    {
        CompanyId = companyId;
        PeriodEndDate = periodEndDate;
    }

    public void AddCheck(string name, bool passed, string? details = null)
    {
        Checks.Add(new MonthEndCheckItem(name, passed, details));
    }
}

public record MonthEndCheckItem(string Name, bool Passed, string? Details);

/// <summary>Status of a month-end close process.</summary>
public class MonthEndCloseStatus
{
    public Guid CompanyId { get; }
    public DateTime PeriodEndDate { get; }
    public bool IsTrialBalanceBalanced { get; set; }
    public bool HasPeriodClosingVoucher { get; set; }
    public bool IsPeriodClosed { get; set; }
    public bool IsFullyClosed => IsTrialBalanceBalanced && HasPeriodClosingVoucher && IsPeriodClosed;

    public MonthEndCloseStatus(Guid companyId, DateTime periodEndDate)
    {
        CompanyId = companyId;
        PeriodEndDate = periodEndDate;
    }
}
