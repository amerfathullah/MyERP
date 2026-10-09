using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace MyERP.Assets;

public class AssetDepreciationsAndBalancesRequestDto
{
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Group by dimension: "Asset Category" or "Asset" (default: "Asset Category").
    /// </summary>
    public string GroupBy { get; set; } = "Asset Category";

    public Guid? AssetCategoryId { get; set; }

    public Guid? AssetId { get; set; }

    /// <summary>
    /// Finance book filter. When null/empty, defaults to Company's default finance book
    /// (or entries with no finance book), excluding non-default finance books per ERPNext PR #59925.
    /// </summary>
    public Guid? FinanceBookId { get; set; }
}

public class AssetDepreciationsAndBalancesRowDto
{
    public Guid? AssetCategoryId { get; set; }
    public string? AssetCategoryName { get; set; }

    public Guid? AssetId { get; set; }
    public string? AssetNumber { get; set; }
    public string? AssetName { get; set; }

    /// <summary>
    /// Cost as on day before FromDate (purchases before FromDate, held/disposed in or after period,
    /// plus value adjustments before FromDate).
    /// </summary>
    public decimal ValueAsOnFromDate { get; set; }

    /// <summary>Cost of new purchases made within the period [FromDate, ToDate].</summary>
    public decimal ValueOfNewPurchase { get; set; }

    /// <summary>Gross cost + adjustments till disposal of assets sold in the period.</summary>
    public decimal ValueOfSoldAsset { get; set; }

    /// <summary>Gross cost + adjustments till disposal of assets scrapped in the period.</summary>
    public decimal ValueOfScrappedAsset { get; set; }

    /// <summary>Gross cost + adjustments till disposal of assets consumed in capitalization in the period.</summary>
    public decimal ValueOfCapitalizedAsset { get; set; }

    /// <summary>Value adjustments during the period (including for assets disposed in period).</summary>
    public decimal AdjustmentDuringPeriod { get; set; }

    /// <summary>
    /// Value as on ToDate:
    /// ValueAsOnFromDate + ValueOfNewPurchase - ValueOfSoldAsset - ValueOfScrappedAsset - ValueOfCapitalizedAsset + AdjustmentDuringPeriod
    /// </summary>
    public decimal ValueAsOnToDate { get; set; }

    /// <summary>
    /// Accumulated depreciation booked before FromDate minus reversals before FromDate.
    /// </summary>
    public decimal AccumulatedDepreciationAsOnFromDate { get; set; }

    /// <summary>Depreciation amount booked during the period on or before disposal date.</summary>
    public decimal DepreciationAmountDuringThePeriod { get; set; }

    /// <summary>Accumulated depreciation eliminated upon asset disposal in the period.</summary>
    public decimal DepreciationEliminatedDuringThePeriod { get; set; }

    /// <summary>Depreciation eliminated via reversal during the period.</summary>
    public decimal DepreciationEliminatedViaReversal { get; set; }

    /// <summary>
    /// Accumulated depreciation as on ToDate:
    /// AccumulatedDepreciationAsOnFromDate + DepreciationAmountDuringThePeriod - DepreciationEliminatedDuringThePeriod - DepreciationEliminatedViaReversal
    /// </summary>
    public decimal AccumulatedDepreciationAsOnToDate { get; set; }

    /// <summary>Net Asset Value as on FromDate: ValueAsOnFromDate - AccumulatedDepreciationAsOnFromDate.</summary>
    public decimal NetAssetValueAsOnFromDate { get; set; }

    /// <summary>Net Asset Value as on ToDate: ValueAsOnToDate - AccumulatedDepreciationAsOnToDate.</summary>
    public decimal NetAssetValueAsOnToDate { get; set; }
}

public class AssetDepreciationsAndBalancesReportDto
{
    public Guid CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string GroupBy { get; set; } = "Asset Category";
    public List<AssetDepreciationsAndBalancesRowDto> Rows { get; set; } = new();
}

public interface IAssetReportingAppService : IApplicationService
{
    Task<AssetDepreciationsAndBalancesReportDto> GetAssetDepreciationsAndBalancesAsync(AssetDepreciationsAndBalancesRequestDto input);
}
