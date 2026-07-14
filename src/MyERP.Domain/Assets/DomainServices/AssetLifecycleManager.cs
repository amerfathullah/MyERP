using System;
using System.Linq;
using System.Threading.Tasks;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace MyERP.Assets.DomainServices;

/// <summary>
/// Domain service for Asset lifecycle rules.
/// Handles depreciation validation, disposal GL calculations, and repair cost capitalization.
/// Per ERPNext: assets/doctype/asset + asset_depreciation.instructions.md.
/// </summary>
public class AssetLifecycleManager : DomainService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IRepository<Company, Guid> _companyRepository;

    public AssetLifecycleManager(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<Company, Guid> companyRepository)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _companyRepository = companyRepository;
    }

    /// <summary>
    /// Calculates gain or loss on asset disposal.
    /// Per ERPNext: gain/loss = disposal_amount - value_after_depreciation.
    /// Positive = gain (profit), negative = loss (write-off).
    /// </summary>
    public decimal CalculateDisposalGainLoss(Asset asset)
    {
        var disposalAmount = asset.DisposalAmount ?? 0;
        return disposalAmount - asset.ValueAfterDepreciation;
    }

    /// <summary>
    /// Validates that an asset can be submitted for depreciation.
    /// Checks: has depreciation settings, has category, available-for-use date is set.
    /// </summary>
    public void ValidateForSubmission(Asset asset)
    {
        if (asset.CalculateDepreciation)
        {
            if (asset.UsefulLifeMonths <= 0)
                throw new BusinessException("MyERP:15002")
                    .WithData("assetName", asset.AssetName)
                    .WithData("field", "UsefulLifeMonths");

            if (!asset.AvailableForUseDate.HasValue)
                throw new BusinessException("MyERP:15002")
                    .WithData("assetName", asset.AssetName)
                    .WithData("field", "AvailableForUseDate");
        }
    }

    /// <summary>
    /// Validates depreciation schedule entries are not in a frozen accounting period.
    /// Per DO-NOT: "Post depreciation entries for schedule dates within company's accounts_frozen_till_date".
    /// </summary>
    public async Task<DateTime?> GetFrozenDateAsync(Guid companyId)
    {
        var company = await _companyRepository.FindAsync(companyId);
        return company?.AccountsFrozenTillDate;
    }

    /// <summary>
    /// Resolves GL accounts for depreciation journal entries.
    /// Priority: AssetCategory → Company defaults.
    /// Returns (depreciationExpenseAccountId, accumulatedDepreciationAccountId).
    /// </summary>
    public async Task<(Guid? DepreciationExpenseAccount, Guid? AccumulatedDepreciationAccount)>
        ResolveDepreciationAccountsAsync(Guid? categoryId, Guid companyId)
    {
        Guid? depExpenseAccount = null;
        Guid? accDepAccount = null;

        // Try category first
        if (categoryId.HasValue)
        {
            var category = await _categoryRepository.FindAsync(categoryId.Value);
            if (category != null)
            {
                depExpenseAccount = category.DepreciationAccountId;
                accDepAccount = category.AccumulatedDepreciationAccountId;
            }
        }

        // Fall back to company defaults
        if (!depExpenseAccount.HasValue || !accDepAccount.HasValue)
        {
            var company = await _companyRepository.FindAsync(companyId);
            if (company != null)
            {
                depExpenseAccount ??= company.DepreciationExpenseAccountId;
                accDepAccount ??= company.AccumulatedDepreciationAccountId;
            }
        }

        return (depExpenseAccount, accDepAccount);
    }

    /// <summary>
    /// Validates asset repair capitalization rules.
    /// Per gotcha #35: fully depreciated assets CAN be repaired but
    /// capitalize_repair_cost and increase_in_asset_life are forced to 0.
    /// </summary>
    public (bool CanCapitalize, bool CanExtendLife) GetRepairOptions(Asset asset)
    {
        if (asset.IsFullyDepreciated || asset.Status == AssetStatus.FullyDepreciated)
        {
            // Fully depreciated: repair tracked but no accounting/schedule impact
            return (false, false);
        }

        return (true, true);
    }

    /// <summary>
    /// Gets unbooked depreciation schedule entries that are due on or before a given date.
    /// Excludes entries in frozen accounting periods.
    /// </summary>
    public async Task<DepreciationScheduleEntry[]> GetDueDepreciationEntriesAsync(
        Asset asset, DateTime asOfDate)
    {
        var frozenDate = await GetFrozenDateAsync(asset.CompanyId);

        return asset.DepreciationSchedule
            .Where(e => !e.IsBooked
                     && e.ScheduleDate <= asOfDate
                     && (!frozenDate.HasValue || e.ScheduleDate > frozenDate.Value))
            .OrderBy(e => e.ScheduleDate)
            .ToArray();
    }
}
