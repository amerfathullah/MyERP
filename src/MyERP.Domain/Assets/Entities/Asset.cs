using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace MyERP.Assets.Entities;

public class Asset : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string AssetNumber { get; set; } = null!;
    public string AssetName { get; set; } = null!;
    public AssetStatus Status { get; private set; }

    public Guid CompanyId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public Guid? ItemId { get; set; }

    /// <summary>Denormalized display name, kept in sync with <see cref="LocationId"/> when set.</summary>
    public string? Location { get; set; }

    /// <summary>Link to the Location master (hierarchical, tree-based). Null = free-text Location only.</summary>
    public Guid? LocationId { get; set; }

    public Guid? CustodianEmployeeId { get; set; }

    // Purchase
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal TotalAssetCost => PurchaseAmount + AdditionalCost;

    /// <summary>Asset quantity (default: 1). Multiple quantity assets can be split.</summary>
    public int AssetQuantity { get; set; } = 1;

    /// <summary>Original asset ID if this asset was created by splitting another asset.</summary>
    public Guid? SplitFromAssetId { get; set; }

    /// <summary>
    /// Indicates this asset is a composite / CWIP (Capital Work in Progress) asset.
    /// Per ERPNext commit 3855536ef1: unsubmitted composite assets have status 'WorkInProgress'.
    /// </summary>
    public bool IsCompositeAsset
    {
        get => _isCompositeAsset;
        set
        {
            _isCompositeAsset = value;
            if (Status == AssetStatus.Draft && _isCompositeAsset)
            {
                Status = AssetStatus.WorkInProgress;
            }
            else if (Status == AssetStatus.WorkInProgress && !_isCompositeAsset)
            {
                Status = AssetStatus.Draft;
            }
        }
    }
    private bool _isCompositeAsset;

    /// <summary>Source Purchase Receipt that created this asset (for return validation).</summary>
    public Guid? PurchaseReceiptId { get; set; }

    /// <summary>Source Purchase Invoice that created this asset (for return validation).</summary>
    public Guid? PurchaseInvoiceId { get; set; }

    // Depreciation
    public bool CalculateDepreciation { get; set; }
    public DepreciationMethod DepreciationMethod { get; set; }
    public int UsefulLifeMonths { get; set; }
    public decimal DepreciationRate { get; set; }
    public int FrequencyMonths { get; set; } = 12;
    public DateTime? AvailableForUseDate { get; set; }
    public decimal OpeningAccumulatedDepreciation { get; set; }
    public decimal ValueAfterDepreciation { get; set; }
    public bool IsFullyDepreciated { get; set; }
    /// <summary>
    /// Snapshot of AccountsSettings.CalculateDeprUsingTotalDays at asset creation — captured
    /// once rather than re-read live, so a later change to the company-wide setting doesn't
    /// retroactively alter an existing asset's already-communicated schedule. When true,
    /// Straight Line periods are weighted by actual elapsed calendar days instead of divided
    /// equally, so a period spanning a shorter month depreciates proportionally less.
    /// </summary>
    public bool UseTotalDaysForDepreciation { get; set; }

    // Disposal
    public DateTime? DisposalDate { get; set; }
    public decimal? DisposalAmount { get; set; }

    public string? Notes { get; set; }

    public List<DepreciationScheduleEntry> DepreciationSchedule { get; private set; } = new();

    /// <summary>
    /// Per-finance-book depreciation settings. Each entry defines method, rate, frequency
    /// for a specific finance book. Enables multi-book depreciation (tax vs management).
    /// Per gotcha #64: asset status driven by DEFAULT finance book, not first.
    /// </summary>
    public List<AssetDepreciationDetail> DepreciationDetails { get; private set; } = new();

    protected Asset() { }

    public Asset(Guid id, Guid companyId, string assetNumber, string assetName,
        DateTime purchaseDate, decimal purchaseAmount, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = Check.NotDefaultOrNull<Guid>(companyId, nameof(companyId));
        AssetNumber = assetNumber;
        AssetName = assetName;
        PurchaseDate = purchaseDate;
        PurchaseAmount = purchaseAmount;
        ValueAfterDepreciation = purchaseAmount;
        Status = AssetStatus.Draft;
        TenantId = tenantId;
    }

    /// <summary>
    /// Calculates purchase amount for auto-created assets from purchase documents.
    /// Per ERPNext PR #57618: uses valuation_rate × qty (not base_net_amount)
    /// because valuation rate includes landed costs and other adjustments.
    /// </summary>
    public static decimal CalculatePurchaseAmountFromValuation(decimal valuationRate, decimal qty)
        => valuationRate * qty;

    public void Submit()
    {
        if (Status != AssetStatus.Draft && Status != AssetStatus.WorkInProgress)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetStatus.Submitted;
    }

    public void MarkPartiallyDepreciated()
    {
        if (Status is not (AssetStatus.Submitted or AssetStatus.PartiallyDepreciated))
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetStatus.PartiallyDepreciated;
    }

    public void MarkFullyDepreciated()
    {
        Status = AssetStatus.FullyDepreciated;
        IsFullyDepreciated = true;
        ValueAfterDepreciation = 0;
    }

    public void Sell(DateTime disposalDate, decimal disposalAmount)
    {
        if (Status is AssetStatus.Draft or AssetStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetStatus.Sold;
        DisposalDate = disposalDate;
        DisposalAmount = disposalAmount;
    }

    public void Scrap(DateTime disposalDate)
    {
        if (Status is AssetStatus.Draft or AssetStatus.Cancelled)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetStatus.Scrapped;
        DisposalDate = disposalDate;
        DisposalAmount = 0;
    }

    /// <summary>
    /// Cancels the asset. Allowed for Draft, and for Submitted/PartiallyDepreciated/
    /// FullyDepreciated assets that have no GL-posted depreciation outstanding — cancelling
    /// those is then a pure status change, no reversal needed here. If depreciation has been
    /// booked, the caller must reverse it first via ReverseAllBookedDepreciation() (which
    /// itself requires every booked entry's Journal Entry to already be reversed via
    /// DocumentPostingOrchestrator — same "caller reverses GL, this only updates state"
    /// contract Restore() uses). Still blocked for InMaintenance/Sold/Scrapped (per ERPNext:
    /// complete maintenance, or restore/un-sell first). Once Cancelled,
    /// DepreciationSchedulerJob's own status filter (Status != Cancelled) stops it from ever
    /// posting the remaining schedule.
    /// </summary>
    public void Cancel()
    {
        var hasBookedDepreciation = DepreciationSchedule.Any(e => e.IsBooked);
        var cancellable = Status == AssetStatus.Draft
            || (Status is AssetStatus.Submitted or AssetStatus.PartiallyDepreciated or AssetStatus.FullyDepreciated
                && !hasBookedDepreciation);

        if (!cancellable)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        Status = AssetStatus.Cancelled;
    }

    /// <summary>
    /// Resets depreciation state ahead of a full cancel from PartiallyDepreciated/
    /// FullyDepreciated — clears the schedule and regenerates it fresh (as if no periods had
    /// ever been booked), and resets each finance book's tracked value. Caller MUST reverse
    /// every currently-booked entry's Journal Entry via
    /// DocumentPostingOrchestrator.ReverseGlForJournalEntryAsync BEFORE calling this — it only
    /// updates the asset's own schedule/value state, matching Restore()'s GL-then-state
    /// ordering contract.
    /// </summary>
    public void ReverseAllBookedDepreciation()
    {
        DepreciationSchedule.Clear();
        ValueAfterDepreciation = TotalAssetCost - OpeningAccumulatedDepreciation;
        IsFullyDepreciated = false;
        GenerateDepreciationSchedule();

        foreach (var detail in DepreciationDetails)
        {
            detail.ValueAfterDepreciation = detail.NetPurchaseAmount - detail.OpeningAccumulatedDepreciation;
        }
    }

    /// <summary>
    /// Reverses a scrap — restores the asset's pre-disposal status and clears the disposal
    /// fields. Scrap-only, matching ERPNext's restore_asset (a sold asset has real external
    /// proceeds/counterparty and isn't reversible this way). Caller is responsible for
    /// reversing the disposal GL entry first (DocumentPostingOrchestrator.ReverseGlForDocumentAsync)
    /// — this method only updates the asset's own state.
    /// </summary>
    public void Restore()
    {
        if (Status != AssetStatus.Scrapped)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);

        DisposalDate = null;
        DisposalAmount = null;
        Status = AssetStatus.Submitted;
        RecalculateStatus();
    }

    /// <summary>
    /// Marks this asset as consumed/capitalized into another asset.
    /// Per ERPNext commit 2391c859b2 / a121c30b56.
    /// </summary>
    public void MarkAsCapitalized(DateTime disposalDate)
    {
        Status = AssetStatus.Capitalized;
        DisposalDate = disposalDate;
    }

    /// <summary>
    /// Restores a previously capitalized asset when the capitalization is cancelled.
    /// Per ERPNext commit 2391c859b2 / a121c30b56.
    /// </summary>
    public void RestoreFromCapitalization()
    {
        if (Status != AssetStatus.Capitalized) return;
        DisposalDate = null;
        Status = AssetStatus.Submitted;
        RecalculateStatus();
        if (CalculateDepreciation)
        {
            GenerateDepreciationSchedule();
        }
    }

    /// <summary>
    /// Computes depreciation rate and initial value after depreciation.
    /// Per ERPNext commit 48311ee5c5: called on asset creation/update before checking CalculateDepreciation.
    /// </summary>
    public void SetDepreciationRateAndValueAfterDepreciation()
    {
        if (SplitFromAssetId.HasValue) return;

        ValueAfterDepreciation = TotalAssetCost - OpeningAccumulatedDepreciation;
        if (UsefulLifeMonths > 0 && FrequencyMonths > 0)
        {
            var periods = UsefulLifeMonths / FrequencyMonths;
            if (periods > 0 && DepreciationRate == 0)
            {
                DepreciationRate = Math.Round(100m / periods, 4);
            }
        }
    }

    /// <summary>
    /// (Re)generates the depreciation schedule. Preserves already-booked (GL-posted) rows —
    /// only unbooked rows are cleared and rebuilt. Regenerating the whole schedule from
    /// scratch would silently recreate fresh, unbooked duplicates of periods whose Journal
    /// Entry already exists, causing the depreciation scheduler to double-post them.
    /// Called both on first schedule creation (no booked rows yet) and incrementally by
    /// <see cref="ApplyRepairCapitalization"/>/<see cref="ApplyValueAdjustment"/> on assets
    /// that may already be Submitted/PartiallyDepreciated with real posted history.
    /// </summary>
    public void GenerateDepreciationSchedule()
    {
        if (!CalculateDepreciation || UsefulLifeMonths <= 0) return;

        var bookedEntries = DepreciationSchedule.Where(e => e.IsBooked).OrderBy(e => e.ScheduleDate).ToList();
        DepreciationSchedule.RemoveAll(e => !e.IsBooked);

        var startDate = AvailableForUseDate ?? PurchaseDate;
        var depreciableAmount = TotalAssetCost - OpeningAccumulatedDepreciation;
        var totalPeriods = FrequencyMonths > 0 ? UsefulLifeMonths / FrequencyMonths : 0;
        if (totalPeriods <= 0) return;

        var bookedCount = bookedEntries.Count;
        var accumulated = bookedCount > 0 ? bookedEntries[^1].AccumulatedDepreciation : OpeningAccumulatedDepreciation;
        var bookValue = TotalAssetCost - accumulated;

        for (int i = bookedCount; i < totalPeriods; i++)
        {
            var scheduleDate = startDate.AddMonths((i + 1) * FrequencyMonths);
            decimal amount;

            if (i == totalPeriods - 1)
            {
                // Final period absorbs rounding difference so book value reaches zero exactly
                amount = Math.Max(bookValue, 0);
            }
            else
            {
                amount = CalculateDepreciationAmount(depreciableAmount, bookValue, totalPeriods, i, startDate);
                amount = Math.Min(amount, bookValue); // never exceed remaining book value
            }

            if (amount <= 0) break;

            accumulated += amount;
            bookValue -= amount;

            DepreciationSchedule.Add(new DepreciationScheduleEntry(
                Guid.NewGuid(), Id, scheduleDate, amount, accumulated));
        }
    }

    /// <summary>
    /// Simulates book value as of an arbitrary date (typically a disposal date) WITHOUT
    /// mutating the real schedule — replicates GenerateDepreciationSchedule's period-by-period
    /// logic up to asOfDate, prorating the final partial period by elapsed-vs-total days within
    /// it. Per ERPNext get_value_after_depreciation_on_disposal_date: disposal gain/loss should
    /// use this instead of the last-booked entry's ValueAfterDepreciation, which can be stale
    /// by however long it's been since the depreciation scheduler last ran (up to a full period).
    /// </summary>
    public decimal SimulateBookValueAtDate(DateTime asOfDate)
    {
        // Per ERPNext get_value_after_depreciation_on_disposal_date: an asset that doesn't
        // calculate depreciation has no schedule to simulate — use the stored value as-is.
        if (!CalculateDepreciation || UsefulLifeMonths <= 0)
            return ValueAfterDepreciation;

        var startDate = AvailableForUseDate ?? PurchaseDate;
        if (asOfDate <= startDate)
            return TotalAssetCost - OpeningAccumulatedDepreciation;

        var bookedEntries = DepreciationSchedule.Where(e => e.IsBooked).OrderBy(e => e.ScheduleDate).ToList();
        var depreciableAmount = TotalAssetCost - OpeningAccumulatedDepreciation;
        var totalPeriods = FrequencyMonths > 0 ? UsefulLifeMonths / FrequencyMonths : 0;
        if (totalPeriods <= 0) return ValueAfterDepreciation;

        var bookedCount = bookedEntries.Count;
        var accumulated = bookedCount > 0 ? bookedEntries[^1].AccumulatedDepreciation : OpeningAccumulatedDepreciation;
        var bookValue = TotalAssetCost - accumulated;
        var periodStart = bookedCount > 0 ? bookedEntries[^1].ScheduleDate : startDate;

        for (int i = bookedCount; i < totalPeriods; i++)
        {
            var scheduleDate = startDate.AddMonths((i + 1) * FrequencyMonths);
            var fullPeriodAmount = i == totalPeriods - 1
                ? Math.Max(bookValue, 0)
                : Math.Min(CalculateDepreciationAmount(depreciableAmount, bookValue, totalPeriods, i, startDate), bookValue);

            if (fullPeriodAmount <= 0) break;

            if (scheduleDate > asOfDate)
            {
                // asOfDate falls mid-period — prorate by elapsed days within this period only.
                var periodDays = (scheduleDate - periodStart).TotalDays;
                var elapsedDays = (asOfDate - periodStart).TotalDays;
                if (periodDays > 0 && elapsedDays > 0)
                {
                    var prorated = Math.Round(fullPeriodAmount * (decimal)(elapsedDays / periodDays), 2);
                    bookValue -= Math.Min(prorated, bookValue);
                }
                break;
            }

            bookValue -= fullPeriodAmount;
            periodStart = scheduleDate;
        }

        return Math.Max(bookValue, 0);
    }

    private decimal CalculateDepreciationAmount(decimal depreciableAmount, decimal bookValue, int totalPeriods, int periodIndex, DateTime startDate)
    {
        return DepreciationMethod switch
        {
            DepreciationMethod.StraightLine => UseTotalDaysForDepreciation
                ? CalculateStraightLineByTotalDays(depreciableAmount, totalPeriods, periodIndex, startDate)
                : Math.Round(depreciableAmount / totalPeriods, 2),
            DepreciationMethod.DoubleDecliningBalance => Math.Round(bookValue * (2m / totalPeriods), 2),
            DepreciationMethod.WrittenDownValue => Math.Round(bookValue * (DepreciationRate / 100m), 2),
            _ => 0,
        };
    }

    /// <summary>
    /// Per ERPNext AccountsSettings.calculate_depreciation_using_total_days: instead of dividing
    /// the depreciable amount equally across periods, weight each period by its actual elapsed
    /// calendar days (a period spanning a shorter month depreciates proportionally less). Only
    /// meaningful for Straight Line — WDV/DDB already derive each period from book value x rate,
    /// not from an equal division of the total.
    /// </summary>
    private decimal CalculateStraightLineByTotalDays(decimal depreciableAmount, int totalPeriods, int periodIndex, DateTime startDate)
    {
        var scheduleStartDate = startDate.AddMonths(totalPeriods * FrequencyMonths);
        var totalDays = (scheduleStartDate - startDate).TotalDays;
        if (totalDays <= 0) return Math.Round(depreciableAmount / totalPeriods, 2);

        var periodStart = startDate.AddMonths(periodIndex * FrequencyMonths);
        var periodEnd = startDate.AddMonths((periodIndex + 1) * FrequencyMonths);
        var periodDays = (periodEnd - periodStart).TotalDays;

        return Math.Round(depreciableAmount * (decimal)(periodDays / totalDays), 2);
    }

    public void ApplyRepairCapitalization(decimal additionalCost, int increaseInUsefulLifeMonths)
    {
        AdditionalCost += additionalCost;
        ValueAfterDepreciation += additionalCost;
        if (increaseInUsefulLifeMonths > 0)
        {
            UsefulLifeMonths += increaseInUsefulLifeMonths;
        }
        GenerateDepreciationSchedule();
        RecalculateStatus();
    }

    public void ApplyValueAdjustment(decimal newValue)
    {
        ValueAfterDepreciation = newValue;
        GenerateDepreciationSchedule();
        RecalculateStatus();
    }

    /// <summary>
    /// Recalculates asset status based on ValueAfterDepreciation and depreciation history.
    /// Per ERPNext PR #48086 / commit da2663b8dc.
    /// </summary>
    public void RecalculateStatus()
    {
        if (Status is AssetStatus.Sold or AssetStatus.Scrapped or AssetStatus.Cancelled or AssetStatus.Draft or AssetStatus.Capitalized or AssetStatus.WorkInProgress)
            return;

        if (ValueAfterDepreciation <= 0)
            Status = AssetStatus.FullyDepreciated;
        else if (ValueAfterDepreciation < TotalAssetCost)
            Status = AssetStatus.PartiallyDepreciated;
        else
            Status = AssetStatus.Submitted;
    }

    public void UpdateLocationAndCustodian(string? location, Guid? custodianEmployeeId, Guid? locationId = null)
    {
        if (!string.IsNullOrWhiteSpace(location) || locationId.HasValue)
        {
            Location = location;
            LocationId = locationId;
        }
        CustodianEmployeeId = custodianEmployeeId;
    }

    /// <summary>
    /// Scales proportional asset values and quantities when an asset is split.
    /// Per ERPNext: split_asset() in assets/doctype/asset/mapper.py.
    /// </summary>
    public void ApplySplitScale(decimal scalingFactor, int newQuantity)
    {
        AssetQuantity = newQuantity;
        PurchaseAmount = Math.Round(PurchaseAmount * scalingFactor, 2);
        AdditionalCost = Math.Round(AdditionalCost * scalingFactor, 2);
        OpeningAccumulatedDepreciation = Math.Round(OpeningAccumulatedDepreciation * scalingFactor, 2);
        ValueAfterDepreciation = Math.Round(ValueAfterDepreciation * scalingFactor, 2);

        foreach (var detail in DepreciationDetails)
        {
            detail.NetPurchaseAmount = Math.Round(detail.NetPurchaseAmount * scalingFactor, 2);
            detail.OpeningAccumulatedDepreciation = Math.Round(detail.OpeningAccumulatedDepreciation * scalingFactor, 2);
            detail.ValueAfterDepreciation = Math.Round(detail.ValueAfterDepreciation * scalingFactor, 2);
            detail.ExpectedValueAfterUsefulLife = Math.Round(detail.ExpectedValueAfterUsefulLife * scalingFactor, 2);
        }

        DepreciationSchedule.Clear();
        GenerateDepreciationSchedule();
    }
}
