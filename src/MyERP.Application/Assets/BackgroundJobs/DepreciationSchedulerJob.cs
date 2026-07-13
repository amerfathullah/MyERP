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
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<DepreciationSchedulerJob> _logger;

    public DepreciationSchedulerJob(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> assetCategoryRepository,
        IRepository<JournalEntry, Guid> journalRepository,
        IRepository<Company, Guid> companyRepository,
        IGuidGenerator guidGenerator,
        ILogger<DepreciationSchedulerJob> logger)
    {
        _assetRepository = assetRepository;
        _assetCategoryRepository = assetCategoryRepository;
        _journalRepository = journalRepository;
        _companyRepository = companyRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public override async Task ExecuteAsync(DepreciationSchedulerArgs args)
    {
        var today = args.AsOfDate ?? DateTime.UtcNow.Date;
        _logger.LogInformation("Running depreciation scheduler for date {Date}, company {CompanyId}", today, args.CompanyId);

        // Check frozen date — skip entries within frozen period
        var company = await _companyRepository.GetAsync(args.CompanyId);

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
            var unbookedEntries = asset.DepreciationSchedule
                .Where(d => !d.IsBooked && d.ScheduleDate <= today)
                .OrderBy(d => d.ScheduleDate)
                .ToList();

            foreach (var entry in unbookedEntries)
            {
                // Skip if schedule date falls in frozen accounting period
                if (company.AccountsFrozenTillDate.HasValue && entry.ScheduleDate <= company.AccountsFrozenTillDate.Value)
                    continue;

                // Create depreciation JE: DR Expense, CR Accumulated
                // Resolve accounts from AssetCategory or Company defaults
                var depreciationExpenseAccountId = asset.AssetCategoryId.HasValue
                    ? (await _assetCategoryRepository.GetAsync(asset.AssetCategoryId.Value)).DepreciationAccountId
                      ?? company.DepreciationExpenseAccountId ?? Guid.Empty
                    : company.DepreciationExpenseAccountId ?? Guid.Empty;
                var accumulatedDepAccountId = asset.AssetCategoryId.HasValue
                    ? (await _assetCategoryRepository.GetAsync(asset.AssetCategoryId.Value)).AccumulatedDepreciationAccountId
                      ?? company.AccumulatedDepreciationAccountId ?? Guid.Empty
                    : company.AccumulatedDepreciationAccountId ?? Guid.Empty;

                var journal = new JournalEntry(
                    _guidGenerator.Create(),
                    asset.CompanyId,
                    args.FiscalYearId ?? Guid.Empty,
                    entry.ScheduleDate,
                    asset.TenantId);

                journal.AddLine(depreciationExpenseAccountId, entry.DepreciationAmount, true,
                    $"Depreciation of {asset.AssetName}");
                journal.AddLine(accumulatedDepAccountId, entry.DepreciationAmount, false,
                    $"Accumulated depreciation - {asset.AssetName}");

                journal.Post();
                await _journalRepository.InsertAsync(journal);

                // Mark entry as booked
                entry.IsBooked = true;
                entry.JournalEntryId = journal.Id;

                // Update asset book value
                asset.ValueAfterDepreciation -= entry.DepreciationAmount;
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

            await _assetRepository.UpdateAsync(asset);
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
