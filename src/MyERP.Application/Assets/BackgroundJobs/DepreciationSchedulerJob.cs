using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace MyERP.Assets.BackgroundJobs;

/// <summary>
/// Depreciation Scheduler — posts depreciation journal entries for assets
/// with schedule entries due on or before today.
/// 
/// ERPNext equivalent: assets/doctype/asset/depreciation.py (daily scheduled)
/// 
/// For each unbooked schedule entry where date <= today:
/// 1. Create JE: DR Depreciation Expense, CR Accumulated Depreciation
/// 2. Update DepreciationScheduleEntry.IsBooked = true
/// 3. Update Asset.ValueAfterDepreciation
/// 4. If fully depreciated → mark asset status
/// 
/// Skips: Draft assets, frozen period dates, already-booked entries
/// </summary>
public class DepreciationSchedulerJob : AsyncBackgroundJob<DepreciationSchedulerArgs>, ITransientDependency
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _assetCategoryRepository;
    private readonly IRepository<JournalEntry, Guid> _journalRepository;
    private readonly IRepository<Company, Guid> _companyRepository;
    private readonly IRepository<FinanceBook, Guid> _financeBookRepository;
    private readonly IRepository<FiscalYear, Guid> _fiscalYearRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<DepreciationSchedulerJob> _logger;

    public DepreciationSchedulerJob(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> assetCategoryRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<Company, Guid> companyRepository,
        IRepository<FinanceBook, Guid> financeBookRepository,
        IRepository<FiscalYear, Guid> fiscalYearRepository,
        IGuidGenerator guidGenerator,
        ILogger<DepreciationSchedulerJob> logger)
    {
        _assetRepository = assetRepository;
        _assetCategoryRepository = assetCategoryRepository;
        _journalRepository = journalRepository;
        _companyRepository = companyRepository;
        _financeBookRepository = financeBookRepository;
        _fiscalYearRepository = fiscalYearRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DepreciationSchedulerArgs args)
    {
        var today = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("Running depreciation scheduler for date {Date}, company {CompanyId}", today, args.CompanyId);

        // Check frozen date — skip entries within frozen period
        var company = await _companyRepository.GetAsync(args.CompanyId);

        // Resolve the open fiscal year covering today — no caller has ever actually supplied
        // FiscalYearId (both NightlyProcessingWorker enqueue sites omit it), which silently
        // skipped every single asset below. Resolve it here so the job is self-sufficient
        // instead of depending on a value nothing ever populates.
        var fiscalYearId = args.FiscalYearId;
        if (!fiscalYearId.HasValue)
        {
            var fiscalYear = await _fiscalYearRepository.FindAsync(fy =>
                fy.CompanyId == args.CompanyId && !fy.IsClosed &&
                fy.StartDate <= today && fy.EndDate >= today);
            fiscalYearId = fiscalYear?.Id;
        }

        if (!fiscalYearId.HasValue)
        {
            _logger.LogWarning("Depreciation scheduler: no open fiscal year for company {CompanyId} on {Date}, skipping", args.CompanyId, today);
            return;
        }

        var assetQuery = await _assetRepository.GetQueryableAsync();
        var assets = assetQuery
            .Where(a => a.CompanyId == args.CompanyId
                && a.CalculateDepreciation
                && a.Status != AssetStatus.Draft
                && a.Status != AssetStatus.FullyDepreciated
                && a.Status != AssetStatus.Cancelled
                && a.DepreciationSchedule.Any(d => !d.IsBooked && d.ScheduleDate <= today))
            .ToList();

        int entriesPosted = 0;

        foreach (var asset in assets)
        {
            try
            {
            // Group unbooked entries by finance book for multi-book depreciation
            var unbookedEntries = asset.DepreciationSchedule
                .Where(d => !d.IsBooked && d.ScheduleDate <= today)
                .OrderBy(d => d.ScheduleDate)
                .ToList();

            // Group by FinanceBookId to create separate JEs per book (per gotcha #636)
            var entriesByBook = unbookedEntries.GroupBy(e => e.FinanceBookId);

            // Resolve accounts once per asset (not per entry) to avoid N+1
            Guid? depreciationExpenseAccountId;
            Guid? accumulatedDepAccountId;
            if (asset.AssetCategoryId.HasValue)
            {
                var category = await _assetCategoryRepository.GetAsync(asset.AssetCategoryId.Value);
                depreciationExpenseAccountId = category.DepreciationAccountId ?? company.DepreciationExpenseAccountId;
                accumulatedDepAccountId = category.AccumulatedDepreciationAccountId ?? company.AccumulatedDepreciationAccountId;
            }
            else
            {
                depreciationExpenseAccountId = company.DepreciationExpenseAccountId;
                accumulatedDepAccountId = company.AccumulatedDepreciationAccountId;
            }

            // Skip this asset if depreciation accounts are not configured
            if (!depreciationExpenseAccountId.HasValue || !accumulatedDepAccountId.HasValue)
            {
                Logger.LogWarning("Skipping depreciation for asset {AssetId}: depreciation accounts not configured", asset.Id);
                continue;
            }

            foreach (var bookGroup in entriesByBook)
            {
                // Resolve finance book name for JE tagging
                string? financeBookName = null;
                if (bookGroup.Key.HasValue)
                {
                    var fb = await _financeBookRepository.FindAsync(bookGroup.Key.Value);
                    financeBookName = fb?.Name;
                }

                // Find matching DepreciationDetail for this book (for per-book ValueAfterDepreciation)
                var detail = asset.DepreciationDetails?
                    .FirstOrDefault(d => d.FinanceBookId == bookGroup.Key);

                foreach (var entry in bookGroup.OrderBy(e => e.ScheduleDate))
                {
                    // Skip if schedule date falls in frozen accounting period
                    if (company.AccountsFrozenTillDate.HasValue && entry.ScheduleDate <= company.AccountsFrozenTillDate.Value)
                        continue;

                    var journal = new JournalEntry(
                        _guidGenerator.Create(),
                        asset.CompanyId,
                        fiscalYearId.Value,
                        entry.ScheduleDate,
                        asset.TenantId);

                    // Round depreciation amount to currency precision (2 decimal places) to match JE debit (ERPNext PR #56964)
                    var deprAmount = Math.Round(entry.DepreciationAmount, 2);

                    // Tag JE lines with finance book for multi-book GL separation
                    journal.AddLine(depreciationExpenseAccountId.Value, deprAmount, true,
                        $"Depreciation of {asset.AssetName}" + (financeBookName != null ? $" [{financeBookName}]" : ""));
                    journal.AddLine(accumulatedDepAccountId.Value, deprAmount, false,
                        $"Accumulated depreciation - {asset.AssetName}" + (financeBookName != null ? $" [{financeBookName}]" : ""));

                    // Set finance book on all lines (after AddLine, which returns void)
                    if (financeBookName != null)
                    {
                        foreach (var line in journal.Lines)
                            line.FinanceBook = financeBookName;
                    }

                    journal.Post();
                    await _journalRepository.InsertAsync(journal);

                    // Mark entry as booked
                    entry.IsBooked = true;
                    entry.JournalEntryId = journal.Id;

                    // Update per-book book value if DepreciationDetail exists
                    if (detail != null)
                    {
                        detail.ValueAfterDepreciation -= deprAmount;
                        if (detail.ValueAfterDepreciation < 0) detail.ValueAfterDepreciation = 0;
                    }

                    // Update overall asset book value (primary/default book drives asset status)
                    asset.ValueAfterDepreciation -= deprAmount;
                    if (asset.ValueAfterDepreciation <= 0)
                    {
                        asset.ValueAfterDepreciation = 0;
                        asset.MarkFullyDepreciated();
                    }
                    else
                    {
                        asset.MarkPartiallyDepreciated();
                    }

                    entriesPosted++;
                }
            }

            await _assetRepository.UpdateAsync(asset);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Depreciation failed for asset {AssetId}, skipping to next", asset.Id);
            }
        }

        _logger.LogInformation("Depreciation scheduler posted {Count} entries for company {CompanyId}", entriesPosted, args.CompanyId);
    }
}

public class DepreciationSchedulerArgs
{
    public Guid CompanyId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTime? AsOfDate { get; set; }
    public Guid? FiscalYearId { get; set; }
}
