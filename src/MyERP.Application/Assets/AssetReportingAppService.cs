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
        var bookQueryable = await _financeBookRepository.GetQueryableAsync();
        var defaultBook = bookQueryable.FirstOrDefault(fb => fb.CompanyId == input.CompanyId && fb.IsDefault);
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

    /// <summary>
    /// Generates the Fixed Asset Register report.
    /// Per ERPNext fixed_asset_register.py and PR #59928 (commit 59862127b8):
    /// - Disposed assets carry 0 AssetValue if disposed on or before report end date (or when no end date is specified).
    /// - Assets disposed AFTER the report end date retain their computed value as of the report end date:
    ///   GrossPurchaseAmount - OpeningAccumulatedDepreciation - DepreciatedAmount + RevaluationAmount.
    /// - Validates IncludeDefaultBookAssets + FinanceBookId conflict.
    /// - Supports grouping by "Asset Category", "Location", or individual assets.
    /// </summary>
    public async Task<FixedAssetRegisterReportDto> GetFixedAssetRegisterAsync(FixedAssetRegisterRequestDto input)
    {
        var hasDateFilter = string.Equals(input.FilterBasedOn, "Date Range", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(input.FilterBasedOn, "Fiscal Year", StringComparison.OrdinalIgnoreCase);

        DateTime? startDate = hasDateFilter ? input.FromDate?.Date : null;
        DateTime? endDate = hasDateFilter ? input.ToDate?.Date : null;

        if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.DateRangeInvalid);
        }

        var bookQueryable = await _financeBookRepository.GetQueryableAsync();
        var defaultBook = bookQueryable.FirstOrDefault(fb => fb.CompanyId == input.CompanyId && fb.IsDefault);

        if (input.IncludeDefaultBookAssets
            && input.FinanceBookId.HasValue
            && defaultBook != null
            && input.FinanceBookId.Value != defaultBook.Id)
        {
            throw new Volo.Abp.BusinessException(MyERPDomainErrorCodes.ValidationFailed)
                .WithData("detail", "To use a different finance book, please uncheck 'Include Default FB Assets'.");
        }

        Guid? effectiveFinanceBookId = null;
        if (input.IncludeDefaultBookAssets && defaultBook != null)
        {
            effectiveFinanceBookId = defaultBook.Id;
        }
        else if (input.FinanceBookId.HasValue)
        {
            effectiveFinanceBookId = input.FinanceBookId.Value;
        }

        var assetQueryable = (await _assetRepository.WithDetailsAsync(a => a.DepreciationSchedule, a => a.DepreciationDetails)).AsQueryable();
        assetQueryable = assetQueryable.Where(a => a.CompanyId == input.CompanyId
                                              && a.Status != AssetStatus.Draft
                                              && a.Status != AssetStatus.Cancelled
                                              && a.Status != AssetStatus.WorkInProgress);

        if (input.AssetCategoryId.HasValue)
        {
            assetQueryable = assetQueryable.Where(a => a.AssetCategoryId == input.AssetCategoryId.Value);
        }

        if (input.LocationId.HasValue)
        {
            assetQueryable = assetQueryable.Where(a => a.LocationId == input.LocationId.Value);
        }

        if (input.OnlyExistingAssets)
        {
            assetQueryable = assetQueryable.Where(a => a.OpeningAccumulatedDepreciation > 0
                                                    || a.OpeningNumberOfBookedDepreciations > 0
                                                    || (!a.PurchaseReceiptId.HasValue && !a.PurchaseInvoiceId.HasValue));
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            if (string.Equals(input.Status, "In Location", StringComparison.OrdinalIgnoreCase))
            {
                assetQueryable = assetQueryable.Where(a => a.Status != AssetStatus.Sold
                                                        && a.Status != AssetStatus.Scrapped
                                                        && a.Status != AssetStatus.Capitalized);
            }
            else
            {
                assetQueryable = assetQueryable.Where(a => a.Status == AssetStatus.Sold
                                                        || a.Status == AssetStatus.Scrapped
                                                        || a.Status == AssetStatus.Capitalized);
            }
        }

        var candidateAssets = assetQueryable.ToList();

        if (hasDateFilter)
        {
            var useAvailableDate = string.Equals(input.DateBasedOn, "Available For Use Date", StringComparison.OrdinalIgnoreCase);
            candidateAssets = candidateAssets.Where(a =>
            {
                var dateVal = (useAvailableDate ? (a.AvailableForUseDate ?? a.PurchaseDate) : a.PurchaseDate).Date;
                if (startDate.HasValue && dateVal < startDate.Value) return false;
                if (endDate.HasValue && dateVal > endDate.Value) return false;
                return true;
            }).ToList();
        }

        // Filter depreciable assets by finance book linkage
        candidateAssets = candidateAssets.Where(a =>
        {
            if (!a.CalculateDepreciation || !a.DepreciationDetails.Any())
                return true;

            if (input.IncludeDefaultBookAssets)
            {
                return a.DepreciationDetails.Any(d =>
                    d.FinanceBookId == null
                    || (input.FinanceBookId.HasValue && d.FinanceBookId == input.FinanceBookId.Value)
                    || (defaultBook != null && d.FinanceBookId == defaultBook.Id));
            }

            return a.DepreciationDetails.Any(d =>
                d.FinanceBookId == null
                || (input.FinanceBookId.HasValue && d.FinanceBookId == input.FinanceBookId.Value));
        }).ToList();

        // Value adjustments (revaluations)
        var adjQueryable = await _adjustmentRepository.GetQueryableAsync();
        var adjQuery = adjQueryable.Where(a => a.CompanyId == input.CompanyId && a.Status == DocumentStatus.Submitted);
        if (effectiveFinanceBookId.HasValue)
        {
            adjQuery = adjQuery.Where(a => a.FinanceBookId == null || a.FinanceBookId == effectiveFinanceBookId.Value);
        }
        else
        {
            adjQuery = adjQuery.Where(a => a.FinanceBookId == null);
        }

        if (hasDateFilter)
        {
            if (startDate.HasValue) adjQuery = adjQuery.Where(a => a.Date.Date >= startDate.Value);
            if (endDate.HasValue) adjQuery = adjQuery.Where(a => a.Date.Date <= endDate.Value);
        }

        var adjustmentsByAsset = adjQuery
            .ToList()
            .GroupBy(a => a.AssetId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DifferenceAmount));

        var categories = (await _categoryRepository.GetListAsync())
            .ToDictionary(c => c.Id, c => c.CategoryName);

        var rows = new List<FixedAssetRegisterRowDto>();

        foreach (var asset in candidateAssets)
        {
            var scheduleEntries = asset.DepreciationSchedule
                .Where(s => effectiveFinanceBookId.HasValue
                    ? (s.FinanceBookId == null || s.FinanceBookId == effectiveFinanceBookId.Value)
                    : s.FinanceBookId == null)
                .ToList();

            if (hasDateFilter)
            {
                if (startDate.HasValue)
                    scheduleEntries = scheduleEntries.Where(s => s.ScheduleDate.Date >= startDate.Value).ToList();
                if (endDate.HasValue)
                    scheduleEntries = scheduleEntries.Where(s => s.ScheduleDate.Date <= endDate.Value).ToList();
            }

            var bookedDepr = scheduleEntries
                .Where(s => s.IsBooked)
                .Sum(s => s.DepreciationAmount);
            var reversedDepr = scheduleEntries
                .Where(s => !s.IsBooked && s.JournalEntryId.HasValue)
                .Sum(s => s.DepreciationAmount);

            var depreciatedAmount = Math.Max(0m, bookedDepr - reversedDepr);
            var revaluationAmount = adjustmentsByAsset.GetValueOrDefault(asset.Id, 0m);

            var assetValue = CalculateAssetValue(asset, depreciatedAmount, revaluationAmount, endDate);

            string? categoryName = null;
            if (asset.AssetCategoryId.HasValue && categories.TryGetValue(asset.AssetCategoryId.Value, out var cName))
            {
                categoryName = cName;
            }

            rows.Add(new FixedAssetRegisterRowDto
            {
                AssetId = asset.Id,
                AssetNumber = asset.AssetNumber,
                AssetName = asset.AssetName,
                AssetCategoryId = asset.AssetCategoryId,
                AssetCategoryName = categoryName,
                Status = asset.Status.ToString(),
                PurchaseDate = asset.PurchaseDate,
                AvailableForUseDate = asset.AvailableForUseDate,
                GrossPurchaseAmount = asset.TotalAssetCost,
                OpeningAccumulatedDepreciation = asset.OpeningAccumulatedDepreciation,
                DepreciatedAmount = depreciatedAmount,
                RevaluationAmount = revaluationAmount,
                AssetValue = assetValue,
                LocationId = asset.LocationId,
                Location = asset.Location,
                CustodianEmployeeId = asset.CustodianEmployeeId,
            });
        }

        List<FixedAssetRegisterRowDto> finalRows;
        if (string.Equals(input.GroupBy, "Asset Category", StringComparison.OrdinalIgnoreCase))
        {
            finalRows = rows
                .GroupBy(r => (r.AssetCategoryId, r.AssetCategoryName))
                .Select(g => new FixedAssetRegisterRowDto
                {
                    AssetCategoryId = g.Key.AssetCategoryId,
                    AssetCategoryName = g.Key.AssetCategoryName ?? "Uncategorized",
                    GrossPurchaseAmount = g.Sum(r => r.GrossPurchaseAmount),
                    OpeningAccumulatedDepreciation = g.Sum(r => r.OpeningAccumulatedDepreciation),
                    DepreciatedAmount = g.Sum(r => r.DepreciatedAmount),
                    RevaluationAmount = g.Sum(r => r.RevaluationAmount),
                    AssetValue = g.Sum(r => r.AssetValue),
                })
                .OrderBy(r => r.AssetCategoryName)
                .ToList();
        }
        else if (string.Equals(input.GroupBy, "Location", StringComparison.OrdinalIgnoreCase))
        {
            finalRows = rows
                .GroupBy(r => (r.LocationId, r.Location))
                .Select(g => new FixedAssetRegisterRowDto
                {
                    LocationId = g.Key.LocationId,
                    Location = g.Key.Location ?? "Unspecified",
                    GrossPurchaseAmount = g.Sum(r => r.GrossPurchaseAmount),
                    OpeningAccumulatedDepreciation = g.Sum(r => r.OpeningAccumulatedDepreciation),
                    DepreciatedAmount = g.Sum(r => r.DepreciatedAmount),
                    RevaluationAmount = g.Sum(r => r.RevaluationAmount),
                    AssetValue = g.Sum(r => r.AssetValue),
                })
                .OrderBy(r => r.Location)
                .ToList();
        }
        else
        {
            finalRows = rows.OrderBy(r => r.AssetNumber).ToList();
        }

        return new FixedAssetRegisterReportDto
        {
            CompanyId = input.CompanyId,
            Status = input.Status,
            GroupBy = input.GroupBy,
            Rows = finalRows,
        };
    }

    /// <summary>
    /// Determines whether an asset was disposed on or before <paramref name="endDate"/>.
    /// Per ERPNext PR #59928 (commit 59862127b8): is_disposed_by(asset, end_date).
    /// </summary>
    public static bool IsDisposedBy(Asset asset, DateTime? endDate)
    {
        if (asset.Status != AssetStatus.Sold
            && asset.Status != AssetStatus.Scrapped
            && asset.Status != AssetStatus.Capitalized)
        {
            return false;
        }

        return !(endDate.HasValue && asset.DisposalDate.HasValue)
            || asset.DisposalDate.Value.Date <= endDate.Value.Date;
    }

    /// <summary>
    /// Calculates the carrying value of an asset as of <paramref name="endDate"/>.
    /// Per ERPNext PR #59928 (commit 59862127b8): get_asset_value(asset, depreciation_amount, revaluation_amount, end_date).
    /// An asset disposed by the end of the period carries 0 value; if disposed after endDate, it carries
    /// TotalAssetCost - OpeningAccumulatedDepreciation - depreciationAmount + revaluationAmount.
    /// </summary>
    public static decimal CalculateAssetValue(
        Asset asset,
        decimal depreciationAmount,
        decimal revaluationAmount,
        DateTime? endDate = null)
    {
        if (IsDisposedBy(asset, endDate))
        {
            return 0m;
        }

        return asset.TotalAssetCost
             - asset.OpeningAccumulatedDepreciation
             - depreciationAmount
             + revaluationAmount;
    }
}
