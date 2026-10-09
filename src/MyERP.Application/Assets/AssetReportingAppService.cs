using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using MyERP.Accounting.Entities;
using MyERP.Assets.Entities;
using MyERP.Core;
using MyERP.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace MyERP.Assets;

[Authorize(MyERPPermissions.Assets.Default)]
public class AssetReportingAppService : ApplicationService, IAssetReportingAppService
{
    private readonly IRepository<Asset, Guid> _assetRepository;
    private readonly IRepository<AssetCategory, Guid> _categoryRepository;
    private readonly IRepository<AssetValueAdjustment, Guid> _adjustmentRepository;
    private readonly IRepository<FinanceBook, Guid> _financeBookRepository;
    private readonly IRepository<AssetCapitalization, Guid> _capitalizationRepository;

    public AssetReportingAppService(
        IRepository<Asset, Guid> assetRepository,
        IRepository<AssetCategory, Guid> categoryRepository,
        IRepository<AssetValueAdjustment, Guid> adjustmentRepository,
        IRepository<FinanceBook, Guid> financeBookRepository,
        IRepository<AssetCapitalization, Guid> capitalizationRepository)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _adjustmentRepository = adjustmentRepository;
        _financeBookRepository = financeBookRepository;
        _capitalizationRepository = capitalizationRepository;
    }

    /// <summary>
    /// Generates Asset Depreciations and Balances report.
    /// Per ERPNext PR #59925:
    /// - Disposed assets on ToDate subtracted once (ValueAsOnToDate nets to zero).
    /// - Value adjustments during the period are retained for assets disposed in the period and added to disposal amounts.
    /// - Excludes non-default finance books when none is selected (or when a specific finance book is selected).
    /// - Opening accumulated depreciation nets reversals before FromDate.
    /// - Supports grouping by Asset Category or Asset.
    /// </summary>
    public async Task<AssetDepreciationsAndBalancesReportDto> GetAssetDepreciationsAndBalancesAsync(AssetDepreciationsAndBalancesRequestDto input)
    {
        var fromDate = input.FromDate.Date;
        var toDate = input.ToDate.Date;

        // Resolve effective finance book (user selection or company default)
        var defaultBook = await _financeBookRepository.FirstOrDefaultAsync(fb => fb.CompanyId == input.CompanyId && fb.IsDefault);
        var effectiveFinanceBookId = input.FinanceBookId ?? defaultBook?.Id;

        // Assets query
        var assetQueryable = (await _assetRepository.WithDetailsAsync(a => a.DepreciationSchedule, a => a.DepreciationDetails)).AsQueryable();
        assetQueryable = assetQueryable.Where(a => a.CompanyId == input.CompanyId
                                              && a.Status != AssetStatus.Draft
                                              && a.Status != AssetStatus.Cancelled
                                              && a.PurchaseDate.Date <= toDate);

        if (input.AssetCategoryId.HasValue)
        {
            assetQueryable = assetQueryable.Where(a => a.AssetCategoryId == input.AssetCategoryId.Value);
        }

        if (input.AssetId.HasValue)
        {
            assetQueryable = assetQueryable.Where(a => a.Id == input.AssetId.Value);
        }

        // Exclude assets consumed in capitalization before FromDate
        var capitalizationQueryable = (await _capitalizationRepository.WithDetailsAsync(c => c.ConsumedAssets)).AsQueryable();
        var capitalizedBeforeFromDateAssetIds = capitalizationQueryable
            .Where(c => c.CompanyId == input.CompanyId && c.Status == AssetCapitalizationStatus.Submitted && c.PostingDate.Date < fromDate)
            .SelectMany(c => c.ConsumedAssets.Select(ca => ca.AssetId))
            .ToHashSet();

        var candidateAssets = assetQueryable
            .Where(a => !capitalizedBeforeFromDateAssetIds.Contains(a.Id))
            .ToList();

        // If specific FinanceBookId filter is selected, filter assets that belong to that book
        if (input.FinanceBookId.HasValue)
        {
            candidateAssets = candidateAssets
                .Where(a => a.DepreciationDetails.Any(d => d.FinanceBookId == input.FinanceBookId.Value)
                         || (!a.DepreciationDetails.Any() && a.DepreciationSchedule.Any(s => s.FinanceBookId == input.FinanceBookId.Value || s.FinanceBookId == null)))
                .ToList();
        }

        // Adjustments query matching finance book condition
        var adjQueryable = await _adjustmentRepository.GetQueryableAsync();
        var adjustments = adjQueryable
            .Where(a => a.CompanyId == input.CompanyId
                     && a.Status == DocumentStatus.Submitted
                     && a.Date.Date <= toDate
                     && (a.FinanceBookId == null || a.FinanceBookId == effectiveFinanceBookId))
            .ToList();

        var adjustmentsByAsset = adjustments
            .GroupBy(a => a.AssetId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Category names lookup
        var categories = (await _categoryRepository.GetListAsync())
            .ToDictionary(c => c.Id, c => c.CategoryName);

        var assetRows = new List<AssetDepreciationsAndBalancesRowDto>();

        foreach (var asset in candidateAssets)
        {
            var isHeldAtFromDate = asset.PurchaseDate.Date < fromDate
                && (!asset.DisposalDate.HasValue || asset.DisposalDate.Value.Date >= fromDate);

            var openingCost = isHeldAtFromDate ? asset.TotalAssetCost : 0m;
            var newPurchaseCost = (asset.PurchaseDate.Date >= fromDate && asset.PurchaseDate.Date <= toDate) ? asset.TotalAssetCost : 0m;

            var disposedInPeriod = asset.DisposalDate.HasValue
                && asset.DisposalDate.Value.Date >= fromDate
                && asset.DisposalDate.Value.Date <= toDate;

            adjustmentsByAsset.TryGetValue(asset.Id, out var assetAdjs);
            assetAdjs ??= new List<AssetValueAdjustment>();

            var adjBeforeFromDate = isHeldAtFromDate
                ? assetAdjs.Where(a => a.Date.Date < fromDate).Sum(a => a.DifferenceAmount)
                : 0m;

            var adjTillToDate = (isHeldAtFromDate || (asset.PurchaseDate.Date >= fromDate && asset.PurchaseDate.Date <= toDate))
                ? assetAdjs.Where(a => a.Date.Date <= toDate).Sum(a => a.DifferenceAmount)
                : 0m;

            var adjDuringPeriod = adjTillToDate - adjBeforeFromDate;

            decimal soldValue = 0m;
            decimal scrappedValue = 0m;
            decimal capitalizedValue = 0m;

            if (disposedInPeriod)
            {
                var adjTillDisposal = assetAdjs
                    .Where(a => a.Date.Date <= asset.DisposalDate!.Value.Date && a.Date.Date <= toDate)
                    .Sum(a => a.DifferenceAmount);

                var totalDisposalBase = asset.TotalAssetCost + adjTillDisposal;
                if (asset.Status == AssetStatus.Sold) soldValue = totalDisposalBase;
                else if (asset.Status == AssetStatus.Scrapped) scrappedValue = totalDisposalBase;
                else if (asset.Status == AssetStatus.Capitalized) capitalizedValue = totalDisposalBase;
            }

            var valueAsOnFromDate = openingCost + adjBeforeFromDate;
            var valueAsOnToDate = valueAsOnFromDate + newPurchaseCost - soldValue - scrappedValue - capitalizedValue + adjDuringPeriod;

            // Depreciation entries filtered by effective finance book
            var scheduleEntries = asset.DepreciationSchedule
                .Where(s => s.FinanceBookId == null || s.FinanceBookId == effectiveFinanceBookId)
                .ToList();

            decimal openingAccDepr = 0m;
            if (isHeldAtFromDate)
            {
                openingAccDepr += asset.OpeningAccumulatedDepreciation;
                openingAccDepr += scheduleEntries
                    .Where(s => s.IsBooked && s.ScheduleDate.Date < fromDate)
                    .Sum(s => s.DepreciationAmount);
            }

            var deprDuringPeriod = scheduleEntries
                .Where(s => s.IsBooked && s.ScheduleDate.Date >= fromDate && s.ScheduleDate.Date <= toDate
                    && (!asset.DisposalDate.HasValue || s.ScheduleDate.Date <= asset.DisposalDate.Value.Date))
                .Sum(s => s.DepreciationAmount);

            decimal deprEliminatedOnDisposal = 0m;
            if (disposedInPeriod)
            {
                deprEliminatedOnDisposal = asset.OpeningAccumulatedDepreciation + scheduleEntries
                    .Where(s => s.IsBooked && s.ScheduleDate.Date <= asset.DisposalDate!.Value.Date)
                    .Sum(s => s.DepreciationAmount);
            }

            decimal deprEliminatedViaReversal = scheduleEntries
                .Where(s => !s.IsBooked && s.JournalEntryId.HasValue && s.ScheduleDate.Date >= fromDate && s.ScheduleDate.Date <= toDate)
                .Sum(s => s.DepreciationAmount);

            var accDeprAsOnToDate = openingAccDepr + deprDuringPeriod - deprEliminatedOnDisposal - deprEliminatedViaReversal;

            var netAssetValueAsOnFromDate = valueAsOnFromDate - openingAccDepr;
            var netAssetValueAsOnToDate = valueAsOnToDate - accDeprAsOnToDate;

            string? categoryName = null;
            if (asset.AssetCategoryId.HasValue && categories.TryGetValue(asset.AssetCategoryId.Value, out var cName))
            {
                categoryName = cName;
            }

            assetRows.Add(new AssetDepreciationsAndBalancesRowDto
            {
                AssetCategoryId = asset.AssetCategoryId,
                AssetCategoryName = categoryName,
                AssetId = asset.Id,
                AssetNumber = asset.AssetNumber,
                AssetName = asset.AssetName,
                ValueAsOnFromDate = valueAsOnFromDate,
                ValueOfNewPurchase = newPurchaseCost,
                ValueOfSoldAsset = soldValue,
                ValueOfScrappedAsset = scrappedValue,
                ValueOfCapitalizedAsset = capitalizedValue,
                AdjustmentDuringPeriod = adjDuringPeriod,
                ValueAsOnToDate = valueAsOnToDate,
                AccumulatedDepreciationAsOnFromDate = openingAccDepr,
                DepreciationAmountDuringThePeriod = deprDuringPeriod,
                DepreciationEliminatedDuringThePeriod = deprEliminatedOnDisposal,
                DepreciationEliminatedViaReversal = deprEliminatedViaReversal,
                AccumulatedDepreciationAsOnToDate = accDeprAsOnToDate,
                NetAssetValueAsOnFromDate = netAssetValueAsOnFromDate,
                NetAssetValueAsOnToDate = netAssetValueAsOnToDate,
            });
        }

        List<AssetDepreciationsAndBalancesRowDto> finalRows;
        if (string.Equals(input.GroupBy, "Asset", StringComparison.OrdinalIgnoreCase))
        {
            finalRows = assetRows.OrderBy(r => r.AssetNumber).ToList();
        }
        else
        {
            // Group by Asset Category
            finalRows = assetRows
                .GroupBy(r => (r.AssetCategoryId, r.AssetCategoryName))
                .Select(g => new AssetDepreciationsAndBalancesRowDto
                {
                    AssetCategoryId = g.Key.AssetCategoryId,
                    AssetCategoryName = g.Key.AssetCategoryName ?? "Uncategorized",
                    AssetId = null,
                    AssetNumber = null,
                    AssetName = null,
                    ValueAsOnFromDate = g.Sum(r => r.ValueAsOnFromDate),
                    ValueOfNewPurchase = g.Sum(r => r.ValueOfNewPurchase),
                    ValueOfSoldAsset = g.Sum(r => r.ValueOfSoldAsset),
                    ValueOfScrappedAsset = g.Sum(r => r.ValueOfScrappedAsset),
                    ValueOfCapitalizedAsset = g.Sum(r => r.ValueOfCapitalizedAsset),
                    AdjustmentDuringPeriod = g.Sum(r => r.AdjustmentDuringPeriod),
                    ValueAsOnToDate = g.Sum(r => r.ValueAsOnToDate),
                    AccumulatedDepreciationAsOnFromDate = g.Sum(r => r.AccumulatedDepreciationAsOnFromDate),
                    DepreciationAmountDuringThePeriod = g.Sum(r => r.DepreciationAmountDuringThePeriod),
                    DepreciationEliminatedDuringThePeriod = g.Sum(r => r.DepreciationEliminatedDuringThePeriod),
                    DepreciationEliminatedViaReversal = g.Sum(r => r.DepreciationEliminatedViaReversal),
                    AccumulatedDepreciationAsOnToDate = g.Sum(r => r.AccumulatedDepreciationAsOnToDate),
                    NetAssetValueAsOnFromDate = g.Sum(r => r.NetAssetValueAsOnFromDate),
                    NetAssetValueAsOnToDate = g.Sum(r => r.NetAssetValueAsOnToDate),
                })
                .OrderBy(r => r.AssetCategoryName)
                .ToList();
        }

        return new AssetDepreciationsAndBalancesReportDto
        {
            CompanyId = input.CompanyId,
            FromDate = fromDate,
            ToDate = toDate,
            GroupBy = input.GroupBy,
            Rows = finalRows,
        };
    }
}
