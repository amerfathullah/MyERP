using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace MyERP.Assets.Entities;

/// <summary>
/// Asset Depreciation Detail — per-finance-book depreciation settings on an Asset.
/// Per ERPNext assets/doctype/asset_finance_book:
/// - Each asset can have MULTIPLE depreciation schedules (one per finance book).
/// - Different methods/rates/frequencies per book (e.g., SLM for tax, WDV for management).
/// - DepreciationScheduleEntry.FinanceBookId links schedule entries to this detail.
/// Per DO-NOT: "Skip Finance Book separation in GL entries (each book gets independent schedules)"
/// Per gotcha #64: "Asset status driven by default finance book, not first book"
/// </summary>
public class AssetDepreciationDetail : FullAuditedEntity<Guid>
{
    public Guid AssetId { get; set; }

    /// <summary>
    /// Finance Book this detail belongs to. Null = default/primary book.
    /// Per gotcha #639: null finance_book entries ALWAYS included in queries.
    /// </summary>
    public Guid? FinanceBookId { get; set; }

    /// <summary>Depreciation method for this finance book.</summary>
    public DepreciationMethod DepreciationMethod { get; set; }

    /// <summary>Total number of depreciation periods.</summary>
    public int TotalNumberOfDepreciations { get; set; }

    /// <summary>Frequency in months between depreciations (1=monthly, 3=quarterly, 12=annually).</summary>
    public int FrequencyOfDepreciation { get; set; } = 12;

    /// <summary>Date when depreciation starts for this book.</summary>
    public DateTime? DepreciationStartDate { get; set; }

    /// <summary>Expected residual/salvage value at end of useful life.</summary>
    public decimal ExpectedValueAfterUsefulLife { get; set; }

    /// <summary>Depreciation rate percentage (for WDV/DDB methods).</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Net purchase amount used as depreciation base.
    /// Per gotcha #asset: net_purchase_amount must equal purchase_amount for non-existing assets.
    /// </summary>
    public decimal NetPurchaseAmount { get; set; }

    /// <summary>Opening accumulated depreciation (for imported/existing assets).</summary>
    public decimal OpeningAccumulatedDepreciation { get; set; }

    /// <summary>Number of depreciations already booked (for existing assets).</summary>
    public int OpeningNumberOfBookedDepreciations { get; set; }

    /// <summary>Current value after depreciation for this book.</summary>
    public decimal ValueAfterDepreciation { get; set; }

    protected AssetDepreciationDetail() { }

    public AssetDepreciationDetail(Guid id, Guid assetId, DepreciationMethod method,
        int totalDepreciations, int frequencyMonths, decimal netPurchaseAmount)
        : base(id)
    {
        AssetId = assetId;
        DepreciationMethod = method;
        TotalNumberOfDepreciations = totalDepreciations;
        FrequencyOfDepreciation = frequencyMonths;
        NetPurchaseAmount = netPurchaseAmount;
        ValueAfterDepreciation = netPurchaseAmount;
    }

    /// <summary>
    /// Calculates the depreciable amount for this finance book.
    /// </summary>
    public decimal DepreciableAmount =>
        NetPurchaseAmount - ExpectedValueAfterUsefulLife - OpeningAccumulatedDepreciation;
}
