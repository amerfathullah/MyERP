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
        if (Status != AssetStatus.Draft)
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

    public void Cancel()
    {
        if (Status != AssetStatus.Draft)
            throw new BusinessException(MyERPDomainErrorCodes.InvalidStatusTransition);
        Status = AssetStatus.Cancelled;
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
                amount = CalculateDepreciationAmount(depreciableAmount, bookValue, totalPeriods, i);
                amount = Math.Min(amount, bookValue); // never exceed remaining book value
            }

            if (amount <= 0) break;

            accumulated += amount;
            bookValue -= amount;

            DepreciationSchedule.Add(new DepreciationScheduleEntry(
                Guid.NewGuid(), Id, scheduleDate, amount, accumulated));
        }
    }

    private decimal CalculateDepreciationAmount(decimal depreciableAmount, decimal bookValue, int totalPeriods, int periodIndex)
    {
        return DepreciationMethod switch
        {
            DepreciationMethod.StraightLine => Math.Round(depreciableAmount / totalPeriods, 2),
            DepreciationMethod.DoubleDecliningBalance => Math.Round(bookValue * (2m / totalPeriods), 2),
            DepreciationMethod.WrittenDownValue => Math.Round(bookValue * (DepreciationRate / 100m), 2),
            _ => 0,
        };
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
    }

    public void ApplyValueAdjustment(decimal newValue)
    {
        ValueAfterDepreciation = newValue;
        GenerateDepreciationSchedule();
    }

    public void UpdateLocationAndCustodian(string? location, Guid? custodianEmployeeId, Guid? locationId = null)
    {
        if (!string.IsNullOrWhiteSpace(location))
        {
            Location = location;
            LocationId = locationId;
        }
        if (custodianEmployeeId.HasValue)
        {
            CustodianEmployeeId = custodianEmployeeId;
        }
    }
}
