using System;
using System.Collections.Generic;
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
    public string? Location { get; set; }
    public Guid? CustodianEmployeeId { get; set; }

    // Purchase
    public DateTime PurchaseDate { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal AdditionalCost { get; set; }
    public decimal TotalAssetCost => PurchaseAmount + AdditionalCost;

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

    public List<DepreciationScheduleEntry> DepreciationSchedule { get; set; } = new();

    protected Asset() { }

    public Asset(Guid id, Guid companyId, string assetNumber, string assetName,
        DateTime purchaseDate, decimal purchaseAmount, Guid? tenantId = null)
        : base(id)
    {
        CompanyId = companyId;
        AssetNumber = assetNumber;
        AssetName = assetName;
        PurchaseDate = purchaseDate;
        PurchaseAmount = purchaseAmount;
        ValueAfterDepreciation = purchaseAmount;
        Status = AssetStatus.Draft;
        TenantId = tenantId;
    }

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

    public void GenerateDepreciationSchedule()
    {
        if (!CalculateDepreciation || UsefulLifeMonths <= 0) return;

        DepreciationSchedule.Clear();
        var startDate = AvailableForUseDate ?? PurchaseDate;
        var depreciableAmount = TotalAssetCost - OpeningAccumulatedDepreciation;
        var periods = UsefulLifeMonths / FrequencyMonths;
        if (periods <= 0) return;

        var accumulated = OpeningAccumulatedDepreciation;
        var bookValue = depreciableAmount;

        for (int i = 0; i < periods; i++)
        {
            var scheduleDate = startDate.AddMonths((i + 1) * FrequencyMonths);
            decimal amount;

            if (i == periods - 1)
            {
                // Final period absorbs rounding difference so book value reaches zero exactly
                amount = Math.Max(bookValue, 0);
            }
            else
            {
                amount = CalculateDepreciationAmount(depreciableAmount, bookValue, periods, i);
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
}
